using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Buildalyzer.Environment;
using Microsoft.Build.Framework;
using NUnit.Framework;
using Shouldly;

namespace Buildalyzer.Tests.Integration
{
    [TestFixture]
    [NonParallelizable]
    public class SimpleProjectsFixture
    {
        private const bool BinaryLog = false;

        private static readonly EnvironmentPreference[] Preferences =
        {
#if Is_Windows
            EnvironmentPreference.Framework,
#endif
            EnvironmentPreference.Core
        };

        private static readonly string[] ProjectFiles =
        {
#if Is_Windows
            @"LegacyFrameworkProject\LegacyFrameworkProject.csproj",
            @"LegacyFrameworkProjectWithReference\LegacyFrameworkProjectWithReference.csproj",
            @"LegacyFrameworkProjectWithPackageReference\LegacyFrameworkProjectWithPackageReference.csproj",
            @"SdkFrameworkProject\SdkFrameworkProject.csproj",
            @"SdkMultiTargetingProject\SdkMultiTargetingProject.csproj",
#endif
            @"SdkNetCoreProject\SdkNetCoreProject.csproj",
            @"SdkNetCore31Project\SdkNetCore31Project.csproj",
            @"SdkNetCoreProjectImport\SdkNetCoreProjectImport.csproj",
            @"SdkNetCoreProjectWithReference\SdkNetCoreProjectWithReference.csproj",
            @"SdkNetCoreProjectWithImportedProps\SdkNetCoreProjectWithImportedProps.csproj",
            @"SdkNetStandardProject\SdkNetStandardProject.csproj",
            @"SdkNetStandardProjectImport\SdkNetStandardProjectImport.csproj",
            @"SdkNetStandardProjectWithPackageReference\SdkNetStandardProjectWithPackageReference.csproj",
            @"SdkNetStandardProjectWithConstants\SdkNetStandardProjectWithConstants.csproj"
        };

        [Test]
        public void DesignTimeBuildsProject(
            [ValueSource(nameof(Preferences))] EnvironmentPreference preference,
            [ValueSource(nameof(ProjectFiles))] string projectFile)
        {
            // Given
            StringWriter log = new StringWriter();
            IProjectAnalyzer analyzer = GetProjectAnalyzer(projectFile, log);
            EnvironmentOptions options = new EnvironmentOptions
            {
                Preference = preference
            };

            // When
            DeleteProjectDirectory(projectFile, "obj");
            DeleteProjectDirectory(projectFile, "bin");
            IAnalyzerResults results = analyzer.Build(options);

            // Then
            results.Count.ShouldBeGreaterThan(0, log.ToString());
            results.OverallSuccess.ShouldBeTrue(log.ToString());
            results.ShouldAllBe(x => x.Succeeded, log.ToString());
        }

        [Test]
        public void BuildsProject(
            [ValueSource(nameof(Preferences))] EnvironmentPreference preference,
            [ValueSource(nameof(ProjectFiles))] string projectFile)
        {
            // Given
            StringWriter log = new StringWriter();
            IProjectAnalyzer analyzer = GetProjectAnalyzer(projectFile, log);
            EnvironmentOptions options = new EnvironmentOptions
            {
                Preference = preference,
                DesignTime = false
            };

            // When
            DeleteProjectDirectory(projectFile, "obj");
            DeleteProjectDirectory(projectFile, "bin");
            IAnalyzerResults results = analyzer.Build(options);

            // Then
            results.Count.ShouldBeGreaterThan(0, log.ToString());
            results.OverallSuccess.ShouldBeTrue(log.ToString());
            results.ShouldAllBe(x => x.Succeeded, log.ToString());
        }

        [Test]
        public void GetsSourceFiles(
            [ValueSource(nameof(Preferences))] EnvironmentPreference preference,
            [ValueSource(nameof(ProjectFiles))] string projectFile)
        {
            // Given
            StringWriter log = new StringWriter();
            IProjectAnalyzer analyzer = GetProjectAnalyzer(projectFile, log);
            EnvironmentOptions options = new EnvironmentOptions
            {
                Preference = preference
            };

            // When
            IReadOnlyList<string> sourceFiles = analyzer.Build(options).First().SourceFiles;

            // Then
            sourceFiles.ShouldNotBeNull(log.ToString());
            new[]
            {
#if Is_Windows
                // Linux and Mac builds appear to omit the AssemblyAttributes.cs file
                "AssemblyAttributes",
#endif
                "Class1",
                "AssemblyInfo"
            }.ShouldBeSubsetOf(sourceFiles.Select(x => Path.GetFileName(x).Split('.').TakeLast(2).First()), log.ToString());
        }

