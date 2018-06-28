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

        public EnvironmentFactory(AnalyzerManager manager, ProjectFile projectFile)
        {
            _manager = manager;
            _projectFile = projectFile;
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
            // Get targets
            List<string> targets = new List<string>();
            if (!_projectFile.Virtual)
            {
                // NuGet.Targets can't handle virtual project files:
                // C:\Program Files\dotnet\sdk\2.1.300\NuGet.targets(239,5): error MSB3202: The project file "E:\Code\...\...csproj" was not found.
                targets.Add("Restore");
            }
            if (_manager.CleanBeforeCompile)
            {
                targets.Add("Clean");
            }
            targets.Add("Compile");

            // Get paths
            string dotnetPath = DotnetPathResolver.ResolvePath(_projectFile.Path);
            string msBuildExePath = Path.Combine(dotnetPath, "MSBuild.dll");
            string sdksPath = Path.Combine(dotnetPath, "Sdks");
            string roslynTargetsPath = Path.Combine(dotnetPath, "Roslyn");

            // Additional global properties
            Dictionary<string, string> additionalGlobalProperties = new Dictionary<string, string>
            {
                // Required to find and import the Restore target
                { MsBuildProperties.NuGetRestoreTargets, $@"{ dotnetPath }\NuGet.targets" }
            };

            return new BuildEnvironment(
                targets.ToArray(),
                msBuildExePath,
                dotnetPath,
                sdksPath,
                roslynTargetsPath,
                additionalGlobalProperties);
        }

        private BuildEnvironment CreateFrameworkEnvironment()
        {
            // Get targets
            List<string> targets = new List<string>();
            if (_projectFile.UsesSdk && !_projectFile.Virtual)
            {
                // NuGet.Targets can't handle virtual project files:
                // C:\Program Files\dotnet\sdk\2.1.300\NuGet.targets(239,5): error MSB3202: The project file "E:\Code\...\...csproj" was not found.
                targets.Add("Restore");
            }
            if (_manager.CleanBeforeCompile)
            {
                targets.Add("Clean");
            }
            targets.Add("Compile");

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

            // Additional global properties
            string vsRoot = Path.Combine(extensionsPath, @"..\");
            Dictionary<string, string> additionalGlobalProperties = new Dictionary<string, string>
            {
                // Need to set directories for default code analysis rulset (see https://github.com/dotnet/roslyn/issues/6774)
                { MsBuildProperties.CodeAnalysisRuleDirectories, Path.GetFullPath(Path.Combine(vsRoot, @"Team Tools\Static Analysis Tools\FxCop\\Rules")) },
                { MsBuildProperties.CodeAnalysisRuleSetDirectories, Path.GetFullPath(Path.Combine(vsRoot, @"Team Tools\Static Analysis Tools\\Rule Sets")) },
                
                // This is required to trigger NuGet package resolution and regeneration of project.assets.json
                { MsBuildProperties.ResolveNuGetPackages, "true" },

                // Required to find and import the Restore target
                { MsBuildProperties.NuGetRestoreTargets, $@"{ toolsPath }\..\..\..\Common7\IDE\CommonExtensions\Microsoft\NuGet\NuGet.targets" }
            };

            return new BuildEnvironment(
                targets.ToArray(),
                msBuildExePath,
                extensionsPath,
                sdksPath,
                roslynTargetsPath,
                additionalGlobalProperties);
        }
    }
}