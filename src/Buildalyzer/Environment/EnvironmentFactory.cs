using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Utilities;

namespace Buildalyzer.Environment
{
    internal class EnvironmentFactory
    {
        private readonly AnalyzerManager _manager;
        private readonly ProjectFile _projectFile;
        private readonly EnvironmentOptions _options;

        public EnvironmentFactory(AnalyzerManager manager, ProjectFile projectFile, EnvironmentOptions options)
        {
            _manager = manager;
            _projectFile = projectFile;
            _options = options ?? new EnvironmentOptions();
        }

        public BuildEnvironment GetBuildEnvironment(string targetFramework)
        {                
            // If we're running on .NET Core, use the .NET Core SDK regardless of the project file
            if (BuildEnvironment.IsRunningOnCore)
            {
                return CreateCoreEnvironment();
            }

            // If this is an SDK project, check the target framework
            if (_projectFile.UsesSdk)
            {
                // Use the Framework tools if this project targets .NET Framework ("net" followed by a digit)
                // (see https://docs.microsoft.com/en-us/dotnet/standard/frameworks)
                if (targetFramework != null
                    && targetFramework.StartsWith("net", StringComparison.OrdinalIgnoreCase)
                    && targetFramework.Length > 3
                    && char.IsDigit(targetFramework[4]))
                {
                    return CreateFrameworkEnvironment();
                }

                // Otherwise use the .NET Core SDK
                return CreateCoreEnvironment();
            }

            // Use Framework tools if a ToolsVersion attribute
            if (_projectFile.ToolsVersion != null)
            {
                return CreateFrameworkEnvironment();
            }

            throw new Exception("Could not determine build environment");
        }

        // Based on code from OmniSharp
        // https://github.com/OmniSharp/omnisharp-roslyn/blob/78ccc8b4376c73da282a600ac6fb10fce8620b52/src/OmniSharp.Abstractions/Services/DotNetCliService.cs
        private BuildEnvironment CreateCoreEnvironment()
        {
            Dictionary<string, string> additionalGlobalProperties = new Dictionary<string, string>();

            // Get targets
            List<string> targets = new List<string>();
            if (_options.RestoreTarget && !_projectFile.Virtual)
            {
                // NuGet.Targets can't handle virtual project files:
                // C:\Program Files\dotnet\sdk\2.1.300\NuGet.targets(239,5): error MSB3202: The project file "E:\Code\...\...csproj" was not found.

                targets.Add("Restore");
            }
            if (_options.CleanTarget)
            {
                targets.Add("Clean");

                // Required to force CoreCompile target when it calculates everything is already built
                // This can happen if the file wasn't previously generated (Clean only cleans what was in that file)
                additionalGlobalProperties.Add(MsBuildProperties.NonExistentFile, @"__NonExistentSubDir__\__NonExistentFile__");
            }
            if (_options.BuildTarget)
            {
                targets.Add("Build");
            }

            // Get paths
            string dotnetPath = DotnetPathResolver.ResolvePath(_projectFile.Path);
            string msBuildExePath = Path.Combine(dotnetPath, "MSBuild.dll");
            string sdksPath = Path.Combine(dotnetPath, "Sdks");
            string roslynTargetsPath = Path.Combine(dotnetPath, "Roslyn");

            // Required to find and import the Restore target
            additionalGlobalProperties.Add(MsBuildProperties.NuGetRestoreTargets, $@"{ dotnetPath }\NuGet.targets");

            return new BuildEnvironment(
                _options.DesignTime,
                targets.ToArray(),
                msBuildExePath,
                dotnetPath,
                sdksPath,
                roslynTargetsPath,
                additionalGlobalProperties);
        }

        private BuildEnvironment CreateFrameworkEnvironment()
        {
            Dictionary<string, string> additionalGlobalProperties = new Dictionary<string, string>();

            // Get targets
            List<string> targets = new List<string>();
            if (_options.RestoreTarget && _projectFile.UsesSdk && !_projectFile.Virtual)
            {
                // NuGet.Targets can't handle virtual project files:
                // C:\Program Files\dotnet\sdk\2.1.300\NuGet.targets(239,5): error MSB3202: The project file "E:\Code\...\...csproj" was not found.

                targets.Add("Restore");
            }
            if (_options.CleanTarget)
            {
                targets.Add("Clean");

                // Required to force CoreCompile target when it calculates everything is already built
                // This can happen if the file wasn't previously generated (Clean only cleans what was in that file)
                additionalGlobalProperties.Add(MsBuildProperties.NonExistentFile, @"__NonExistentSubDir__\__NonExistentFile__");
            }
            if (_options.BuildTarget)
            {
                targets.Add("Build");
            }

            // Get paths
            string msBuildExePath = ToolLocationHelper.GetPathToBuildToolsFile("msbuild.exe", ToolLocationHelper.CurrentToolsVersion);
            if (string.IsNullOrEmpty(msBuildExePath))
            {
                // Could not find the tools path, possibly due to https://github.com/Microsoft/msbuild/issues/2369
                // Try to poll for it. From https://github.com/KirillOsenkov/MSBuildStructuredLog/blob/4649f55f900a324421bad5a714a2584926a02138/src/StructuredLogViewer/MSBuildLocator.cs
                string programFilesX86 = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86);
                msBuildExePath = new[]
                {
                    Path.Combine(programFilesX86, @"Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\Bin\MSBuild.exe"),
                    Path.Combine(programFilesX86, @"Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\MSBuild.exe"),
                    Path.Combine(programFilesX86, @"Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe")
                }
                .Where(File.Exists)
                .FirstOrDefault();
            }
            if (string.IsNullOrEmpty(msBuildExePath))
            {
                throw new InvalidOperationException("Could not locate the tools (msbuild.exe) path");
            }

            string toolsPath = Path.GetDirectoryName(msBuildExePath);
            string extensionsPath = Path.GetFullPath(Path.Combine(toolsPath, @"..\..\"));
            string sdksPath = Path.Combine(_projectFile.UsesSdk ? DotnetPathResolver.ResolvePath(_projectFile.Path) : extensionsPath, "Sdks");
            string roslynTargetsPath = Path.Combine(toolsPath, "Roslyn");
            
            // Need to set directories for default code analysis rulset (see https://github.com/dotnet/roslyn/issues/6774)
            string vsRoot = Path.Combine(extensionsPath, @"..\");
            additionalGlobalProperties.Add(MsBuildProperties.CodeAnalysisRuleDirectories, Path.GetFullPath(Path.Combine(vsRoot, @"Team Tools\Static Analysis Tools\FxCop\\Rules")));
            additionalGlobalProperties.Add(MsBuildProperties.CodeAnalysisRuleSetDirectories, Path.GetFullPath(Path.Combine(vsRoot, @"Team Tools\Static Analysis Tools\\Rule Sets")));

            // This is required to trigger NuGet package resolution and regeneration of project.assets.json
            additionalGlobalProperties.Add(MsBuildProperties.ResolveNuGetPackages, "true");

            // Required to find and import the Restore target
            additionalGlobalProperties.Add(MsBuildProperties.NuGetRestoreTargets, $@"{ toolsPath }\..\..\..\Common7\IDE\CommonExtensions\Microsoft\NuGet\NuGet.targets");

            return new BuildEnvironment(
                _options.DesignTime,
                targets.ToArray(),
                msBuildExePath,
                extensionsPath,
                sdksPath,
                roslynTargetsPath,
                additionalGlobalProperties);
        }
    }
}