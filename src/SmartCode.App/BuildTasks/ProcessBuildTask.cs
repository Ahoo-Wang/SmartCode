using Microsoft.Extensions.Logging;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using HandlebarsDotNet;
using SmartCode.Utilities;

namespace SmartCode.App.BuildTasks
{
    public class ProcessBuildTask : IBuildTask
    {
        const string CREATE_NO_WINDOW = "CreateNoWindow";
        const string WORKING_DIRECTORY = "WorkingDirectory";
        const string WRITE_LINES = "WriteLines";
        const string FILE_NAME = "FileName";
        const string ARGS = "Args";
        const string TIMEOUT = "Timeout";
        private readonly ILogger<ProcessBuildTask> _logger;
        const bool DEFAULT_CREATE_NO_WINDOW = true;
        public bool Initialized => true;

        public string Name => "Process";
        public ProcessBuildTask(ILogger<ProcessBuildTask> logger)
        {
            _logger = logger;
        }

        public Task Build(BuildContext context)
        {
            if (!context.Build.Parameters.Value(FILE_NAME, out string fileName))
            {
                throw new SmartCodeException($"Build:{context.BuildKey},Can not find Parameter:{FILE_NAME}!");
            }
            var startInfo = new ProcessStartInfo(fileName)
            {
                UseShellExecute = false,
                RedirectStandardInput = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = DEFAULT_CREATE_NO_WINDOW
            };
            if (context.Build.Parameters.Value(nameof(ProcessStartInfo.RedirectStandardInput), out bool redirectStandardInput))
            {
                startInfo.RedirectStandardInput = redirectStandardInput;
            }
            if (context.Build.Parameters.Value(nameof(ProcessStartInfo.RedirectStandardOutput), out bool redirectStandardOutput))
            {
                startInfo.RedirectStandardOutput = redirectStandardOutput;
            }
            if (context.Build.Parameters.Value(nameof(ProcessStartInfo.RedirectStandardError), out bool redirectStandardError))
            {
                startInfo.RedirectStandardError = redirectStandardError;
            }
            if (context.Build.Parameters.Value(ARGS, out string args))
            {
                startInfo.Arguments = Handlebars.Compile(args)(context);
            }
            if (context.Build.Parameters.Value(WORKING_DIRECTORY, out string workingDic))
            {
                startInfo.WorkingDirectory = Handlebars.Compile(workingDic)(context);
            }
            if (context.Build.Parameters.Value(CREATE_NO_WINDOW, out bool createNoWin))
            {
                startInfo.CreateNoWindow = createNoWin;
            }
            _logger.LogDebug($"--------Process.FileName:{startInfo.FileName},Args:{startInfo.Arguments} Start--------");
            using (var process = Process.Start(startInfo))
            {
                if (startInfo.RedirectStandardOutput)
                {
                    process.OutputDataReceived += Process_OutputDataReceived;
                    process.BeginOutputReadLine();
                }
                if (startInfo.RedirectStandardError)
                {
                    process.ErrorDataReceived += Process_ErrorDataReceived;
                    process.BeginErrorReadLine();
                }
                if (context.Build.Parameters.Value(WRITE_LINES, out IEnumerable<object> lines))
                {
                    foreach (var line in lines)
                    {
                        var lineCommand = Handlebars.Compile(line.ToString())(context);
                        _logger.LogDebug($"StandardInput.WriteLine: [{lineCommand}].");
                        process.StandardInput.WriteLine(lineCommand);
                    }
                }

                if (!(context.Build.Parameters.Value(nameof(Process.WaitForExit), out bool waitForExit)
                      && !waitForExit))
                {
                    if (context.Build.Parameters.Value(TIMEOUT, out int _timeout))
                    {
                        process.WaitForExit(_timeout);
                    }
                    else
                    {
                        process.WaitForExit();
                    }
                }
                _logger.LogDebug($"--------Process.FileName:{startInfo.FileName},Args:{startInfo.Arguments},Taken:{process.TotalProcessorTime.TotalMilliseconds} End--------");
            }
            return Task.CompletedTask;
        }

        private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
                Console.WriteLine(e.Data);
        }

        private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
                Console.WriteLine(e.Data);
        }

        public void Initialize(IDictionary<string, object> parameters)
        {

        }
    }
}
