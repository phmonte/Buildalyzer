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

#if Is_Windows
        [TestCaseSource(nameof(_projectFiles))]
        public void X86FrameworkAppRuns(string projectFile) =>
            RunApp(Path.Combine(TestContext.CurrentContext.TestDirectory, "X86FrameworkApp.exe"), $"\"\"\"{GetProjectFilePath(projectFile)}\"\"\"");

        [TestCaseSource(nameof(_projectFiles))]
        public void X64FrameworkAppRuns(string projectFile) =>
            RunApp(Path.Combine(TestContext.CurrentContext.TestDirectory, "X64FrameworkApp.exe"), $"\"\"\"{GetProjectFilePath(projectFile)}\"\"\"");
#endif
        
        [TestCaseSource(nameof(_projectFiles))]
        public void X86CoreAppRuns(string projectFile) =>
            RunApp("dotnet", $"run --project \"{GetProjectFilePath(@"apps\X86CoreApp")}\" \"{GetProjectFilePath(projectFile)}\"");

        [TestCaseSource(nameof(_projectFiles))]
        public void X64CoreAppRuns(string projectFile) =>
            RunApp("dotnet", $"run --project \"{GetProjectFilePath(@"apps\X86CoreApp")}\" \"{GetProjectFilePath(projectFile)}\"");

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
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };

            // When
            List<string> lines = new List<string>();
            int exitCode;
            using (Process process = Process.Start(startInfo))
            {
                string line;
                while ((line = process.StandardOutput.ReadLine()) != null)
                {
                    lines.Add(line);
                }
                process.WaitForExit(1000);
                exitCode = process.ExitCode;
            }

            // Then
            exitCode.ShouldBe(0);
            lines.ShouldContain(x => x.Contains("Class1.cs"));
        }
    }
}
