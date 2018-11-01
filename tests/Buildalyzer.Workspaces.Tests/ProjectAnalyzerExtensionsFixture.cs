using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Shouldly;

namespace Buildalyzer.Workspaces.Tests
{
    [TestFixture]
    [NonParallelizable]
    public class ProjectAnalyzerExtensionsFixture
    {
        [Test]
        public void LoadsWorkspace()
        {
            // Given
            StringWriter log = new StringWriter();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(@"projects\SdkNetStandardProject\SdkNetStandardProject.csproj", log);
            
            // When
            Workspace workspace = analyzer.GetWorkspace();

            // Then
            workspace.CurrentSolution.Projects.First().Documents.ShouldContain(x => x.Name == "Class1.cs", log.ToString());
        }

        [Test]
        public void SupportsCompilation()
        {
            // Given
            StringWriter log = new StringWriter();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(@"projects\SdkNetStandardProject\SdkNetStandardProject.csproj", log);

            // When
            Workspace workspace = analyzer.GetWorkspace();
            Compilation compilation = workspace.CurrentSolution.Projects.First().GetCompilationAsync().Result;

            // Then
            compilation.GetSymbolsWithName(x => x == "Class1").ShouldNotBeEmpty(log.ToString());
        }

        [Test]
        public void CreatesCompilationOptions()
        {
            // Given
            StringWriter log = new StringWriter();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(@"projects\SdkNetStandardProject\SdkNetStandardProject.csproj", log);

            // When
            Workspace workspace = analyzer.GetWorkspace();
            CompilationOptions compilationOptions = workspace.CurrentSolution.Projects.First().CompilationOptions;

            // Then
            compilationOptions.OutputKind.ShouldBe(OutputKind.DynamicallyLinkedLibrary, log.ToString());
        }

        [TestCase(false, 1)]
        [TestCase(true, 2)]
        public void AddsProjectReferences(bool addProjectReferences, int totalProjects)
        {
            // Given
            StringWriter log = new StringWriter();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(@"projects\LegacyFrameworkProjectWithReference\LegacyFrameworkProjectWithReference.csproj", log);
            GetProjectAnalyzer(@"projects\LegacyFrameworkProject\LegacyFrameworkProject.csproj", log, analyzer.Manager);

            // When
            Workspace workspace = analyzer.GetWorkspace(addProjectReferences);

            // Then
            workspace.CurrentSolution.Projects.Count().ShouldBe(totalProjects, log.ToString());
        }

        [Test]
        public void SupportsConstants()
        {
            // Given
            StringWriter log = new StringWriter();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(@"projects\SdkNetStandardProjectWithConstants\SdkNetStandardProjectWithConstants.csproj", log);

            // When
            Workspace workspace = analyzer.GetWorkspace();
            Compilation compilation = workspace.CurrentSolution.Projects.First().GetCompilationAsync().Result;

            // Then
            compilation.GetSymbolsWithName(x => x == "Class1").ShouldBeEmpty(log.ToString());
            compilation.GetSymbolsWithName(x => x == "Class2").ShouldNotBeEmpty(log.ToString());
        }

        private ProjectAnalyzer GetProjectAnalyzer(string projectFile, StringWriter log, AnalyzerManager manager = null)
        {
            // The path will get normalized inside the .GetProject() call below
            string projectPath = Path.GetFullPath(
                Path.Combine(
                    Path.GetDirectoryName(typeof(ProjectAnalyzerExtensionsFixture).Assembly.Location),
                    @"..\..\..\..\" + projectFile));
            if (manager == null)
            {
                manager = new AnalyzerManager(new AnalyzerManagerOptions
                {
                    LogWriter = log
                });
            }
            return manager.GetProject(projectPath);
        }
    }
}
