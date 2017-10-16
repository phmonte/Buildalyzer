using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using NUnit.Framework;
using Shouldly;

namespace AppTests
{
    [TestFixture]
    public class AppTestFixture
    {
        private static string[] _projectFiles =
        {
#if Is_Windows
            @"projects\LegacyFrameworkProject\LegacyFrameworkProject.csproj",
            @"projects\LegacyFrameworkProjectWithReference\LegacyFrameworkProjectWithReference.csproj",
#endif
            @"projects\SdkNetCoreProject\SdkNetCoreProject.csproj",
            @"projects\SdkNetCoreProjectImport\SdkNetCoreProjectImport.csproj",
            @"projects\SdkNetStandardProject\SdkNetStandardProject.csproj",
            @"projects\SdkNetStandardProjectImport\SdkNetStandardProjectImport.csproj"
        };

#if DEBUG
        private static string OutputPath = @"bin\Debug";
#else
        private static string OutputPath = @"bin\Release";
#endif


#if Is_Windows
        [TestCaseSource(nameof(_projectFiles))]
        public void X86FrameworkApp(string projectFile)
        {
            string fileName = $@"{GetProjectFilePath(@"apps\X86FrameworkApp")}\{OutputPath}\X86FrameworkApp.exe";
            string arguments = $"\"{GetProjectFilePath(projectFile)}\"";
            RunApp(fileName, arguments);
        }

        [TestCaseSource(nameof(_projectFiles))]
        public void X64FrameworkApp(string projectFile)
        {
            string fileName = $@"{GetProjectFilePath(@"apps\X64FrameworkApp")}\{OutputPath}\X64FrameworkApp.exe";
            string arguments = $"\"{GetProjectFilePath(projectFile)}\"";
            RunApp(fileName, arguments);
        }
#endif
        
        [TestCaseSource(nameof(_projectFiles))]
        public void X86CoreApp(string projectFile) =>
            RunApp("dotnet", $"run --project \"{GetProjectFilePath(@"apps\X86CoreApp")}\" \"{GetProjectFilePath(projectFile)}\"");

        [TestCaseSource(nameof(_projectFiles))]
        public void X64CoreApp(string projectFile) =>
            RunApp("dotnet", $"run --project \"{GetProjectFilePath(@"apps\X64CoreApp")}\" \"{GetProjectFilePath(projectFile)}\"");

        private string GetProjectFilePath(string projectFile) => 
            Path.GetFullPath(
                Path.Combine(
                    Path.GetDirectoryName(typeof(AppTestFixture).Assembly.Location),
                    @"..\..\..\..\" + projectFile));

        private void RunApp(string fileName, string arguments)
        {
            // Given
            ProcessStartInfo startInfo = new ProcessStartInfo(fileName, arguments)
            {
                WorkingDirectory = Path.GetDirectoryName(fileName),
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            // When
            string output;
            string error;
            int exitCode;
            using (Process process = Process.Start(startInfo))
            {
                output = process.StandardOutput.ReadToEnd();
                error = process.StandardError.ReadToEnd();
                process.WaitForExit(1000);
                exitCode = process.ExitCode;
            }

            // Then
            exitCode.ShouldBe(0, error);
            output.ShouldContain("Class1.cs", error);
        }
    }
}
