using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Buildalyzer.Construction;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.Logging;

namespace Buildalyzer.Environment
{
    public class EnvironmentFactory
    {
        private readonly AnalyzerManager _manager;
        private readonly ProjectFile _projectFile;
        private readonly ILogger<EnvironmentFactory> _logger;

        internal EnvironmentFactory(AnalyzerManager manager, ProjectFile projectFile)
        {
            _manager = manager;
            _projectFile = projectFile;
            _logger = _manager.LoggerFactory?.CreateLogger<EnvironmentFactory>();
        }

        public BuildEnvironment GetBuildEnvironment() =>
            GetBuildEnvironment(null, null);

        public BuildEnvironment GetBuildEnvironment(string targetFramework) =>
            GetBuildEnvironment(targetFramework, null);

        public BuildEnvironment GetBuildEnvironment(EnvironmentOptions options) =>
            GetBuildEnvironment(null, options);

        public BuildEnvironment GetBuildEnvironment(string targetFramework, EnvironmentOptions options)
        {
            options = options ?? new EnvironmentOptions();
            BuildEnvironment buildEnvironment;

            // Use the .NET Framework if that's the preference
            // ...or if this project file uses a target known to require .NET Framework
            // ...or if this project ONLY targets .NET Framework ("net" followed by a digit)
            if (options.Preference == EnvironmentPreference.Framework
                || _projectFile.RequiresNetFramework
                || (_projectFile.UsesSdk && OnlyTargetsFramework(targetFramework)))
            {
                buildEnvironment = CreateFrameworkEnvironment(options) ?? CreateCoreEnvironment(options);
            }
            else
            {
                // Otherwise, use a Core environment if it can be found
                buildEnvironment = CreateCoreEnvironment(options) ?? CreateFrameworkEnvironment(options);
            }

            return buildEnvironment ?? throw new InvalidOperationException("Could not find build environment");
        }

        // Based on code from OmniSharp
        // https://github.com/OmniSharp/omnisharp-roslyn/blob/78ccc8b4376c73da282a600ac6fb10fce8620b52/src/OmniSharp.Abstractions/Services/DotNetCliService.cs
        private BuildEnvironment CreateCoreEnvironment(EnvironmentOptions options)
        {
            // Get paths
            DotnetPathResolver pathResolver = new DotnetPathResolver(_manager.LoggerFactory);
            string dotnetPath = pathResolver.ResolvePath(_projectFile.Path, options.DotnetExePath);
            if (dotnetPath == null)
            {
                return null;
            }

            string msBuildExePath = Path.Combine(dotnetPath, "MSBuild.dll");
            if (options != null && options.EnvironmentVariables.ContainsKey(EnvironmentVariables.MSBUILD_EXE_PATH))
            {
                msBuildExePath = options.EnvironmentVariables[EnvironmentVariables.MSBUILD_EXE_PATH];
            }

            // Clone the options global properties dictionary so we can add to it
            Dictionary<string, string> additionalGlobalProperties = new Dictionary<string, string>(options.GlobalProperties);

            // Required to force CoreCompile target when it calculates everything is already built
            // This can happen if the file wasn't previously generated (Clean only cleans what was in that file)
            if (options.TargetsToBuild.Contains("Clean", StringComparer.OrdinalIgnoreCase))
            {
                additionalGlobalProperties.Add(MsBuildProperties.NonExistentFile, Path.Combine("__NonExistentSubDir__", "__NonExistentFile__"));
            }

            // Clone the options global properties dictionary so we can add to it
            Dictionary<string, string> additionalEnvironmentVariables = new Dictionary<string, string>(options.EnvironmentVariables);

            // (Re)set the enviornment variables that dotnet sets
            // See https://github.com/dotnet/cli/blob/0a4ad813ff971f549d34ac4ebc6c8cca9a741c36/src/Microsoft.DotNet.Cli.Utils/MSBuildForwardingAppWithoutLogging.cs#L22-L28
            // Especially important if a global.json is used because dotnet may set these to the latest, but then we'll call a msbuild.dll for the global.json version
            if (!additionalEnvironmentVariables.ContainsKey(EnvironmentVariables.MSBuildExtensionsPath))
            {
                additionalEnvironmentVariables.Add(EnvironmentVariables.MSBuildExtensionsPath, dotnetPath);
            }
            if (!additionalEnvironmentVariables.ContainsKey(EnvironmentVariables.MSBuildSDKsPath))
            {
                additionalEnvironmentVariables.Add(EnvironmentVariables.MSBuildSDKsPath, Path.Combine(dotnetPath, "Sdks"));
            }

            return new BuildEnvironment(
                options.DesignTime,
                options.Restore,
                options.TargetsToBuild.ToArray(),
                msBuildExePath,
                options.DotnetExePath,
                additionalGlobalProperties,
                additionalEnvironmentVariables);
        }

