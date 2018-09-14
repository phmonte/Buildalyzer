using Buildalyzer.Environment;
using LibGit2Sharp;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using NUnit.Framework;
using Shouldly;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Buildalyzer.Tests.Integration
{
    [TestFixture]
    [NonParallelizable]
    public class OpenSourceProjectsFixture
    {
        private const LoggerVerbosity Verbosity = LoggerVerbosity.Normal;
        private const bool BinaryLog = false;

        private static EnvironmentPreference[] Preferences =
        {
#if Is_Windows
            EnvironmentPreference.Framework,
#endif
            EnvironmentPreference.Core
        };

        private static TestRepository[] Repositories =
        {
            //new TestRepository(EnvironmentPreference.Framework, "https://github.com/AngleSharp/AngleSharp.git"),  // Something strange with NuGet and restore, won't build at all - Contains portable project, can't build using SDK
            new TestRepository("https://github.com/autofac/Autofac.git"),
            new TestRepository("https://github.com/AutoMapper/AutoMapper.git"),
            new TestRepository("https://github.com/SixLabors/ImageSharp.git"),
            //new TestRepository("https://github.com/moq/moq.git"),  does not use Build as the default target, see https://github.com/moq/moq/issues/21
            new TestRepository(EnvironmentPreference.Framework, "https://github.com/JamesNK/Newtonsoft.Json.git"),  // Contains portable project, can't build using SDK
            new TestRepository("https://github.com/nodatime/nodatime.git",
                @"\src\NodaTime.Web.Blazor\NodaTime.Web.Blazor.csproj"),
            new TestRepository("https://github.com/JasonBock/Rocks.git"),
            //new TestRepository("https://github.com/dotnet/roslyn.git"),  uses a special Restore.cmd prior to build
            new TestRepository(EnvironmentPreference.Framework, "https://github.com/serilog/serilog.git"), // SourceLink messed up from AppVeyor on SDK: "SourceLink.Create.CommandLine.dll. Assembly with same name is already loaded Confirm that the <UsingTask> declaration is correct"
            new TestRepository("https://github.com/Abc-Arbitrage/ZeroLog.git"),
            new TestRepository("https://github.com/cake-build/cake"),
            new TestRepository("https://github.com/Wyamio/Wyam.git"),
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
                Excluded = excluded ?? Array.Empty<string>();
            }

            public override string ToString() => Url;
        }

        private class ProjectTestCases : IEnumerable
        {
            public IEnumerator GetEnumerator()
            {
                // Iterate all repositories
                foreach(TestRepository repository in Repositories)
                {
                    // Iterate all available preferences
                    foreach (EnvironmentPreference preference in Preferences)
                    {
                        // Only build the desired preferences
                        if(!repository.Preference.HasValue || repository.Preference.Value == preference)
                        {
                            // Iterate all solution files in the repository
                            foreach(string solutionPath in
                                Directory.EnumerateFiles(GetRepositoryPath(repository.Url), "*.sln", SearchOption.AllDirectories))
                            {
                                // Exclude any solution files we don't want to build
                                if(!repository.Excluded.Any(x => solutionPath.EndsWith(x)))
                                {
                                    // Get all the projects in this solution
                                    foreach(string projectPath in
                                        AnalyzerManager.GetProjectsInSolution(solutionPath).Select(project => project.AbsolutePath))
                                    {
                                        // Exclude any project files we don't want to build
                                        if (!repository.Excluded.Any(x => projectPath.EndsWith(x)))
                                        {
                                            yield return new object[]
                                            {
                                                preference,
                                                solutionPath,
                                                projectPath
                                            };
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        // Do setup in a static constructor since the TestCaseSource depends on it
        // See https://stackoverflow.com/a/40507964/807064
        static OpenSourceProjectsFixture()
        {
            foreach (TestRepository repository in Repositories)
            {
                string path = GetRepositoryPath(repository.Url);
                CloneRepository(repository.Url, path);
            }
        }

        [TestCaseSource(typeof(ProjectTestCases))]
        public void CompilesProject(EnvironmentPreference preference, string solutionPath, string projectPath)
        {
            // Given
            StringWriter log = new StringWriter();
            AnalyzerManager manager = new AnalyzerManager(solutionPath, new AnalyzerManagerOptions
            {
                LogWriter = log,
                LoggerVerbosity = Verbosity
            });
            ProjectAnalyzer analyzer = manager.GetProject(projectPath);
            EnvironmentOptions options = new EnvironmentOptions
            {
                Preference = preference
            };

            // When
            DeleteProjectDirectory(analyzer.ProjectFile.Path, "obj");
            DeleteProjectDirectory(analyzer.ProjectFile.Path, "bin");
            analyzer.IgnoreFaultyImports = false;
            if (BinaryLog)
            {
                analyzer.AddBinaryLogger($@"E:\Temp\{Path.GetFileNameWithoutExtension(solutionPath)}.{Path.GetFileNameWithoutExtension(analyzer.ProjectFile.Path)}.core.binlog");
            }
            AnalyzerResults results = analyzer.Build(options);

            // Then
            results.Count.ShouldBeGreaterThan(0, log.ToString());
            results.ShouldAllBe(x => x.OverallSuccess, log.ToString());
        }

        private static void CloneRepository(string repository, string path)
        {
            if (!Directory.Exists(path))
            {
                TestContext.Progress.WriteLine($"Cloning { path }");
                Directory.CreateDirectory(path);
                string clonedPath = Repository.Clone(repository, path);
                TestContext.Progress.WriteLine($"Cloned into { clonedPath }");
            }
            else if (!Repository.IsValid(path))
            {
                TestContext.Progress.WriteLine($"Recloning { path }");
                Directory.Delete(path, true);
                Thread.Sleep(1000);
                Directory.CreateDirectory(path);
                string clonedPath = Repository.Clone(repository, path);
                TestContext.Progress.WriteLine($"Cloned into { clonedPath }");
            }
            else
            {
                TestContext.Progress.WriteLine($"Updating { path }");
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
