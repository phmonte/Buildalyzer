using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
        // Places the log file in C:/Temp
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
            @"SdkNetCore2Project\SdkNetCore2Project.csproj",
            @"SdkNetCore31Project\SdkNetCore31Project.csproj",
            @"SdkNet5Project\SdkNet5Project.csproj",
            @"SdkNet6Project\SdkNet6Project.csproj",
            @"SdkNet6Exe\SdkNet6Exe.csproj",
            @"SdkNet6SelfContained\SdkNet6SelfContained.csproj",
            @"SdkNet6ImplicitUsings\SdkNet6ImplicitUsings.csproj",
            @"SdkNet7Project\SdkNet7Project.csproj",
            @"SdkNetCore2ProjectImport\SdkNetCore2ProjectImport.csproj",
            @"SdkNetCore2ProjectWithReference\SdkNetCore2ProjectWithReference.csproj",
            @"SdkNetCore2ProjectWithImportedProps\SdkNetCore2ProjectWithImportedProps.csproj",
            @"SdkNetCore2ProjectWithAnalyzer\SdkNetCore2ProjectWithAnalyzer.csproj",
            @"SdkNetStandardProject\SdkNetStandardProject.csproj",
            @"SdkNetStandardProjectImport\SdkNetStandardProjectImport.csproj",
            @"SdkNetStandardProjectWithPackageReference\SdkNetStandardProjectWithPackageReference.csproj",
            @"SdkNetStandardProjectWithConstants\SdkNetStandardProjectWithConstants.csproj",
            @"ResponseFile\ResponseFile.csproj",
            @"FunctionApp\FunctionApp.csproj",
            @"FunctionAppIsolated\FunctionAppIsolated.csproj",
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
            IAnalyzerResults results = analyzer.Build(options);

            // Then
            // If this is the multi-targeted project, use the net462 target
            IReadOnlyList<string> sourceFiles = results.Count == 1 ? results.First().SourceFiles : results["net462"].SourceFiles;
            sourceFiles.ShouldNotBeNull(log.ToString());
            new[]
            {
                "AssemblyAttributes",
                analyzer.ProjectFile.OutputType?.Equals("exe", StringComparison.OrdinalIgnoreCase) ?? false ? "Program" : "Class1",
                "AssemblyInfo"
            }.ShouldBeSubsetOf(sourceFiles.Select(x => Path.GetFileName(x).Split('.').TakeLast(2).First()), log.ToString());
        }

        [Test]
        public void GetsReferences(
            [ValueSource(nameof(Preferences))] EnvironmentPreference preference,
            [ValueSource(nameof(ProjectFiles))] [NotNull] string projectFile)
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
            references.ShouldContain(
                x => x.Contains("mscorlib"),
                log.ToString() + System.Environment.NewLine + "References:" + string.Join(System.Environment.NewLine, references));
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
                IAnalyzerResults results = analyzer.Manager.Analyze(binLogPath);

                // Then
                // If this is the multi-targeted project, use the net462 target
                IReadOnlyList<string> sourceFiles = results.Count == 1 ? results.First().SourceFiles : results["net462"].SourceFiles;
                sourceFiles.ShouldNotBeNull(log.ToString());
                new[]
                {
                "AssemblyAttributes",
                analyzer.ProjectFile.OutputType?.Equals("exe", StringComparison.OrdinalIgnoreCase) ?? false ? "Program" : "Class1",
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

        [Test]
        [Platform("win")]
        public void WpfControlLibraryGetsSourceFiles()
        {
            // Given
            StringWriter log = new StringWriter();
            IProjectAnalyzer analyzer = GetProjectAnalyzer(@"WpfCustomControlLibrary1\WpfCustomControlLibrary1.csproj", log);

            // When
            IAnalyzerResults results = analyzer.Build();

            // Then
            IReadOnlyList<string> sourceFiles = results.SingleOrDefault()?.SourceFiles;
            sourceFiles.ShouldNotBeNull(log.ToString());
            new[]
            {
                "CustomControl1.cs",
                "AssemblyInfo.cs",
                "Resources.Designer.cs",
                "Settings.Designer.cs",
                "GeneratedInternalTypeHelper.g.cs"
            }.ShouldBeSubsetOf(sourceFiles.Select(x => Path.GetFileName(x)), log.ToString());
        }

        [Test]
        public void MultiTargetingBuildAllTargetFrameworksGetsSourceFiles()
        {
            // Given
            StringWriter log = new StringWriter();
            IProjectAnalyzer analyzer = GetProjectAnalyzer(@"SdkMultiTargetingProject\SdkMultiTargetingProject.csproj", log);

            // When
            IAnalyzerResults results = analyzer.Build();

            // Then
            // Multi-targeting projects product an extra result with an empty target framework that holds some MSBuild properties (I.e. the "outer" build)
            results.Count.ShouldBe(3);
            results.TargetFrameworks.ShouldBe(new[] { "net462", "netstandard2.0", string.Empty }, ignoreOrder: false, log.ToString());
            results[string.Empty].SourceFiles.ShouldBeEmpty();
            new[]
            {
                "AssemblyAttributes",
                "Class1",
                "AssemblyInfo"
            }.ShouldBeSubsetOf(results["net462"].SourceFiles.Select(x => Path.GetFileName(x).Split('.').TakeLast(2).First()), log.ToString());
            new[]
            {
                "AssemblyAttributes",
                "Class2",
                "AssemblyInfo"
            }.ShouldBeSubsetOf(results["netstandard2.0"].SourceFiles.Select(x => Path.GetFileName(x).Split('.').TakeLast(2).First()), log.ToString());
        }

        [Test]
        public void SolutionDirShouldEndWithDirectorySeparator()
        {
            // Given
            StringWriter log = new StringWriter();
            IProjectAnalyzer analyzer = GetProjectAnalyzer(@"SdkMultiTargetingProject\SdkMultiTargetingProject.csproj", log);

            analyzer.SolutionDirectory.ShouldEndWith(Path.DirectorySeparatorChar.ToString());
        }

        [Test]
        public void MultiTargetingBuildFrameworkTargetFrameworkGetsSourceFiles()
        {
            // Given
            StringWriter log = new StringWriter();
            IProjectAnalyzer analyzer = GetProjectAnalyzer(@"SdkMultiTargetingProject\SdkMultiTargetingProject.csproj", log);

            // When
            IAnalyzerResults results = analyzer.Build("net462");

            // Then
            IReadOnlyList<string> sourceFiles = results.First(x => x.TargetFramework == "net462").SourceFiles;
            sourceFiles.ShouldNotBeNull(log.ToString());
            new[]
            {
                "AssemblyAttributes",
                "Class1",
                "AssemblyInfo"
            }.ShouldBeSubsetOf(sourceFiles.Select(x => Path.GetFileName(x).Split('.').TakeLast(2).First()), log.ToString());
        }

        [Test]
        public void MultiTargetingBuildCoreTargetFrameworkGetsSourceFiles()
        {
            // Given
            StringWriter log = new StringWriter();
            IProjectAnalyzer analyzer = GetProjectAnalyzer(@"SdkMultiTargetingProject\SdkMultiTargetingProject.csproj", log);

            // When
            IAnalyzerResults results = analyzer.Build("netstandard2.0");

            // Then
            IReadOnlyList<string> sourceFiles = results.First(x => x.TargetFramework == "netstandard2.0").SourceFiles;
            sourceFiles.ShouldNotBeNull(log.ToString());
            new[]
            {
                "AssemblyAttributes",
                "AssemblyInfo",
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
            IProjectAnalyzer analyzer = GetProjectAnalyzer(@"SdkNetCore2ProjectWithReference\SdkNetCore2ProjectWithReference.csproj", log);

            // When
            IEnumerable<string> references = analyzer.Build().First().ProjectReferences;

            // Then
            references.ShouldNotBeNull(log.ToString());
            references.ShouldContain(x => x.EndsWith("SdkNetStandardProjectWithPackageReference.csproj"), log.ToString());
            references.ShouldContain(x => x.EndsWith("SdkNetStandardProject.csproj"), log.ToString());
        }

        [Test]
        public void SdkProjectWithDefineContstantsGetsPreprocessorSymbols()
        {
            // Given
            StringWriter log = new StringWriter();
            IProjectAnalyzer analyzer = GetProjectAnalyzer(@"SdkNetStandardProjectWithConstants\SdkNetStandardProjectWithConstants.csproj", log);

            // When
            IEnumerable<string> preprocessorSymbols = analyzer.Build().First().PreprocessorSymbols;

            // Then
            preprocessorSymbols.ShouldNotBeNull(log.ToString());
            preprocessorSymbols.ShouldContain("DEF2", log.ToString());
            preprocessorSymbols.ShouldContain("NETSTANDARD2_0", log.ToString());

            // If this test runs on .NET 5 or greater, the NETSTANDARD2_0_OR_GREATER preprocessor symbol should be added. Can't test on lower SDK versions

#if NETSTANDARD2_0_OR_GREATER
            preprocessorSymbols.ShouldContain("NETSTANDARD2_0_OR_GREATER", log.ToString());
#endif
        }

        [Test]
        [Platform("win")]
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
            const string projectFile = @"SdkNetCore2Project\SdkNetCore2Project.csproj";
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
            // The generated GUIDs are based on subpath and can also change between MSBuild versions,
            // so this may need to be updated periodically
            results.First().ProjectGuid.ToString().ShouldBe("1ff50b40-c27b-5cea-b265-29c5436a8a7b");
        }

        [Test]
        public void BuildsProjectWithoutLogger([ValueSource(nameof(Preferences))] EnvironmentPreference preference)
        {
            // Given
            const string projectFile = @"SdkNetCore2Project\SdkNetCore2Project.csproj";
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
            results.First().SourceFiles.ShouldNotBeNull();
            results.OverallSuccess.ShouldBeTrue(log.ToString());
            results.ShouldAllBe(x => x.Succeeded, log.ToString());
        }

        [Test]
        public void BuildsVisualBasicProject()
        {
            // Given
            const string projectFile = @"VisualBasicProject\VisualBasicNetConsoleApp.vbproj";
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

            IAnalyzerResult result = results.First();
            result.PackageReferences.Count.ShouldBeGreaterThan(0);
            result.PackageReferences.ShouldContain(x => x.Key == "BouncyCastle.NetCore");
            result.SourceFiles.Length.ShouldBeGreaterThan(0);
            result.SourceFiles.ShouldContain(x => x.Contains("Program.vb"));
            result.References.Length.ShouldBeGreaterThan(0);
            result.References.ShouldContain(x => x.Contains("BouncyCastle.Crypto.dll"));
        }

        [Test]
        public void BuildsLotsOfProjects()
        {
            // Given
            StringWriter log = new StringWriter();
            AnalyzerManager manager = new AnalyzerManager(
                GetProjectPath(@"LotsOfProjects\LotsOfProjects.sln"),
                new AnalyzerManagerOptions
                {
                    LogWriter = log
                });
            List<IProjectAnalyzer> projects = manager.Projects.Values.ToList();

            // When
            List<IAnalyzerResults> analyzerResults = projects
                .AsParallel()
                .Select(x => x.Build())
                .ToList();

            // Then
            analyzerResults.Count.ShouldBe(50);
        }

        // To produce different versions, create a global.json and then run `dotnet clean` and `dotnet build -bl:SdkNetCore31Project-vX.binlog` from the source project folder
        [TestCase("SdkNetCore31Project-v9.binlog", 9)]
        [TestCase("SdkNetCore31Project-v14.binlog", 14)]
        public void GetsSourceFilesFromBinLogFile(string path, int expectedVersion)
        {
            // Verify this is the expected version
            path = Path.GetFullPath(
                Path.Combine(
                    Path.GetDirectoryName(typeof(SimpleProjectsFixture).Assembly.Location),
                    "..",
                    "..",
                    "..",
                    "..",
                    "binlogs",
                    path))
                .Replace('\\', Path.DirectorySeparatorChar);
            EnvironmentOptions options = new EnvironmentOptions();
            using (Stream stream = File.OpenRead(path))
            {
                using (GZipStream gzip = new GZipStream(stream, CompressionMode.Decompress))
                {
                    using (BinaryReader reader = new BinaryReader(gzip))
                    {
                        reader.ReadInt32().ShouldBe(expectedVersion);
                    }
                }
            }

            // Given
            StringWriter log = new StringWriter();
            AnalyzerManager analyzerManager = new AnalyzerManager(
                new AnalyzerManagerOptions
                {
                    LogWriter = log
                });

            // When
            IAnalyzerResults analyzerResults = analyzerManager.Analyze(path);
            IReadOnlyList<string> sourceFiles = analyzerResults.First().SourceFiles;

            // Then
            sourceFiles.ShouldNotBeNull(log.ToString());
            new[]
            {
            "AssemblyAttributes",
            "Class1",
            "AssemblyInfo"
            }.ShouldBeSubsetOf(sourceFiles.Select(x => Path.GetFileName(x).Split('.').TakeLast(2).First()), log.ToString());
        }

        [Test]
        public static void DuplicateProjectReferences()
        {
            // Given
            StringWriter log = new StringWriter();
            AnalyzerManager manager = new AnalyzerManager(
                GetProjectPath(@"DuplicateProjectReferences\MainProject\MainProject.sln"),
                new AnalyzerManagerOptions
                {
                    LogWriter = log
                });
            List<IProjectAnalyzer> projects = manager.Projects.Values.ToList();

            // When
            List<IAnalyzerResults> analyzerResults = projects
                .AsParallel()
                .Select(x => x.Build())
                .ToList();

            // Then
            analyzerResults.ForEach(v =>
            {
                IAnalyzerResult result = v.Results.FirstOrDefault();
                result.ProjectReferences.Count().ShouldBeLessThanOrEqualTo(1);
            });
        }

        [Test]
        public void GetsAdditionalCscFiles()
        {
            // Given
            StringWriter log = new StringWriter();
            IProjectAnalyzer analyzer = GetProjectAnalyzer(@"RazorClassLibraryTest\RazorClassLibraryTest.csproj", log);

            // When
            IEnumerable<string> additionalFiles = analyzer.Build().First().AdditionalFiles;

            // Then
            additionalFiles.ShouldBe(new[] { "_Imports.razor", "Component1.razor" }, log.ToString());
        }

        [Test]
        public void GetsAdditionalFile()
        {
            // Given
            StringWriter log = new StringWriter();
            IProjectAnalyzer analyzer = GetProjectAnalyzer(@"ProjectWithAdditionalFile\ProjectWithAdditionalFile.csproj", log);

            // When
            IEnumerable<string> additionalFiles = analyzer.Build().First().AdditionalFiles;

            // Then
            additionalFiles.ShouldBe(new[] { "message.txt" }, log.ToString());
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