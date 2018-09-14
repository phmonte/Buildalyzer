using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace Buildalyzer.Environment
{
    internal class ProcessRunner
    {
        private readonly ILogger _logger;
        private readonly List<string> _outputLines;
        private readonly int _timeout;

        public ProcessRunner(ILogger logger, List<string> outputLines, int timeout = 0)
        {
            _logger = logger;
            _outputLines = outputLines;
            _timeout = timeout;
        }

        public int Run(string fileName, string arguments, string workingDirectory, Dictionary<string, string> environmentVariables, Action waitAction = null)
        {
            using (environmentVariables == null ? (IDisposable)new EmptyDisposable() : new TemporaryEnvironment(environmentVariables))
            {
                Process process = new Process();
                try
                {
                    // Create the process info
                    process.StartInfo.FileName = fileName;
                    process.StartInfo.Arguments = arguments;
                    process.StartInfo.WorkingDirectory = workingDirectory;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.UseShellExecute = false;

                    // Capture output
                    if (_logger != null || _outputLines != null)
                    {
                        process.StartInfo.RedirectStandardOutput = true;
                        process.StartInfo.RedirectStandardError = true;
                        process.OutputDataReceived += DataReceived;
                        process.ErrorDataReceived += DataReceived;
                    }

                    // Execute the process
                    process.Start();
                    _logger?.LogDebug($"{System.Environment.NewLine}Started process {process.Id}: {fileName} {arguments}{System.Environment.NewLine}");
                    if (_logger != null || _outputLines != null)
                    {
                        process.BeginOutputReadLine();
                    }
                    Stopwatch sw = new Stopwatch();
                    sw.Start();
                    while (!process.HasExited)
                    {
                        if (waitAction != null)
                        {
                            waitAction();
                        }
                        else
                        {
                            Thread.Sleep(100);
                        }
                        if (_timeout > 0 && sw.ElapsedMilliseconds > _timeout)
                        {
                            _logger?.LogDebug($"Process timeout, killing process {process.Id}{System.Environment.NewLine}");
                            process.Kill();
                            break;
                        }
                    }
                    sw.Stop();
                    int exitCode = process.ExitCode;
                    if (_logger != null || _outputLines != null)
                    {
                        process.OutputDataReceived -= DataReceived;
                        process.ErrorDataReceived -= DataReceived;
                    }
                    _logger?.LogDebug($"Process {process.Id} exited with code {exitCode}{System.Environment.NewLine}{System.Environment.NewLine}");
                    return exitCode;
                }
                finally
                {
                    process.Close();
                }
            }
        }

        private void DataReceived(object sender, DataReceivedEventArgs e)
        {
            _outputLines?.Add(e.Data);
            _logger?.LogDebug($"{e.Data}{System.Environment.NewLine}");
        }
    }
}
