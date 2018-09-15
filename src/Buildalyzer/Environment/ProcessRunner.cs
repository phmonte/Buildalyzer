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
        private readonly ILogger _logger;
        private readonly List<string> _outputLines;

        public Process Process { get; }

        public ProcessRunner(
            string fileName,
            string arguments,
            string workingDirectory,
            Dictionary<string, string> environmentVariables,
            ILogger logger,
            List<string> outputLines = null)
        {
            _logger = logger;
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
            if (logger != null || outputLines != null)
            {
                Process.StartInfo.RedirectStandardOutput = true;
                Process.StartInfo.RedirectStandardError = true;
                Process.OutputDataReceived += DataReceived;
                Process.ErrorDataReceived += DataReceived;
            }

            Process.Exited += Exited;
        }
        
        public ProcessRunner Start()
        {
            Process.Start();
            _logger?.LogDebug($"{System.Environment.NewLine}Started process {Process.Id}: \"{Process.StartInfo.FileName}\" {Process.StartInfo.Arguments}{System.Environment.NewLine}");
            if (_logger != null || _outputLines != null)
            {
                Process.BeginOutputReadLine();
            }
            return this;
        }

        public int PollForExit() => PollForExit(0);

        public int PollForExit(int timeout)
        {
            // Wait for exit
            Stopwatch sw = new Stopwatch();
            sw.Start();
            while (!Process.HasExited)
            {
                Thread.Sleep(100);
                if (timeout > 0 && sw.ElapsedMilliseconds > timeout)
                {
                    _logger?.LogDebug($"Process timeout, killing process {Process.Id}{System.Environment.NewLine}");
                    Process.Kill();
                    break;
                }
            }
            sw.Stop();

            // Clean up
            if (_logger != null || _outputLines != null)
            {
                Process.OutputDataReceived -= DataReceived;
                Process.ErrorDataReceived -= DataReceived;
            }

            return Process.ExitCode;
        }

        private void Exited(object sender, EventArgs e)
        {
            _logger?.LogDebug($"Process {Process.Id} exited with code {Process.ExitCode}{System.Environment.NewLine}{System.Environment.NewLine}");
        }

        public void Dispose()
        {
            Process.Close();
        }        

        private void DataReceived(object sender, DataReceivedEventArgs e)
        {
            _outputLines?.Add(e.Data);
            _logger?.LogDebug($"{e.Data}{System.Environment.NewLine}");
        }
    }
}
