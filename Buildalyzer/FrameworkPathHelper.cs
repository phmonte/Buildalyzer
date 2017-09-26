using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Utilities;

namespace Buildalyzer
{
    internal class FrameworkPathHelper : IPathHelper
    {
        public string ToolsPath { get; }
        public string ExtensionsPath { get; }
        public string SDKsPath { get; }
        public string RoslynTargetsPath { get; }

        public FrameworkPathHelper()
        {
            ToolsPath = GetToolsPath();
            ExtensionsPath = Path.GetFullPath(Path.Combine(ToolsPath, @"..\..\"));
            SDKsPath = Path.Combine(ExtensionsPath, "Sdks");
            RoslynTargetsPath = Path.Combine(ToolsPath, "Roslyn");
        }

        private static string GetToolsPath()
        {
            string toolsPath = ToolLocationHelper.GetPathToBuildToolsFile("msbuild.exe", ToolLocationHelper.CurrentToolsVersion);
            if (string.IsNullOrEmpty(toolsPath))
            {
                // Could not find the tools path, possibly due to https://github.com/Microsoft/msbuild/issues/2369
                // Try to poll for it
                toolsPath = PollForToolsPath();
            }
            if (string.IsNullOrEmpty(toolsPath))
            {
                throw new InvalidOperationException("Could not locate the tools (msbuild.exe) path");
            }
            return Path.GetDirectoryName(toolsPath);
        }

        // From https://github.com/KirillOsenkov/MSBuildStructuredLog/blob/4649f55f900a324421bad5a714a2584926a02138/src/StructuredLogViewer/MSBuildLocator.cs
        private static string PollForToolsPath()
        {
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            return new[]
            {
                Path.Combine(programFilesX86, @"Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\Bin\MSBuild.exe"),
                Path.Combine(programFilesX86, @"Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\MSBuild.exe"),
                Path.Combine(programFilesX86, @"Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe"),
                Path.Combine(programFilesX86, @"MSBuild\14.0\Bin\MSBuild.exe"),
                Path.Combine(programFilesX86, @"MSBuild\12.0\Bin\MSBuild.exe")
            }
            .Where(File.Exists)
            .FirstOrDefault();
        }
    }
}