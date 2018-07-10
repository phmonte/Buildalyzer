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
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Buildalyzer.Environment;

namespace NetCoreTests
{
    [TestFixture]
    public class NetCoreTestFixture
    {
        private const LoggerVerbosity Verbosity = LoggerVerbosity.Normal;
        private const bool BinaryLog = true;

        private static string[] _projectFiles =
        {
#if Is_Windows
            @"LegacyFrameworkProject\LegacyFrameworkProject.csproj",
            @"LegacyFrameworkProjectWithReference\LegacyFrameworkProjectWithReference.csproj",
            @"SdkFrameworkProject\SdkFrameworkProject.csproj",
#endif
            @"SdkNetCoreProject\SdkNetCoreProject.csproj",
            @"SdkNetCoreProjectImport\SdkNetCoreProjectImport.csproj",
            @"SdkNetStandardProject\SdkNetStandardProject.csproj",
            @"SdkNetStandardProjectImport\SdkNetStandardProjectImport.csproj",
            @"SdkNetStandardProjectWithPackageReference\SdkNetStandardProjectWithPackageReference.csproj",
            @"SdkProjectWithImportedProps\SdkProjectWithImportedProps.csproj",
            @"SdkMultiTargetingProject\SdkMultiTargetingProject.csproj"
        };

        [TestCaseSource(nameof(_projectFiles))]
        public void LoadsProject(string projectFile)
        {
            // Given
            StringWriter log = new StringWriter();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(projectFile, log);

            // When
            DeleteProjectDirectory(projectFile, "obj");
            DeleteProjectDirectory(projectFile, "bin");
            Project project = analyzer.Load();

            // Then
            project.ShouldNotBeNull(log.ToString());
        }

        [TestCaseSource(nameof(_projectFiles))]
        public void DesignTimeBuildsProject(string projectFile)
        {
            // Given
            StringWriter log = new StringWriter();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(projectFile, log);

            // When
            DeleteProjectDirectory(projectFile, "obj");
            DeleteProjectDirectory(projectFile, "bin");
            AnalyzerResults results = analyzer.Build();

            // Then
            results.Count.ShouldBeGreaterThan(0, log.ToString());
            results.First().ProjectInstance.ShouldNotBeNull(log.ToString());
        }

        [TestCaseSource(nameof(_projectFiles))]
        public void BuildsProject(string projectFile)
        {
            // Given
            StringWriter log = new StringWriter();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(projectFile, log);
            EnvironmentOptions options = new EnvironmentOptions
            {
                DesignTime = false
            };

            // When
            DeleteProjectDirectory(projectFile, "obj");
            DeleteProjectDirectory(projectFile, "bin");
            AnalyzerResults results = analyzer.Build(options);

            // Then
            results.Count.ShouldBeGreaterThan(0, log.ToString());
            results.First().ProjectInstance.ShouldNotBeNull(log.ToString());
        }

        [TestCaseSource(nameof(_projectFiles))]
        public void GetsSourceFiles(string projectFile)
        {
            // Given
            StringWriter log = new StringWriter();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(projectFile, log);

            // When
            IReadOnlyList<string> sourceFiles = analyzer.Build().First().GetSourceFiles();

            // Then
            sourceFiles.ShouldNotBeNull(log.ToString());
            sourceFiles.Select(x => Path.GetFileName(x).Split('.').TakeLast(2).First()).ShouldBe(new[]
            {
                "Class1",
                "AssemblyAttributes",
                "AssemblyInfo"
            }, true, log.ToString());
        }

        [Test]
        public void BuildAllTargetFrameworksGetsSourceFiles()
        {
            // Given
            StringWriter log = new StringWriter();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(@"SdkMultiTargetingProject\SdkMultiTargetingProject.csproj", log);

            // When
            AnalyzerResults results = analyzer.Build();

            // Then
            results.Count.ShouldBe(2);
            results.TargetFrameworks.ShouldBe(new[] { "net462", "netstandard2.0" }, true, log.ToString());
            results["net462"].GetSourceFiles().Select(x => Path.GetFileName(x).Split('.').Reverse().Take(2).Reverse().First()).ShouldBe(new[]
            {
                "Class1",
                "AssemblyAttributes",
                "AssemblyInfo"
            }, true, log.ToString());
            results["netstandard2.0"].GetSourceFiles().Select(x => Path.GetFileName(x).Split('.').Reverse().Take(2).Reverse().First()).ShouldBe(new[]
            {
                "Class2",
                "AssemblyAttributes",
                "AssemblyInfo"
            }, true, log.ToString());
        }

