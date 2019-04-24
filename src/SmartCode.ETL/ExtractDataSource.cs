﻿using Microsoft.Extensions.Logging;
using SmartCode.Configuration;
using SmartCode.ETL.Entity;
using SmartSql;
using SmartSql.Abstractions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using static SmartCode.Db.SmartSqlMapperFactory;

namespace SmartCode.ETL
{
    public class ExtractDataSource : IDataSource
    {
        private readonly Project _project;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<ExtractDataSource> _logger;
        private readonly IProjectBuilder _projectBuilder;
        private readonly IPluginManager _pluginManager;

        public bool Initialized { get; private set; }
        public string Name => "Extract";
        public DbSet DbSet { get; set; }
        public DbTable TransformData { get; set; }

        private IETLTaskRepository _etlRepository;
        public ExtractDataSource(Project project
            , ILoggerFactory loggerFactory
            , ILogger<ExtractDataSource> logger
            , IProjectBuilder projectBuilder
            , IPluginManager pluginManager)
        {
            _project = project;
            _loggerFactory = loggerFactory;
            _logger = logger;
            _projectBuilder = projectBuilder;
            _pluginManager = pluginManager;
            _etlRepository = _pluginManager.Resolve<IETLTaskRepository>(_project.GetETLRepository());

        }
        private void InitProjectBuilderEvent()
        {
            _projectBuilder.OnStartup += _projectBuilder_OnStartup;
            _projectBuilder.OnFailed += _projectBuilder_OnFailed;
            _projectBuilder.OnSucceed += _projectBuilder_OnSucceed;
        }

        private async Task _projectBuilder_OnSucceed(object sender, OnProjectBuildSucceedEventArgs eventArgs)
        {
            await _etlRepository.Success(_project.GetETKTaskId());
        }

        private async Task _projectBuilder_OnStartup(object sender, OnProjectBuildStartupEventArgs eventArgs)
        {
            var etlTaskId = await _etlRepository.Startup(_project.ConfigPath, _project.GetETLCode());
            _project.SetETKTaskId(etlTaskId);
        }

        private async Task _projectBuilder_OnFailed(object sender, OnProjectBuildFailedEventArgs eventArgs)
        {
            await _etlRepository.Fail(_project.GetETKTaskId(), eventArgs.ErrorException.Message);
        }

        public async Task InitData()
        {
            var dataSource = _project.DataSource;
            dataSource.Parameters.EnsureValue("DbProvider", out string dbProvider);
            dataSource.Parameters.EnsureValue("ConnectionString", out string connString);
            dataSource.Parameters.Value("PKColumn", out string pkColumn);
            dataSource.Parameters.Value("ModifyTime", out string modifyTime);
            dataSource.Parameters.EnsureValue("Query", out string queryCmd);
            #region CreateSqlMapper
            var smartSqlOptions = new CreateSmartSqlMapperOptions
            {
                LoggerFactory = _loggerFactory,
                ProviderName = dbProvider,
                Alias = Name,
                DataSource = new SmartSql.Configuration.WriteDataSource
                {
                    Name = Name,
                    ConnectionString = connString
                }
            };
            var sqlMapper = Create(smartSqlOptions);
            #endregion
            var lastExtract = await _etlRepository.GetLastExtract(_project.GetETLCode());
            _project.SetETLLastExtract(lastExtract);
            var queryParams = new Dictionary<string, object>
            {
                { "LastMaxId",lastExtract.MaxId},
                { "LastQueryTime",lastExtract.QueryTime},
                { "LastMaxModifyTime",lastExtract.MaxModifyTime},
            };
            var extractEntity = new ETLExtract
            {
                QueryTime = DateTime.Now,
                PKColumn = pkColumn,
                QueryCommand = new ETLDbCommand
                {
                    Command = queryCmd,
                    Parameters = queryParams
                },
                QuerySize = -1,
                MaxId = -1
            };
            Stopwatch stopwatch = Stopwatch.StartNew();
            DbSet = await sqlMapper.GetDbSetAsync(new RequestContext { RealSql = queryCmd, Request = queryParams });
            TransformData = DbSet.Tables.First();
            stopwatch.Stop();
            extractEntity.QuerySize = TransformData.Rows.Count;
            extractEntity.QueryCommand.Taken = stopwatch.ElapsedMilliseconds;
            _logger.LogWarning($"InitData,Data.Size:{extractEntity.QuerySize},Taken:{extractEntity.QueryCommand.Taken}ms!");

            dataSource.Parameters.Value("PkIsNumeric", out bool pkIsNumeric);
            dataSource.Parameters.Value("AutoIncrement", out bool autoIncrement);

            if (!String.IsNullOrEmpty(pkColumn)
                && (pkIsNumeric || autoIncrement)
                && extractEntity.QuerySize > 0)
            {
                var maxId = TransformData.Rows.Max(m => m.Cells[pkColumn].Value);
                extractEntity.MaxId = Convert.ToInt64(maxId);
            }
            else
            {
                extractEntity.MaxId = lastExtract.MaxId;
            }

            if (!String.IsNullOrEmpty(modifyTime)
                && extractEntity.QuerySize > 0)
            {
                var maxModifyTime = TransformData.Rows.Max(m =>
                {
                    var cell = m.Cells[modifyTime];
                    if (cell.Value is DateTime dateTime)
                    {
                        return dateTime;
                    }
                    return Convert.ToDateTime(cell.Value);
                });
                extractEntity.MaxModifyTime = Convert.ToDateTime(maxModifyTime);
            }
            else
            {
                extractEntity.MaxModifyTime = lastExtract.MaxModifyTime;
            }


            await _etlRepository.Extract(_project.GetETKTaskId(), extractEntity);
        }


        public void Initialize(IDictionary<string, object> parameters)
        {
            InitProjectBuilderEvent();
            Initialized = true;
        }
    }
}
