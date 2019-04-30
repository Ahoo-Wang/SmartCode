﻿using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SmartCode.Configuration;
using SmartSql;

namespace SmartCode.ETL
{
    public class ExtractDataSource : AbstractExtractDataSource
    {
        public ExtractDataSource(Project project, ILoggerFactory loggerFactory, ILogger<ExtractDataSource> logger,
            IProjectBuilder projectBuilder, IPluginManager pluginManager) : base(project, loggerFactory, logger,
            projectBuilder, pluginManager)
        {
        }

        public override string Name => "Extract";
        public DataSet DataSet { get; set; }
        public DataTable TransformData { get; set; }
        protected override async Task LoadData(RequestContext requestContext)
        {
            DataSet = await SqlMapper.GetDataSetAsync(requestContext);
            TransformData = DataSet.Tables[0];
        }
        public override long GetMaxId(string pkColumn)
        {
            return TransformData.Rows.Cast<DataRow>().AsParallel().Max(dr => Convert.ToInt64(dr[pkColumn]));
        }

        public override DateTime GetMaxModifyTime(string modifyTime)
        {
            return TransformData.Rows.Cast<DataRow>().AsParallel().Max(dr => Convert.ToDateTime(dr[modifyTime]));
        }

        public override int GetCount()
        {
            return TransformData.Rows.Count;
        }


    }
}