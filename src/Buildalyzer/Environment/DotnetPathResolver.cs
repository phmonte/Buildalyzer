using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Buildalyzer.Environment
{
    internal static class DotnetPathResolver
    {
        const string DOTNET_CLI_UI_LANGUAGE = nameof(DOTNET_CLI_UI_LANGUAGE);

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
                    lines = GetInfo(projectPath);
                    retry++;
                } while ((lines == null || lines.Count == 0) && retry < 5);
                BasePath = ParseBasePath(lines);

                return BasePath;
            }
        }

        private static List<string> GetInfo(string projectPath)
        {
            // Ensure that we set the DOTNET_CLI_UI_LANGUAGE environment variable to "en-US" before
            // running 'dotnet --info'. Otherwise, we may get localized results.
            string originalCliLanguage = System.Environment.GetEnvironmentVariable(DOTNET_CLI_UI_LANGUAGE);
            System.Environment.SetEnvironmentVariable(DOTNET_CLI_UI_LANGUAGE, "en-US");

            try
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
                process.OutputDataReceived += (s, e) => lines.Add(e.Data);

                // Execute the process
                process.Start();
                process.BeginOutputReadLine();
                Stopwatch sw = new Stopwatch();
                sw.Start();
                while (!process.HasExited)
                {
                    if (sw.ElapsedMilliseconds > 4000)
                    {
                        break;
                    }
                }
                sw.Stop();
                process.Close();
                return lines;
            }
            finally
            {
                System.Environment.SetEnvironmentVariable(DOTNET_CLI_UI_LANGUAGE, originalCliLanguage);
            }
        }

        private static string ParseBasePath(List<string> lines)
        {
            if (lines == null || lines.Count == 0)
            {
                throw new InvalidOperationException("Could not get results from `dotnet --info` call");
            }

            foreach (string line in lines)
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

            throw new InvalidOperationException("Could not locate base path in `dotnet --info` results");
        }
    }
}