        [Test]
        public void GetsReferences(
            [ValueSource(nameof(Preferences))] EnvironmentPreference preference,
            [ValueSource(nameof(ProjectFiles))] string projectFile)
        {
            // Given
            StringWriter log = new StringWriter();
            IProjectAnalyzer analyzer = GetProjectAnalyzer(projectFile, log);
            EnvironmentOptions options = new EnvironmentOptions
            {
                Preference = preference
            };

            // When
            IReadOnlyList<string> references = analyzer.Build(options).First().References;

            // Then
            references.ShouldNotBeNull(log.ToString());
            references.ShouldContain(x => x.Contains("mscorlib"), log.ToString());
            if (projectFile.Contains("PackageReference"))
            {
                references.ShouldContain(x => x.EndsWith("NodaTime.dll"), log.ToString());
            }
        }

        [Test]
        public void GetsSourceFilesFromBinaryLog(
            [ValueSource(nameof(Preferences))] EnvironmentPreference preference,
            [ValueSource(nameof(ProjectFiles))] string projectFile)
        {
            // Given
            StringWriter log = new StringWriter();
            IProjectAnalyzer analyzer = GetProjectAnalyzer(projectFile, log);
            EnvironmentOptions options = new EnvironmentOptions
            {
                Preference = preference
            };
            string binLogPath = Path.ChangeExtension(Path.GetTempFileName(), ".binlog");
            analyzer.AddBinaryLogger(binLogPath);

            try
            {
                // When
                analyzer.Build(options);
                IReadOnlyList<string> sourceFiles = analyzer.Manager.Analyze(binLogPath).First().SourceFiles;

                // Then
                sourceFiles.ShouldNotBeNull(log.ToString());
                new[]
                {
#if Is_Windows
                // Linux and Mac builds appear to omit the AssemblyAttributes.cs file
                "AssemblyAttributes",
#endif
                "Class1",
                "AssemblyInfo"
                }.ShouldBeSubsetOf(sourceFiles.Select(x => Path.GetFileName(x).Split('.').TakeLast(2).First()), log.ToString());
            }
            finally
            {
                if (File.Exists(binLogPath))
                {
                    File.Delete(binLogPath);
                }
            }
        }

#if Is_Windows
        [Test]
        public void MultiTargetingBuildAllTargetFrameworksGetsSourceFiles()
        {
            // Given
            StringWriter log = new StringWriter();
            IProjectAnalyzer analyzer = GetProjectAnalyzer(@"SdkMultiTargetingProject\SdkMultiTargetingProject.csproj", log);

            // When
            IAnalyzerResults results = analyzer.Build();

            // Then
            results.Count.ShouldBe(2);
            results.TargetFrameworks.ShouldBe(new[] { "net462", "netstandard2.0" }, true, log.ToString());
            new[]
            {
#if Is_Windows
                // Linux and Mac builds appear to omit the AssemblyAttributes.cs file
                "AssemblyAttributes",
#endif
                "Class1",
                "AssemblyInfo"
            }.ShouldBeSubsetOf(results["net462"].SourceFiles.Select(x => Path.GetFileName(x).Split('.').TakeLast(2).First()), log.ToString());
            new[]
            {
#if Is_Windows
                // Linux and Mac builds appear to omit the AssemblyAttributes.cs file
                "AssemblyAttributes",
#endif
                "Class2",
                "AssemblyInfo"
            }.ShouldBeSubsetOf(results["netstandard2.0"].SourceFiles.Select(x => Path.GetFileName(x).Split('.').TakeLast(2).First()), log.ToString());
        }

