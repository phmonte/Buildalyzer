using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Buildalyzer;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Shouldly;

namespace NetCoreTests
{
    [TestFixture]
    public class NetCoreTestFixture
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

        [TestCaseSource(nameof(_projectFiles))]
        public void LoadsProject(string projectFile)
        {
            // Given
            StringBuilder log = new StringBuilder();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(projectFile, log);

            // When
            Project project = analyzer.Load();

            // Then
            project.ShouldNotBeNull(log.ToString());
        }

        [TestCaseSource(nameof(_projectFiles))]
        public void CompilesProject(string projectFile)
        {
            // Given
            StringBuilder log = new StringBuilder();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(projectFile, log);

            // When
            ProjectInstance projectInstance = analyzer.Compile();

            // Then
            projectInstance.ShouldNotBeNull(log.ToString());
        }

        [TestCaseSource(nameof(_projectFiles))]
        public void GetsSourceFiles(string projectFile)
        {
            // Given
            StringBuilder log = new StringBuilder();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(projectFile, log);

            // When
            IReadOnlyList<string> sourceFiles = analyzer.GetSourceFiles();

            // Then
            sourceFiles.ShouldContain(x => x.EndsWith("Class1.cs"));
        }

        private ProjectAnalyzer GetProjectAnalyzer(string projectFile, StringBuilder log)
        {
            string projectPath = Path.GetFullPath(
                Path.Combine(
                    Path.GetDirectoryName(typeof(NetCoreTestFixture).Assembly.Location),
                    @"..\..\..\..\" + projectFile));
            AnalyzerManager manager = new AnalyzerManager(log);
            return manager.GetProject(projectPath.Replace('\\', Path.DirectorySeparatorChar));
        }
    }
}
