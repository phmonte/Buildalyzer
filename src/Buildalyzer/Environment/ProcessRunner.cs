using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace Buildalyzer.Environment
{
    internal class ProcessRunner : IDisposable
    {
        private readonly ILogger<ProcessRunner> _logger;
        private readonly List<string> _output;

        public Process Process { get; }

        public ProcessRunner(
            string fileName,
            string arguments,
            string workingDirectory,
            Dictionary<string, string> environmentVariables,
            ILoggerFactory loggerFactory,
            List<string> output = null)
        {
            _logger = loggerFactory?.CreateLogger<ProcessRunner>();
            _output = output;
            Process = new Process();

            // Create the process info
            Process.StartInfo.FileName = fileName;
            Process.StartInfo.Arguments = arguments;
            Process.StartInfo.WorkingDirectory = workingDirectory;
            Process.StartInfo.CreateNoWindow = true;
            Process.StartInfo.UseShellExecute = false;

            // Copy over environment variables
            if(environmentVariables != null)
            {
                foreach(KeyValuePair<string, string> variable in environmentVariables)
                {
                    Process.StartInfo.Environment[variable.Key] = variable.Value;
                    Process.StartInfo.EnvironmentVariables[variable.Key] = variable.Value;
                }
            }

            // Capture output
            if (_logger != null || output != null)
            {
                Process.StartInfo.RedirectStandardOutput = true;
                Process.OutputDataReceived += DataReceived;
            }

            Process.Exited += Exited;
        }
        
        public ProcessRunner Start()
        {
            Process.Start();
            _logger?.LogDebug($"{System.Environment.NewLine}Started process {Process.Id}: \"{Process.StartInfo.FileName}\" {Process.StartInfo.Arguments}{System.Environment.NewLine}");
            if (_logger != null || _output != null)
            {
                Process.BeginOutputReadLine();
            }
            return this;
        }

        private void Exited(object sender, EventArgs e)
        {
            _logger?.LogDebug($"Process {Process.Id} exited with code {Process.ExitCode}{System.Environment.NewLine}{System.Environment.NewLine}");
        }

        public void Dispose()
        {
            Process.Exited -= Exited;
            if (_logger != null || _output != null)
            {
                Process.OutputDataReceived -= DataReceived;
            }
            Process.Close();
        }        

        private void DataReceived(object sender, DataReceivedEventArgs e)
        {
            _output?.Add(e.Data);
            _logger?.LogDebug($"{e.Data}{System.Environment.NewLine}");
        }
    }
}
