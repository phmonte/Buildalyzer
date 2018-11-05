using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Buildalyzer.Environment
{
    internal class DotnetPathResolver
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<DotnetPathResolver> _logger;

        public DotnetPathResolver(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory?.CreateLogger<DotnetPathResolver>();
        }
        
        // Don't cache the result because it might change project to project due to global.json
        public string ResolvePath(string projectPath, string dotnetExePath)
        {
            dotnetExePath = dotnetExePath ?? "dotnet";
            List<string> output = GetInfo(projectPath, dotnetExePath);

            // Did we get any output?
            if (output == null || output.Count == 0)
            {
                _logger?.LogWarning($"Could not get results from `{ dotnetExePath } --info` call");
                return null;
            }
            
            // Try to get a path
            string basePath = ParseBasePath(output) ?? ParseInstalledSdksPath(output);
            if(string.IsNullOrWhiteSpace(basePath))
            {
                _logger?.LogWarning($"Could not locate SDK path in `{ dotnetExePath } --info` results");
                return null;
            }

            return basePath;
        }

        private List<string> GetInfo(string projectPath, string dotnetExePath)
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
            using (ProcessRunner processRunner = new ProcessRunner(dotnetExePath, "--info", Path.GetDirectoryName(projectPath), environmentVariables, _loggerFactory))
            {
                processRunner.Start();
                processRunner.WaitForExit(4000);
                return processRunner.Output;
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