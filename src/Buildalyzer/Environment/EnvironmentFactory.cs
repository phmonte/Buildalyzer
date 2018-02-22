using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Utilities;

namespace Buildalyzer.Environment
{
    internal abstract class EnvironmentFactory
    {
        public static BuildEnvironment GetBuildEnvironment(string projectPath, XDocument projectDocument)
        {
            // If we're running on .NET Core, use the .NET Core SDK regardless of the project file
            if (System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
                .Replace(" ", "").StartsWith(".NETCore", StringComparison.OrdinalIgnoreCase))
            {
                return CreateCoreEnvironment(projectPath);
            }

            // Look at the project file to determine
            XElement projectElement = projectDocument.GetDescendants("Project").FirstOrDefault();
            if (projectElement != null)
            {
                // Does this project use the SDK?
                // Check for an SDK attribute on the project element
                // If no <Project> attribute, check for a SDK import (see https://github.com/Microsoft/msbuild/issues/1493)
                if (projectElement.GetAttributeValue("Sdk") != null
                    || projectElement.GetDescendants("Import").Any(x => x.GetAttributeValue("Sdk") != null))
                {
                    // Use the Framework tools if this project targets .NET Framework ("net" followed by a digit)
                    // https://docs.microsoft.com/en-us/dotnet/standard/frameworks
                    string targetFramework = projectElement.GetDescendants("TargetFramework").FirstOrDefault()?.Value;
                    if(targetFramework != null
                        && targetFramework.StartsWith("net", StringComparison.OrdinalIgnoreCase)
                        && targetFramework.Length > 3
                        && char.IsDigit(targetFramework[4]))
                    {
                        return CreateFrameworkEnvironment(projectPath, true);
                    }

                    // Otherwise use the .NET Core SDK
                    return CreateCoreEnvironment(projectPath);
                }

                // Use Framework tools if a ToolsVersion attribute
                if (projectElement.GetAttributeValue("ToolsVersion") != null)
                {
                    return CreateFrameworkEnvironment(projectPath, false);
                }
            }

            throw new InvalidOperationException("Unrecognized project file format");
        }

        // Based on code from OmniSharp
        // https://github.com/OmniSharp/omnisharp-roslyn/blob/78ccc8b4376c73da282a600ac6fb10fce8620b52/src/OmniSharp.Abstractions/Services/DotNetCliService.cs
        private static BuildEnvironment CreateCoreEnvironment(string projectPath)
        {
            string dotnetPath = DotnetPathResolver.ResolvePath(projectPath);
            return new BuildEnvironment
            {
                ToolsPath = dotnetPath,
                ExtensionsPath = dotnetPath,
                SDKsPath = Path.Combine(dotnetPath, "Sdks"),
                RoslynTargetsPath = Path.Combine(dotnetPath, "Roslyn"),
            };
        }

        private static BuildEnvironment CreateFrameworkEnvironment(string projectPath, bool sdkProject)
        {
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
            return new BuildEnvironment
            {
                ToolsPath = toolsPath,
                ExtensionsPath = extensionsPath,
                SDKsPath = Path.Combine(sdkProject ? DotnetPathResolver.ResolvePath(projectPath) : extensionsPath, "Sdks"),
                RoslynTargetsPath = Path.Combine(toolsPath, "Roslyn"),
            };
        }
    }
}