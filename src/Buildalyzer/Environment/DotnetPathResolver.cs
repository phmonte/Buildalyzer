using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace Buildalyzer.Environment
{
    internal static class DotnetPathResolver
    {
        private static readonly object BasePathLock = new object();
        private static string BasePath = null;

        public static string ResolvePath(string projectPath)
        {
            lock(BasePathLock)
            {
                if(BasePath != null)
                {
                    return BasePath;
                }

                // Need to rety calling "dotnet --info" and do a hacky timeout for the process otherwise it occasionally locks up during testing (and possibly in the field)
                List<string> lines = GetInfo(projectPath);
                int retry = 0;
                do
                {
                    if(retry == 0)
                    {
                        Thread.Sleep(500);
                    }
                    lines = GetInfo(projectPath);
                    retry++;
                } while ((lines == null || lines.Count == 0) && retry < 5);

                // Did we get any output?
                if (lines == null || lines.Count == 0)
                {
                    throw new InvalidOperationException("Could not get results from `dotnet --info` call");
                }

                // Try to get a path
                BasePath = ParseBasePath(lines) ?? ParseInstalledSdksPath(lines);
                if(string.IsNullOrWhiteSpace(BasePath))
                {
                    throw new InvalidOperationException("Could not locate SDK path in `dotnet --info` results");
                }

                return BasePath;
            }
        }

        private static List<string> GetInfo(string projectPath)
        {
            // Ensure that we set the DOTNET_CLI_UI_LANGUAGE environment variable to "en-US" before
            // running 'dotnet --info'. Otherwise, we may get localized results
            // Also unset some MSBuild variables, see https://github.com/OmniSharp/omnisharp-roslyn/blob/df160f86ce906bc566fe3e04e38a4077bd6023b4/src/OmniSharp.Abstractions/Services/DotNetCliService.cs#L36
            Dictionary<string, string> environmentVariables = new Dictionary<string, string>
            {
                { EnvironmentVariables.DOTNET_CLI_UI_LANGUAGE, "en-US" },
                { EnvironmentVariables.MSBUILD_EXE_PATH, null },
                { MsBuildProperties.MSBuildExtensionsPath, null }
            };

            using (new TemporaryEnvironment(environmentVariables))
            {               
                // Create the process info
                Process process = new Process();
                process.StartInfo.FileName = "dotnet";
                process.StartInfo.Arguments = "--info";
                process.StartInfo.WorkingDirectory = Path.GetDirectoryName(projectPath); // global.json may change the version, so need to set working directory
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;

                // Capture output
                List<string> lines = new List<string>();
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.OutputDataReceived += (s, e) => lines.Add(e.Data);
                process.ErrorDataReceived += (s, e) => lines.Add(e.Data);

                // Execute the process
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
                return lines;
            }
        }

        // Try to find a base path
        internal static string ParseBasePath(List<string> lines)
        {
            foreach (string line in lines.Where(x => x != null))
            {
                int colonIndex = line.IndexOf(':');
                if (colonIndex >= 0
                    && line.Substring(0, colonIndex).Trim().Equals("Base Path", StringComparison.OrdinalIgnoreCase))
                {
                    string basePath = line.Substring(colonIndex + 1).Trim();

                    // Make sure the base path matches the runtime architecture if on Windows
                    // Note that this only works for the default installation locations under "Program Files"
                    if (basePath.Contains(@"\Program Files\") && !System.Environment.Is64BitProcess)
                    {
                        string newBasePath = basePath.Replace(@"\Program Files\", @"\Program Files (x86)\");
                        if (Directory.Exists(newBasePath))
                        {
                            basePath = newBasePath;
                        }
                    }
                    else if (basePath.Contains(@"\Program Files (x86)\") && System.Environment.Is64BitProcess)
                    {
                        string newBasePath = basePath.Replace(@"\Program Files (x86)\", @"\Program Files\");
                        if (Directory.Exists(newBasePath))
                        {
                            basePath = newBasePath;
                        }
                    }

                    return basePath;
                }
            }
            return null;
        }

        // Fallback if a base path couldn't be found (I.e., global.json version is not available)
        internal static string ParseInstalledSdksPath(List<string> lines)
        {
            int index = lines.IndexOf(".NET Core SDKs installed:");
            if (index == -1)
            {
                return null;
            }
            index++;
            while(!string.IsNullOrWhiteSpace(lines[index + 1]))
            {
                index++;
            }
            string[] segments = lines[index]
                .Split(new[] { '[', ']' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToArray();
            return $@"{segments[1]}{Path.DirectorySeparatorChar}{segments[0]}{Path.DirectorySeparatorChar}";
        }
    }
}