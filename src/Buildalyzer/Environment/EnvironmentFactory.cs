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

        internal EnvironmentFactory(AnalyzerManager manager, ProjectFile projectFile)
        {
            _manager = manager;
            _projectFile = projectFile;
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
            // Clone the options global properties dictionary so we can add to it
            Dictionary<string, string> additionalGlobalProperties = options.GlobalProperties.ToDictionary(x => x.Key, x => x.Value);

            // Tweak targets
            List<string> targets = new List<string>(options.TargetsToBuild);
            if (targets.Contains("Restore", StringComparer.OrdinalIgnoreCase) && _projectFile.Virtual)
            {
                // NuGet.Targets can't handle virtual project files:
                // C:\Program Files\dotnet\sdk\2.1.300\NuGet.targets(239,5): error MSB3202: The project file "E:\Code\...\...csproj" was not found.
                targets.RemoveAll(x => x.Equals("Restore", StringComparison.OrdinalIgnoreCase));
            }
            if (targets.Contains("Clean", StringComparer.OrdinalIgnoreCase))
            {
                // Required to force CoreCompile target when it calculates everything is already built
                // This can happen if the file wasn't previously generated (Clean only cleans what was in that file)
                additionalGlobalProperties.Add(MsBuildProperties.NonExistentFile, Path.Combine("__NonExistentSubDir__", "__NonExistentFile__"));
            }

            // Get paths
            string dotnetPath = DotnetPathResolver.ResolvePath(_projectFile.Path, _manager.ProjectLogger);
            if(dotnetPath == null)
            {
                return null;
            }
            string msBuildExePath = Path.Combine(dotnetPath, "MSBuild.dll");

            // Required to find and import the Restore target
            additionalGlobalProperties.Add(MsBuildProperties.NuGetRestoreTargets, Path.Combine(dotnetPath, "NuGet.targets"));

            return new BuildEnvironment(
                options.DesignTime,
                targets.ToArray(),
                msBuildExePath,
                additionalGlobalProperties,
                options.EnvironmentVariables);
        }

        private BuildEnvironment CreateFrameworkEnvironment(EnvironmentOptions options)
        {
            // Clone the options global properties dictionary so we can add to it
            Dictionary<string, string> additionalGlobalProperties = options.GlobalProperties.ToDictionary(x => x.Key, x => x.Value);

            // Tweak targets
            List<string> targets = new List<string>(options.TargetsToBuild);
            if (targets.Contains("Restore", StringComparer.OrdinalIgnoreCase) && _projectFile.Virtual)
            {
                // NuGet.Targets can't handle virtual project files:
                // C:\Program Files\dotnet\sdk\2.1.300\NuGet.targets(239,5): error MSB3202: The project file "E:\Code\...\...csproj" was not found.
                targets.RemoveAll(x => x.Equals("Restore", StringComparison.OrdinalIgnoreCase));
            }
            if (targets.Contains("Clean", StringComparer.OrdinalIgnoreCase))
            {
                // Required to force CoreCompile target when it calculates everything is already built
                // This can happen if the file wasn't previously generated (Clean only cleans what was in that file)
                additionalGlobalProperties.Add(MsBuildProperties.NonExistentFile, Path.Combine("__NonExistentSubDir__", "__NonExistentFile__"));
            }

            if (!GetFrameworkMsBuildExePath(out string msBuildExePath))
            {
                _manager.ProjectLogger.LogWarning("Couldn't find a .NET Framework MSBuild path");
                return null;
            }
            
            // This is required to trigger NuGet package resolution and regeneration of project.assets.json
            additionalGlobalProperties.Add(MsBuildProperties.ResolveNuGetPackages, "true");
            
            return new BuildEnvironment(
                options.DesignTime,
                targets.ToArray(),
                msBuildExePath,
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