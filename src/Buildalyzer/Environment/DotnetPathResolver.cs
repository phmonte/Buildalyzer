using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Buildalyzer.Environment
{
    internal static class DotnetPathResolver
    {
        // Don't cache the result because it might change project to project due to global.json
        public static string ResolvePath(string projectPath, ILogger logger)
        {
            List<string> lines = GetInfo(projectPath, logger);
            if (lines.Count == 0)
            {
                // Need to rety calling "dotnet --info" and do a hacky timeout for the process otherwise it occasionally locks up during testing (and possibly in the field)
                int retry = 0;
                do
                {
                    Thread.Sleep(500);
                    lines = GetInfo(projectPath, logger);
                    retry++;
                } while (lines.Count == 0 && retry < 5);
            }

            // Did we get any output?
            if (lines == null || lines.Count == 0)
            {
                logger.LogWarning("Could not get results from `dotnet --info` call");
                return null;
            }
            
            // Try to get a path
            string basePath = ParseBasePath(lines) ?? ParseInstalledSdksPath(lines);
            if(string.IsNullOrWhiteSpace(basePath))
            {
                logger.LogWarning("Could not locate SDK path in `dotnet --info` results");
                return null;
            }

            return basePath;
        }

        private static List<string> GetInfo(string projectPath, ILogger logger)
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

            // global.json may change the version, so need to set working directory
            List<string> lines = new List<string>();
            new ProcessRunner(logger, lines, 4000).Run("dotnet", "--info", Path.GetDirectoryName(projectPath), environmentVariables);
            return lines;
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