        private BuildEnvironment CreateFrameworkEnvironment(EnvironmentOptions options)
        {
            // Clone the options global properties dictionary so we can add to it
            Dictionary<string, string> additionalGlobalProperties = new Dictionary<string, string>(options.GlobalProperties);

            // Required to force CoreCompile target when it calculates everything is already built
            // This can happen if the file wasn't previously generated (Clean only cleans what was in that file)
            if (options.TargetsToBuild.Contains("Clean", StringComparer.OrdinalIgnoreCase))
            {
                additionalGlobalProperties.Add(MsBuildProperties.NonExistentFile, Path.Combine("__NonExistentSubDir__", "__NonExistentFile__"));
            }

            string msBuildExePath;
            if(options != null && options.EnvironmentVariables.ContainsKey(EnvironmentVariables.MSBUILD_EXE_PATH))
            {
                msBuildExePath = options.EnvironmentVariables[EnvironmentVariables.MSBUILD_EXE_PATH];
            }
            else if (!GetFrameworkMsBuildExePath(out msBuildExePath))
            {
                _logger?.LogWarning("Couldn't find a .NET Framework MSBuild path");
                return null;
            }

            // This is required to trigger NuGet package resolution and regeneration of project.assets.json
            additionalGlobalProperties.Add(MsBuildProperties.ResolveNuGetPackages, "true");

            return new BuildEnvironment(
                options.DesignTime,
                options.Restore,
                options.TargetsToBuild.ToArray(),
                msBuildExePath,
                options.DotnetExePath,
                additionalGlobalProperties,
                options.EnvironmentVariables);
        }

        private bool GetFrameworkMsBuildExePath(out string msBuildExePath)
        {
            msBuildExePath = ToolLocationHelper.GetPathToBuildToolsFile("msbuild.exe", ToolLocationHelper.CurrentToolsVersion);
            if (string.IsNullOrEmpty(msBuildExePath))
            {
                // Could not find the tools path, possibly due to https://github.com/Microsoft/msbuild/issues/2369
                // Try to poll for it. From https://github.com/KirillOsenkov/MSBuildStructuredLog/blob/4649f55f900a324421bad5a714a2584926a02138/src/StructuredLogViewer/MSBuildLocator.cs
                string programFilesX86 = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86);
                msBuildExePath = new[]
                {
                    Path.Combine(programFilesX86, "Microsoft Visual Studio", "2017", "Enterprise", "MSBuild", "15.0", "Bin", "MSBuild.exe"),
                    Path.Combine(programFilesX86, "Microsoft Visual Studio", "2017", "Professional", "MSBuild", "15.0", "Bin", "MSBuild.exe"),
                    Path.Combine(programFilesX86, "Microsoft Visual Studio", "2017", "Community", "MSBuild", "15.0", "Bin", "MSBuild.exe")
                }
                .Where(File.Exists)
                .FirstOrDefault();
            }
            if (string.IsNullOrEmpty(msBuildExePath))
            {
                return false;
            }
            return true;
        }

        private bool OnlyTargetsFramework(string targetFramework) =>
            targetFramework == null ? _projectFile.TargetFrameworks.All(x => IsFrameworkTargetFramework(x)) : IsFrameworkTargetFramework(targetFramework);

        private bool IsFrameworkTargetFramework(string targetFramework) =>
            targetFramework.StartsWith("net", StringComparison.OrdinalIgnoreCase)
                && targetFramework.Length > 3
                && char.IsDigit(targetFramework[4]);
    }
}