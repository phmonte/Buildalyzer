using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Buildalyzer.Environment;
using LibGit2Sharp;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using NUnit.Framework;
using Shouldly;

namespace Buildalyzer.Tests.Integration
{
    [TestFixture]
    [NonParallelizable]
    public class OpenSourceProjectsFixture
    {
        private const bool BinaryLog = false;

        private static readonly EnvironmentPreference[] Preferences =
        {
#if Is_Windows
            EnvironmentPreference.Framework,
#endif
            EnvironmentPreference.Core
        };

        private static readonly TestRepository[] Repositories =
        {
            new TestRepository(
                "https://github.com/autofac/Autofac.git",
                @"\bench\Autofac.Benchmarks\Autofac.Benchmarks.csproj"),
            new TestRepository("https://github.com/AutoMapper/AutoMapper.git"),
            new TestRepository(EnvironmentPreference.Framework, "https://github.com/serilog/serilog.git"), // SourceLink messed up from AppVeyor on SDK: "SourceLink.Create.CommandLine.dll. Assembly with same name is already loaded Confirm that the <UsingTask> declaration is correct"
            new TestRepository("https://github.com/cake-build/cake"),
            new TestRepository("https://github.com/Wyamio/Wyam.git")
        };

        public class TestRepository
        {
            public EnvironmentPreference? Preference { get; }

            public string Url { get; }

            public string[] Excluded { get; }

            public TestRepository(string url, params string[] excluded)
                : this(null, url, excluded)
            {
            }

            public TestRepository(EnvironmentPreference? preference, string url, params string[] excluded)
            {
                Preference = preference;
                Url = url;
                Excluded = excluded?.Select(x => x.Replace('\\', Path.DirectorySeparatorChar)).ToArray() ?? Array.Empty<string>();
            }

            public override string ToString() => Url;
        }

        private static readonly List<object[]> ProjectTestCases = new List<object[]>();

        // Do setup in a static constructor since the TestCaseSource depends on it
        // See https://stackoverflow.com/a/40507964/807064
        static OpenSourceProjectsFixture()
        {
            foreach (TestRepository repository in Repositories)
            {
                string path = GetRepositoryPath(repository.Url);
                CloneRepository(repository.Url, path);
            }

            // Iterate all repositories
            foreach (TestRepository repository in Repositories)
            {
                // Iterate all available preferences
                foreach (EnvironmentPreference preference in Preferences)
                {
                    // Only build the desired preferences
                    if (!repository.Preference.HasValue || repository.Preference.Value == preference)
                    {
                        // Iterate all solution files in the repository
                        foreach (string solutionPath in
                            Directory.EnumerateFiles(GetRepositoryPath(repository.Url), "*.sln", SearchOption.AllDirectories))
                        {
                            // Exclude any solution files we don't want to build
                            if (!repository.Excluded.Any(x => solutionPath.EndsWith(x)))
                            {
                                // Get all the projects in this solution
                                SolutionFile solutionFile = SolutionFile.Parse(solutionPath);
                                foreach (string projectPath in
                                    solutionFile.ProjectsInOrder
                                    .Where(x => AnalyzerManager.SupportedProjectTypes.Contains(x.ProjectType))
                                    .Select(x => x.AbsolutePath))
                                {
                                    // Exclude any project files we don't want to build
                                    if (!repository.Excluded.Any(x => projectPath.EndsWith(x)))
                                    {
                                        ProjectTestCases.Add(new object[]
                                        {
                                                preference,
                                                solutionPath,
                                                projectPath
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        [TestCaseSource(nameof(ProjectTestCases))]
        public void CompilesProject(EnvironmentPreference preference, string solutionPath, string projectPath)
        {
            // Given
            StringWriter log = new StringWriter();
            AnalyzerManager manager = new AnalyzerManager(solutionPath, new AnalyzerManagerOptions
            {
                LogWriter = log
            });
            IProjectAnalyzer analyzer = manager.GetProject(projectPath);
            EnvironmentOptions options = new EnvironmentOptions
            {
                Preference = preference
            };

            // Set some environment variables to make it seem like we're not in a CI build
            // Sometimes this messes up libraries like SourceLink since we're building as part of a test and not for CI
            options.EnvironmentVariables.Add("APPVEYOR", "False");
            options.EnvironmentVariables.Add("ContinuousIntegrationBuild", null);
            options.EnvironmentVariables.Add("CI", "False");
            options.EnvironmentVariables.Add("CI_LINUX", "False");
            options.EnvironmentVariables.Add("CI_WINDOWS", "False");

            // When
            DeleteProjectDirectory(analyzer.ProjectFile.Path, "obj");
            DeleteProjectDirectory(analyzer.ProjectFile.Path, "bin");
            analyzer.IgnoreFaultyImports = false;

#pragma warning disable 0162
            if (BinaryLog)
            {
                analyzer.AddBinaryLogger($@"C:\Temp\{Path.GetFileNameWithoutExtension(solutionPath)}.{Path.GetFileNameWithoutExtension(analyzer.ProjectFile.Path)}.core.binlog");
            }
#pragma warning restore 0162

#if Is_Windows
            IAnalyzerResults results = analyzer.Build(options);
#else
            // On non-Windows platforms we have to remove the .NET Framework target frameworks and only build .NET Core target frameworks
            // See https://github.com/dotnet/sdk/issues/826
            string[] excludedTargetFrameworks = new[] { "net2", "net3", "net4", "portable" };
            string[] targetFrameworks = analyzer.ProjectFile.TargetFrameworks.Where(x => !excludedTargetFrameworks.Any(y => x.StartsWith(y))).ToArray();
            if (targetFrameworks.Length == 0)
            {
                Assert.Ignore();
            }
            IAnalyzerResults results = analyzer.Build(targetFrameworks, options);
#endif

            // Then
            results.Count.ShouldBeGreaterThan(0, log.ToString());
            results.OverallSuccess.ShouldBeTrue(log.ToString());
            results.ShouldAllBe(x => x.Succeeded, log.ToString());
        }

        private static void CloneRepository(string repository, string path)
        {
            if (!Directory.Exists(path))
            {
                TestContext.Progress.WriteLine($"Cloning {path}");
                Directory.CreateDirectory(path);
                string clonedPath = Repository.Clone(repository, path);
                TestContext.Progress.WriteLine($"Cloned into {clonedPath}");
            }
            else if (!Repository.IsValid(path))
            {
                TestContext.Progress.WriteLine($"Recloning {path}");
                Directory.Delete(path, true);
                Thread.Sleep(1000);
                Directory.CreateDirectory(path);
                string clonedPath = Repository.Clone(repository, path);
                TestContext.Progress.WriteLine($"Cloned into {clonedPath}");
            }
            else
            {
                TestContext.Progress.WriteLine($"Updating {path}");
                Repository repo = new Repository(path);
                foreach (Remote remote in repo.Network.Remotes)
                {
                    IEnumerable<string> refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
                    Commands.Fetch(repo, remote.Name, refSpecs, null, string.Empty);
                }
            }
        }

        private static string GetRepositoryPath(string repository)
        {
            string path = Path.GetFullPath(
                Path.Combine(
                    Path.GetDirectoryName(typeof(OpenSourceProjectsFixture).Assembly.Location),
                    @"..\..\..\..\repos\" + Path.GetFileNameWithoutExtension(repository)));

            return path.Replace('\\', Path.DirectorySeparatorChar);
        }

        private static void DeleteProjectDirectory(string projectPath, string directory)
        {
            string path = Path.Combine(Path.GetDirectoryName(projectPath), directory);
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }
}
