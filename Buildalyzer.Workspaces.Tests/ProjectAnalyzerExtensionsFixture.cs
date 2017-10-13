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
    public class ProjectAnalyzerExtensionsFixture
    {
        [Test]
        public void LoadsWorkspace()
        {
            // Given
            StringBuilder log = new StringBuilder();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(@"SdkNetStandardProject\SdkNetStandardProject.csproj", log);
            
            // When
            Workspace workspace = analyzer.GetWorkspace();

            // Then
            workspace.CurrentSolution.Projects.First().Documents.ShouldContain(x => x.Name == "Class1.cs");
        }

        [Test]
        public void SupportsCompilation()
        {
            // Given
            StringBuilder log = new StringBuilder();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(@"SdkNetStandardProject\SdkNetStandardProject.csproj", log);

            // When
            Workspace workspace = analyzer.GetWorkspace();
            Compilation compilation = workspace.CurrentSolution.Projects.First().GetCompilationAsync().Result;

            // Then
            compilation.GetSymbolsWithName(x => x == "Class1").ShouldNotBeEmpty();
        }
        
        [Test]
        public void CreatesCompilationOptions()
        {
            // Given
            StringBuilder log = new StringBuilder();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(@"SdkNetStandardProject\SdkNetStandardProject.csproj", log);

            // When
            Workspace workspace = analyzer.GetWorkspace();
            CompilationOptions compilationOptions = workspace.CurrentSolution.Projects.First().CompilationOptions;

            // Then
            compilationOptions.OutputKind.ShouldBe(OutputKind.DynamicallyLinkedLibrary);
        }

        private ProjectAnalyzer GetProjectAnalyzer(string projectFile, StringBuilder log)
        {
            string projectPath = Path.GetFullPath(
                Path.Combine(
                    Path.GetDirectoryName(typeof(ProjectAnalyzerExtensionsFixture).Assembly.Location),
                    @"..\..\..\..\" + projectFile));
            LoggerFactory loggerFactory = null;
            if (log != null)
            {
                loggerFactory = new LoggerFactory();
                loggerFactory.AddProvider(new StringBuilderLoggerProvider(log));
            }
            AnalyzerManager manager = new AnalyzerManager(loggerFactory);
            return manager.GetProject(projectPath.Replace('\\', Path.DirectorySeparatorChar));
        }
    }
}
