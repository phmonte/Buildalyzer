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
            IProjectAnalyzer analyzer = GetProjectAnalyzer(@"projects\SdkNetStandardProject\SdkNetStandardProject.csproj", log);

            // When
            Workspace workspace = analyzer.GetWorkspace();

            // Then
            workspace.CurrentSolution.Projects.First().Documents.ShouldContain(x => x.Name == "Class1.cs", log.ToString());
        }

        [Test]
        public void LoadsSolution()
        {
            // Given
            string solutionPath = GetFullPath(@"projects\TestProjects.sln");
            StringWriter log = new StringWriter();
            AnalyzerManager manager = new AnalyzerManager(solutionPath, new AnalyzerManagerOptions { LogWriter = log });

            // When
            Workspace workspace = manager.GetWorkspace();

            // Then
            workspace.CurrentSolution.FilePath.ShouldBe(solutionPath);
            workspace.CurrentSolution.Projects.ShouldContain(p => p.Name == "LegacyFrameworkProject");
            workspace.CurrentSolution.Projects.ShouldContain(p => p.Name == "SdkFrameworkProject");
        }

        [Test]
        public void SupportsCompilation()
        {
            // Given
            StringWriter log = new StringWriter();
            IProjectAnalyzer analyzer = GetProjectAnalyzer(@"projects\SdkNetStandardProject\SdkNetStandardProject.csproj", log);

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
            IProjectAnalyzer analyzer = GetProjectAnalyzer(@"projects\SdkNetStandardProject\SdkNetStandardProject.csproj", log);

            // When
            Workspace workspace = analyzer.GetWorkspace();
            CompilationOptions compilationOptions = workspace.CurrentSolution.Projects.First().CompilationOptions;

            // Then
            compilationOptions.OutputKind.ShouldBe(OutputKind.DynamicallyLinkedLibrary, log.ToString());
        }

        [TestCase(false, 1)]
        [TestCase(true, 3)]
        public void AddsProjectReferences(bool addProjectReferences, int totalProjects)
        {
            // Given
            StringWriter log = new StringWriter();
            IProjectAnalyzer analyzer = GetProjectAnalyzer(@"projects\LegacyFrameworkProjectWithReference\LegacyFrameworkProjectWithReference.csproj", log);

            // When
            Workspace workspace = analyzer.GetWorkspace(addProjectReferences);

            // Then
            workspace.CurrentSolution.Projects.Count().ShouldBe(totalProjects, log.ToString());
        }

        [TestCase(false, 1)]
        [TestCase(true, 4)]
        public void AddsTransitiveProjectReferences(bool addProjectReferences, int totalProjects)
        {
            // Given
            StringWriter log = new StringWriter();
            IProjectAnalyzer analyzer = GetProjectAnalyzer(@"projects\TransitiveProjectReference\TransitiveProjectReference.csproj", log);

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
            IProjectAnalyzer analyzer = GetProjectAnalyzer(@"projects\SdkNetStandardProjectWithConstants\SdkNetStandardProjectWithConstants.csproj", log);

            // When
            Workspace workspace = analyzer.GetWorkspace();
            Compilation compilation = workspace.CurrentSolution.Projects.First().GetCompilationAsync().Result;

            // Then
            compilation.GetSymbolsWithName(x => x == "Class1").ShouldBeEmpty(log.ToString());
            compilation.GetSymbolsWithName(x => x == "Class2").ShouldNotBeEmpty(log.ToString());
        }

        [Test]
        public void SupportsAnalyzers()
        {
            // Given
            StringWriter log = new StringWriter();
            IProjectAnalyzer analyzer = GetProjectAnalyzer(@"projects\SdkNetCoreProjectWithAnalyzer\SdkNetCoreProjectWithAnalyzer.csproj", log);

            // When
            Workspace workspace = analyzer.GetWorkspace();
            Project project = workspace.CurrentSolution.Projects.First();

            // Then
            project.AnalyzerReferences.ShouldContain(reference => reference.Display == "Microsoft.CodeQuality.Analyzers");
        }

        private IProjectAnalyzer GetProjectAnalyzer(string projectFile, StringWriter log, AnalyzerManager manager = null)
        {
            // The path will get normalized inside the .GetProject() call below
            string projectPath = GetFullPath(projectFile);
            if (manager == null)
            {
                manager = new AnalyzerManager(new AnalyzerManagerOptions { LogWriter = log });
            }

            return manager.GetProject(projectPath);
        }

        private static string GetFullPath(string partialPath)
        {
            return Path.GetFullPath(
                Path.Combine(
                    Path.GetDirectoryName(typeof(ProjectAnalyzerExtensionsFixture).Assembly.Location),
                    @"..\..\..\..\" + partialPath));
        }
    }
}