        [Test]
        public void MultiTargetingBuildFrameworkTargetFrameworkGetsSourceFiles()
        {
            // Given
            StringWriter log = new StringWriter();
            IProjectAnalyzer analyzer = GetProjectAnalyzer(@"SdkMultiTargetingProject\SdkMultiTargetingProject.csproj", log);

            // When
            IReadOnlyList<string> sourceFiles = analyzer.Build("net462").First().SourceFiles;

            // Then
            sourceFiles.ShouldNotBeNull(log.ToString());
            new[]
            {
#if Is_Windows
                // Linux and Mac builds appear to omit the AssemblyAttributes.cs file
                "AssemblyAttributes",
#endif
                "Class1",
                "AssemblyInfo"
            }.ShouldBeSubsetOf(sourceFiles.Select(x => Path.GetFileName(x).Split('.').TakeLast(2).First()), log.ToString());
        }
#endif

        [Test]
        public void MultiTargetingBuildCoreTargetFrameworkGetsSourceFiles()
        {
            // Given
            StringWriter log = new StringWriter();
            IProjectAnalyzer analyzer = GetProjectAnalyzer(@"SdkMultiTargetingProject\SdkMultiTargetingProject.csproj", log);

            // When
            IReadOnlyList<string> sourceFiles = analyzer.Build("netstandard2.0").First().SourceFiles;

            // Then
            sourceFiles.ShouldNotBeNull(log.ToString());
            new[]
            {
#if Is_Windows
                // Linux and Mac builds appear to omit the AssemblyAttributes.cs file
                "AssemblyAttributes",
                "AssemblyInfo",
#endif
                "Class2"
            }.ShouldBeSubsetOf(sourceFiles.Select(x => Path.GetFileName(x).Split('.').TakeLast(2).First()), log.ToString());
        }

        [Test]
        public void SdkProjectWithPackageReferenceGetsReferences()
        {
            // Given
            StringWriter log = new StringWriter();
            IProjectAnalyzer analyzer = GetProjectAnalyzer(@"SdkNetStandardProjectWithPackageReference\SdkNetStandardProjectWithPackageReference.csproj", log);

            // When
            IReadOnlyList<string> references = analyzer.Build().First().References;

            // Then
            references.ShouldNotBeNull(log.ToString());
            references.ShouldContain(x => x.EndsWith("NodaTime.dll"), log.ToString());
        }

        [Test]
        public void SdkProjectWithPackageReferenceGetsPackageReferences()
        {
            // Given
            StringWriter log = new StringWriter();
            IProjectAnalyzer analyzer = GetProjectAnalyzer(@"SdkNetStandardProjectWithPackageReference\SdkNetStandardProjectWithPackageReference.csproj", log);

            // When
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> packageReferences = analyzer.Build().First().PackageReferences;

            // Then
            packageReferences.ShouldNotBeNull(log.ToString());
            packageReferences.Keys.ShouldContain("NodaTime", log.ToString());
        }

        [Test]
        public void SdkProjectWithProjectReferenceGetsReferences()
        {
            // Given
            StringWriter log = new StringWriter();
            IProjectAnalyzer analyzer = GetProjectAnalyzer(@"SdkNetCoreProjectWithReference\SdkNetCoreProjectWithReference.csproj", log);

            // When
            IEnumerable<string> references = analyzer.Build().First().ProjectReferences;

            // Then
            references.ShouldNotBeNull(log.ToString());
            references.ShouldContain(x => x.EndsWith("SdkNetStandardProjectWithPackageReference.csproj"), log.ToString());
            references.ShouldContain(x => x.EndsWith("SdkNetStandardProject.csproj"), log.ToString());
        }

#if Is_Windows
        [Test]
        public void LegacyFrameworkProjectWithPackageReferenceGetsReferences()
        {
            // Given
            StringWriter log = new StringWriter();
            IProjectAnalyzer analyzer = GetProjectAnalyzer(@"LegacyFrameworkProjectWithPackageReference\LegacyFrameworkProjectWithPackageReference.csproj", log);

            // When
            IReadOnlyList<string> references = analyzer.Build().First().References;

            // Then
            references.ShouldNotBeNull(log.ToString());
            references.ShouldContain(x => x.EndsWith("NodaTime.dll"), log.ToString());
        }

