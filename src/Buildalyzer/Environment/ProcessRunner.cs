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
        public static int Run(string fileName, string arguments, string workingDirectory, Dictionary<string, string> environmentVariables, ILogger logger, List<string> lines = null)
        {
            using (environmentVariables == null ? (IDisposable)new EmptyDisposable() : new TemporaryEnvironment(environmentVariables))
            {
                // Create the process info
                Process process = new Process();
                process.StartInfo.FileName = fileName;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.WorkingDirectory = workingDirectory;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                
                // Capture output
                if (lines != null)
                {
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.OutputDataReceived += (s, e) => lines.Add(e.Data);
                    process.ErrorDataReceived += (s, e) => lines.Add(e.Data);
                }

                // Execute the process
                if(logger != null)
                {
                    logger.LogDebug($"Starting process {fileName} {arguments}");
                }
                process.Start();
                process.BeginOutputReadLine();
                Stopwatch sw = new Stopwatch();
                sw.Start();
                while (!process.HasExited)
                {
                    Thread.Sleep(100);
                    if (sw.ElapsedMilliseconds > 4000)
                    {
                        break;
                    }
                }
                sw.Stop();
                process.Close();       
                
                // Log the results
                if(lines != null && logger != null)
                {
                    foreach(string line in lines)
                    {
                        logger.LogDebug($"{line}{System.Environment.NewLine}");
                    }
                    logger.LogDebug(System.Environment.NewLine);
                }

                return process.ExitCode;
            }
        }
    }
}
