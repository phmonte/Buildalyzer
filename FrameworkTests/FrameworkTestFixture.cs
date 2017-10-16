using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Buildalyzer;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Shouldly;
#if Is_Windows
namespace FrameworkTests
{
    [TestFixture]
    public class FrameworkTestFixture
    {
        private static string[] _projectFiles =
        {
            @"LegacyFrameworkProject\LegacyFrameworkProject.csproj",
            @"LegacyFrameworkProjectWithReference\LegacyFrameworkProjectWithReference.csproj",
            @"SdkNetCoreProject\SdkNetCoreProject.csproj",
            @"SdkNetStandardProject\SdkNetStandardProject.csproj",
            @"SdkNetCoreProjectImport\SdkNetCoreProjectImport.csproj",
            @"SdkNetStandardProjectImport\SdkNetStandardProjectImport.csproj"
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
                    Path.GetDirectoryName(typeof(FrameworkTestFixture).Assembly.Location),
                    @"..\..\..\" + projectFile));
            LoggerFactory loggerFactory = null;
            if (log != null)
            {
                loggerFactory = new LoggerFactory();
                loggerFactory.AddProvider(new StringBuilderLoggerProvider(log));
            }
            AnalyzerManager manager = new AnalyzerManager(loggerFactory);
            return manager.GetProject(projectPath);
        }
    }
}
#endif