        [Test]
        public void LegacyFrameworkProjectWithPackageReferenceGetsPackageReferences()
        {
            // Given
            StringWriter log = new StringWriter();
            IProjectAnalyzer analyzer = GetProjectAnalyzer(@"LegacyFrameworkProjectWithPackageReference\LegacyFrameworkProjectWithPackageReference.csproj", log);

            // When
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> packageReferences = analyzer.Build().First().PackageReferences;

            // Then
            packageReferences.ShouldNotBeNull(log.ToString());
            packageReferences.Keys.ShouldContain("NodaTime", log.ToString());
        }

        [Test]
        public void LegacyFrameworkProjectWithProjectReferenceGetsReferences()
        {
            // Given
            StringWriter log = new StringWriter();
            IProjectAnalyzer analyzer = GetProjectAnalyzer(@"LegacyFrameworkProjectWithReference\LegacyFrameworkProjectWithReference.csproj", log);

            // When
            IEnumerable<string> references = analyzer.Build().First().ProjectReferences;

            // Then
            references.ShouldNotBeNull(log.ToString());
            references.ShouldContain(x => x.EndsWith("LegacyFrameworkProject.csproj"), log.ToString());
            references.ShouldContain(x => x.EndsWith("LegacyFrameworkProjectWithPackageReference.csproj"), log.ToString());
        }
#endif

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
                    LogWriter = log
                });

            // Then
            ProjectFiles.Select(x => GetProjectPath(x)).ShouldBeSubsetOf(manager.Projects.Keys, log.ToString());
        }

        [Test]
        public void FiltersProjectsInSolution()
        {
            // Given
            StringWriter log = new StringWriter();

            // When
            AnalyzerManager manager = new AnalyzerManager(
                GetProjectPath("TestProjects.sln"),
                new AnalyzerManagerOptions
                {
                    LogWriter = log,
                    ProjectFilter = x => x.AbsolutePath.Contains("Core")
                });

            // Then
            ProjectFiles.Select(x => GetProjectPath(x)).Where(x => x.Contains("Core")).ShouldBe(manager.Projects.Keys, true, log.ToString());
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
        public void GetsProjectGuidFromSolution([ValueSource(nameof(Preferences))] EnvironmentPreference preference)
        {
            // Given
            AnalyzerManager manager = new AnalyzerManager(
                GetProjectPath("TestProjects.sln"));
            IProjectAnalyzer analyzer = manager.Projects.First(x => x.Key.EndsWith("SdkNetStandardProject.csproj")).Value;
            EnvironmentOptions options = new EnvironmentOptions
            {
                Preference = preference
            };

            // When
            DeleteProjectDirectory(analyzer.ProjectFile.Path, "obj");
            DeleteProjectDirectory(analyzer.ProjectFile.Path, "bin");
            IAnalyzerResults results = analyzer.Build(options);

            // Then
            results.First().ProjectGuid.ToString().ShouldBe("016713d9-b665-4272-9980-148801a9b88f");
        }

        [Test]
        public void GetsProjectGuidFromProject([ValueSource(nameof(Preferences))] EnvironmentPreference preference)
        {
            // Given
            const string projectFile = @"SdkNetCoreProject\SdkNetCoreProject.csproj";
            IProjectAnalyzer analyzer = new AnalyzerManager()
                .GetProject(GetProjectPath(projectFile));
            EnvironmentOptions options = new EnvironmentOptions
            {
                Preference = preference
            };

            // When
            DeleteProjectDirectory(projectFile, "obj");
            DeleteProjectDirectory(projectFile, "bin");
            IAnalyzerResults results = analyzer.Build(options);

            // Then
            // The generated GUIDs are based on subpath, so they'll be different from Windows to Linux
#if Is_Windows
            results.First().ProjectGuid.ToString().ShouldBe("646a532e-8943-5a4b-b106-e1341b4d3535");
#else
            results.First().ProjectGuid.ToString().ShouldBe("c9df4376-d954-5554-bd10-b9976b7afa9d");
#endif
        }

        [Test]
        public void BuildsProjectWithoutLogger([ValueSource(nameof(Preferences))] EnvironmentPreference preference)
        {
            // Given
            const string projectFile = @"SdkNetCoreProject\SdkNetCoreProject.csproj";
            IProjectAnalyzer analyzer = new AnalyzerManager()
                .GetProject(GetProjectPath(projectFile));
            EnvironmentOptions options = new EnvironmentOptions
            {
                Preference = preference
            };

            // When
            DeleteProjectDirectory(projectFile, "obj");
            DeleteProjectDirectory(projectFile, "bin");
            IAnalyzerResults results = analyzer.Build(options);

            // Then
            results.Count.ShouldBeGreaterThan(0);
            results.OverallSuccess.ShouldBeTrue();
            results.ShouldAllBe(x => x.Succeeded);
        }

        [Test]
        public void BuildsFSharpProject()
        {
            // Given
            const string projectFile = @"FSharpProject\FSharpProject.fsproj";
            StringWriter log = new StringWriter();
            IProjectAnalyzer analyzer = GetProjectAnalyzer(projectFile, log);

            // When
            DeleteProjectDirectory(projectFile, "obj");
            DeleteProjectDirectory(projectFile, "bin");
            IAnalyzerResults results = analyzer.Build();

            // Then
            results.Count.ShouldBeGreaterThan(0, log.ToString());
            results.OverallSuccess.ShouldBeTrue(log.ToString());
            results.ShouldAllBe(x => x.Succeeded, log.ToString());
        }

        [Test]
        public void GetsSourceFilesFromVersion9BinLog()
        {
            // Given
            StringWriter log = new StringWriter();
            IProjectAnalyzer analyzer = GetProjectAnalyzer(
                @"SdkNetCore31Project\SdkNetCore31Project.csproj",
                log);
            string binLogPath = Path.ChangeExtension(Path.GetTempFileName(), ".binlog");
            EnvironmentOptions options = new EnvironmentOptions();
            options.Arguments.Add("/bl:" + binLogPath); // Tell MSBuild to produce the binlog so we use the latest internal logger

            try
            {
                // When
                analyzer.Build(options);
                using (Stream stream = File.OpenRead(binLogPath))
                {
                    using (GZipStream gzip = new GZipStream(stream, CompressionMode.Decompress))
                    {
                        using (BinaryReader reader = new BinaryReader(gzip))
                        {
                            // Verify this produced a version 9 binlog
                            reader.ReadInt32().ShouldBe(9);
                        }
                    }
                }
                IReadOnlyList<string> sourceFiles = analyzer.Manager.Analyze(binLogPath).First().SourceFiles;

                // Then
                sourceFiles.ShouldNotBeNull(log.ToString());
                new[]
                {
#if Is_Windows
                // Linux and Mac builds appear to omit the AssemblyAttributes.cs file
                "AssemblyAttributes",
#endif
                "Class1",
                "AssemblyInfo"
                }.ShouldBeSubsetOf(sourceFiles.Select(x => Path.GetFileName(x).Split('.').TakeLast(2).First()), log.ToString());
            }
            finally
            {
                if (File.Exists(binLogPath))
                {
                    File.Delete(binLogPath);
                }
            }
        }

        private static IProjectAnalyzer GetProjectAnalyzer(string projectFile, StringWriter log)
        {
            IProjectAnalyzer analyzer = new AnalyzerManager(
                new AnalyzerManagerOptions
                {
                    LogWriter = log
                })
                .GetProject(GetProjectPath(projectFile));

#pragma warning disable 0162
            if (BinaryLog)
            {
                analyzer.AddBinaryLogger(Path.Combine(@"C:\Temp\", Path.ChangeExtension(Path.GetFileName(projectFile), ".core.binlog")));
            }
#pragma warning restore 0162

            return analyzer;
        }

        private static string GetProjectPath(string file)
        {
            string path = Path.GetFullPath(
                Path.Combine(
                    Path.GetDirectoryName(typeof(SimpleProjectsFixture).Assembly.Location),
                    "..",
                    "..",
                    "..",
                    "..",
                    "projects",
                    file));

            return path.Replace('\\', Path.DirectorySeparatorChar);
        }

        private static void DeleteProjectDirectory(string projectFile, string directory)
        {
            string path = Path.Combine(Path.GetDirectoryName(GetProjectPath(projectFile)), directory);
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }
}
