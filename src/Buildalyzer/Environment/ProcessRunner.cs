using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace Buildalyzer.Environment
{
    internal class ProcessRunner : IDisposable
    {
        private readonly ILogger<ProcessRunner> _logger;
        
        public List<string> Output { get; private set; }
        public List<string> Error { get; private set; }
        public int ExitCode => Process.ExitCode;

        private Process Process { get; }

        public Action Exited { get; set; }

        public ProcessRunner(
            string fileName,
            string arguments,
            string workingDirectory,
            Dictionary<string, string> environmentVariables,
            ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory?.CreateLogger<ProcessRunner>();
            Process = new Process
            {
                StartInfo =
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            // Create the process info

            // Copy over environment variables
            if(environmentVariables != null)
            {
                foreach(KeyValuePair<string, string> variable in environmentVariables)
                {
                    Process.StartInfo.Environment[variable.Key] = variable.Value;
                    Process.StartInfo.EnvironmentVariables[variable.Key] = variable.Value;
                }
            }

            Process.EnableRaisingEvents = true;  // Raises Process.Exited immediately instead of when checked via .WaitForExit() or .HasExited
            Process.Exited += ProcessExited;
        }
        
        public ProcessRunner Start()
        {
            Process.Start();
            _logger?.LogDebug($"{System.Environment.NewLine}Started process {Process.Id}: \"{Process.StartInfo.FileName}\" {Process.StartInfo.Arguments}{System.Environment.NewLine}");
            return this;
        }

        private void ProcessExited(object sender, EventArgs e)
        {
            Exited?.Invoke();
            _logger?.LogDebug($"Process {Process.Id} exited with code {Process.ExitCode}{System.Environment.NewLine}{System.Environment.NewLine}");
        }

        public void WaitForExit()
        {
            Process.WaitForExit();
            ProcessOutput();
        }

        public bool WaitForExit(int timeout)
        {
            bool exited = Process.WaitForExit(timeout);
            ProcessOutput();
            return exited;
        }

        private void ProcessOutput()
        {
            Output = Process.StandardOutput.ReadToEnd().Split('\n').Select(l => l.Trim()).ToList();
            Error = Process.StandardError.ReadToEnd().Split('\n').Select(l => l.Trim()).ToList();
        }

        public void Dispose()
        {
            Process.Exited -= ProcessExited;
            Process.Close();
        }                
    }
}