        [Test]
        public void BuildTargetFrameworkGetsSourceFiles()
        {
            // Given
            StringWriter log = new StringWriter();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(@"SdkMultiTargetingProject\SdkMultiTargetingProject.csproj", log);

            // When
            IReadOnlyList<string> sourceFiles = analyzer.Build("net462").GetSourceFiles();

            // Then
            sourceFiles.ShouldNotBeNull(log.ToString());
            sourceFiles.Select(x => Path.GetFileName(x).Split('.').Reverse().Take(2).Reverse().First()).ShouldBe(new[]
            {
                "Class1",
                "AssemblyAttributes",
                "AssemblyInfo"
            }, true, log.ToString());

            // When
            log.GetStringBuilder().Clear();
            sourceFiles = analyzer.Build("netstandard2.0").GetSourceFiles();

            // Then
            sourceFiles.ShouldNotBeNull(log.ToString());
            sourceFiles.Select(x => Path.GetFileName(x).Split('.').Reverse().Take(2).Reverse().First()).ShouldBe(new[]
            {
                "Class2",
                "AssemblyAttributes",
                "AssemblyInfo"
            }, true, log.ToString());
        }

        [TestCaseSource(nameof(_projectFiles))]
        public void GetsVirtualProjectSourceFiles(string projectFile)
        {
            // Given
            StringWriter log = new StringWriter();
            projectFile = GetProjectPath(projectFile);
            XDocument projectDocument = XDocument.Load(projectFile);
            projectFile = projectFile.Replace(".csproj", "Virtual.csproj");
            ProjectAnalyzer analyzer = new AnalyzerManager(
                new AnalyzerManagerOptions
                {
                    LogWriter = log,
                    LoggerVerbosity = Verbosity
                })
                .GetProject(projectFile, projectDocument);

            // When
            IReadOnlyList<string> sourceFiles = analyzer.Build().First().GetSourceFiles();

            // Then
            sourceFiles.ShouldNotBeNull(log.ToString());
            sourceFiles.ShouldContain(x => x.EndsWith("Class1.cs"), log.ToString());
        }

        [TestCaseSource(nameof(_projectFiles))]
        public void GetsReferences(string projectFile)
        {
            // Given
            StringWriter log = new StringWriter();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(projectFile, log);

            // When
            IReadOnlyList<string> references = analyzer.Build().First().GetReferences();

            // Then
            references.ShouldNotBeNull(log.ToString());
            references.ShouldContain(x => x.EndsWith("mscorlib.dll"), log.ToString());
            if (projectFile.Contains("PackageReference"))
            {
                references.ShouldContain(x => x.EndsWith("NodaTime.dll"), log.ToString());
            }
        }

        [Test]
        public void SdkProjectWithPackageReferenceGetsReferences()
        {
            // Given
            StringWriter log = new StringWriter();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(@"SdkNetStandardProjectWithPackageReference\SdkNetStandardProjectWithPackageReference.csproj", log);

            // When
            IReadOnlyList<string> references = analyzer.Build().First().GetReferences();

            // Then
            references.ShouldNotBeNull(log.ToString());
            references.ShouldContain(x => x.EndsWith("NodaTime.dll"), log.ToString());
        }

        [Test]
        public void GetsProjectsInSolution()
        {
            // Given
            StringWriter log = new StringWriter();

            // When
            AnalyzerManager manager = new AnalyzerManager(
                GetProjectPath("TestProjects.sln"),
                new AnalyzerManagerOptions
                {
                    LogWriter = log,
                    LoggerVerbosity = Verbosity
                });

            // Then
            _projectFiles.Select(x => GetProjectPath(x)).ShouldBeSubsetOf(manager.Projects.Keys, log.ToString());
        }

        [Test]
        public void IgnoreSolutionItemsThatAreNotProjects()
        {
            // Given / When
            AnalyzerManager manager = new AnalyzerManager(GetProjectPath("TestProjects.sln"));

            // Then
            manager.Projects.Any(x => x.Value.ProjectFile.Path.Contains("TestEmptySolutionFolder")).ShouldBeFalse();
        }

        [Test]
        public void ThrowsForLegacyFrameworkProjectWithPackageReference()
        {
            // Given, When, Then
            AnalyzerManager manager = new AnalyzerManager();
            ProjectAnalyzer analyzer = manager.GetProject(GetProjectPath(@"LegacyFrameworkProjectWithPackageReference\LegacyFrameworkProjectWithPackageReference.csproj"));
            Should.Throw<Exception>(() => analyzer.Load());      
        }

        private static ProjectAnalyzer GetProjectAnalyzer(string projectFile, StringWriter log)
        {
            ProjectAnalyzer analyzer = new AnalyzerManager(
                new AnalyzerManagerOptions
                {
                    LogWriter = log,
                    LoggerVerbosity = Verbosity
                })
                .GetProject(GetProjectPath(projectFile));
            if (BinaryLog)
            {
                analyzer.AddBinaryLogger(Path.Combine(@"E:\Temp\", Path.ChangeExtension(Path.GetFileName(projectFile), ".core.binlog")));
            }
            return analyzer;
        }

        private static string GetProjectPath(string file)
        {
            string path = Path.GetFullPath(
                Path.Combine(
                    Path.GetDirectoryName(typeof(NetCoreTestFixture).Assembly.Location),
                    @"..\..\..\..\projects\" + file));

            return path.Replace('\\', Path.DirectorySeparatorChar);
        }

        private static void DeleteProjectDirectory(string projectFile, string directory)
        {
            string path = Path.Combine(Path.GetDirectoryName(GetProjectPath(projectFile)), directory);
            if(Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }
}
