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
        private readonly int _timeout;
        private readonly Process _process;

        //public ProcessRunner(ILogger logger, List<string> outputLines, int timeout = 0)
        //{
        //    _logger = logger;
        //    _outputLines = outputLines;
        //    _timeout = timeout;
        //}

        public ProcessRunner(
            string fileName,
            string arguments,
            string workingDirectory,
            ILogger logger,
            List<string> outputLines = null,
            int timeout = 0)
        {
            _logger = logger;
            _timeout = timeout;
            _process = new Process();

            // Create the process info
            _process.StartInfo.FileName = fileName;
            _process.StartInfo.Arguments = arguments;
            _process.StartInfo.WorkingDirectory = workingDirectory;
            _process.StartInfo.CreateNoWindow = true;
            _process.StartInfo.UseShellExecute = false;

            // Capture output
            if (logger != null || outputLines != null)
            {
                _process.StartInfo.RedirectStandardOutput = true;
                _process.StartInfo.RedirectStandardError = true;
                _process.OutputDataReceived += DataReceived;
                _process.ErrorDataReceived += DataReceived;
            }
        }

        public ProcessRunner Start(Dictionary<string, string> environmentVariables)
        {
            using (environmentVariables == null ? (IDisposable)new EmptyDisposable() : new TemporaryEnvironment(environmentVariables))
            {
                _process.Start();
                _logger?.LogDebug($"{System.Environment.NewLine}Started process {_process.Id}: \"{_process.StartInfo.FileName}\" {_process.StartInfo.Arguments}{System.Environment.NewLine}");
                if (_logger != null || _outputLines != null)
                {
                    _process.BeginOutputReadLine();
                }
                return this;
            }
        }

        public int WaitForExit()
        {
            // Wait for exit
            Stopwatch sw = new Stopwatch();
            sw.Start();
            while (!_process.HasExited)
            {
                Thread.Sleep(100);
                if (_timeout > 0 && sw.ElapsedMilliseconds > _timeout)
                {
                    _logger?.LogDebug($"Process timeout, killing process {_process.Id}{System.Environment.NewLine}");
                    _process.Kill();
                    break;
                }
            }
            sw.Stop();
            int exitCode = _process.ExitCode;

            // Clean up
            if (_logger != null || _outputLines != null)
            {
                _process.OutputDataReceived -= DataReceived;
                _process.ErrorDataReceived -= DataReceived;
            }
            _logger?.LogDebug($"Process {_process.Id} exited with code {exitCode}{System.Environment.NewLine}{System.Environment.NewLine}");
            return exitCode;
        }

        public void Dispose()
        {
            _process.Close();
        }        

        private void DataReceived(object sender, DataReceivedEventArgs e)
        {
            _outputLines?.Add(e.Data);
            _logger?.LogDebug($"{e.Data}{System.Environment.NewLine}");
        }
    }
}
