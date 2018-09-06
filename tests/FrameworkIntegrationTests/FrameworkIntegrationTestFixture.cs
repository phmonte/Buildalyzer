using Buildalyzer;
using LibGit2Sharp;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using NUnit.Framework;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FrameworkIntegrationTests
{
#if Is_Windows
    [TestFixture]
    [NonParallelizable]
    public class FrameworkIntegrationTestFixture
    {
        private const LoggerVerbosity Verbosity = LoggerVerbosity.Normal;
        private const bool BinaryLog = false;

        public class TestRepository
        {
            public string Url { get; }

            public string[] Excluded { get; }

            public TestRepository(string url, params string[] excluded)
            {
                Url = url;
                Excluded = excluded ?? Array.Empty<string>();
            }

            public override string ToString() => Url;
        }

        private static TestRepository[] _repositories =
        {
            new TestRepository("https://github.com/AngleSharp/AngleSharp.git"),
            new TestRepository("https://github.com/autofac/Autofac.git"),
            new TestRepository("https://github.com/AutoMapper/AutoMapper.git"),
            new TestRepository("https://github.com/SixLabors/ImageSharp.git"),
            //new TestRepository("https://github.com/moq/moq.git"),  does not use Build as the default target, see https://github.com/moq/moq/issues/21
            new TestRepository("https://github.com/JamesNK/Newtonsoft.Json.git"),
            new TestRepository("https://github.com/nodatime/nodatime.git",
                @"\src\NodaTime.Web.Blazor\NodaTime.Web.Blazor.csproj"),
            new TestRepository("https://github.com/JasonBock/Rocks.git"),
            //new TestRepository("https://github.com/dotnet/roslyn.git"),  uses a special Restore.cmd prior to build
            new TestRepository("https://github.com/Reactive-Extensions/Rx.NET.git",
                @"\Ix.NET\Integration\Uwp\Uwp.csproj",  // Can't build XAML projects
                @"\Ix.NET\Integration\Win81\Win81.csproj",  // Can't build XAML projects
                @"\Ix.NET\Integration\Wpa81\Wpa81.csproj",  // Can't build XAML projects
                @"\Ix.NET\Integration\Wp8\Wp8.csproj",  // Can't build Windows Phone projects
                @"\Ix.NET\Integration\tvOS\tvOS.csproj", // Can't build tvOS projects
                @"\Ix.NET\Integration\Android\Android.csproj", // Can't build Android projects
                @"\Ix.NET\Integration\iOS\iOS.csproj", // Can't build iOS projects
                @"\Rx.NET\Integration\Installation\Uwp\Uwp.csproj",  // Can't build XAML projects,
                @"\Rx.NET\Integration\Installation\Win81\Win81.csproj",  // Can't build XAML projects
                @"\Rx.NET\Integration\Installation\Wpa81\Wpa81.csproj",  // Can't build XAML projects
                @"\Rx.NET\Integration\Installation\Wp8\Wp8.csproj",  // Can't build Windows Phone projects
                @"\Rx.NET\Integration\Installation\tvOS\tvOS.csproj", // Can't build tvOS projects
                @"\Rx.NET\Integration\Installation\Android\Android.csproj", // Can't build Android projects
                @"\Rx.NET\Integration\Installation\iOS\iOS.csproj", // Can't build iOS projects
                @"\Rx.NET\Samples\Portable\SilverlightApplication\SilverlightApplication.csproj",  // Can't build Silverlight projects
                @"\Rx.NET\Source\src\System.Reactive\System.Reactive.csproj", // Multi-targets UAP projects which Restore can't handle (since Restore scans all frameworks for each build in the _GetRestoreSettingsPerFramework target)
                @"_NuGet.csproj",  // These projects use local packages
                @"\Rx.NET\Source\src\Microsoft.Reactive.Testing\Microsoft.Reactive.Testing.csproj",
                @"\Rx.NET\Source\tests\Tests.System.Reactive\Tests.System.Reactive.csproj",
                @"\Rx.NET\Source\src\System.Reactive.Observable.Aliases\System.Reactive.Observable.Aliases.csproj",
                @"\Rx.NET\Source\tests\Tests.System.Reactive.Uwp.DeviceRunner\Tests.System.Reactive.Uwp.DeviceRunner.csproj",
                @"\Rx.NET\Source\facades\System.Reactive.Core\System.Reactive.Core.csproj",
                @"\Rx.NET\Source\facades\System.Reactive.Linq\System.Reactive.Linq.csproj",
                @"\Rx.NET\Source\facades\System.Reactive.PlatformServices\System.Reactive.PlatformServices.csproj",
                @"\Rx.NET\Source\facades\System.Reactive.Providers\System.Reactive.Providers.csproj",
                @"\Rx.NET\Source\facades\System.Reactive.Windows.Threading\System.Reactive.Windows.Threading.csproj",
                @"\Rx.NET\Source\facades\System.Reactive.WindowsRuntime\System.Reactive.WindowsRuntime.csproj",
                @"\Rx.NET\Source\facades\System.Reactive.Interfaces\System.Reactive.Interfaces.csproj",
                @"\Rx.NET\Integration\BindingRedirects\CommonCodeInPcl\CommonCodeInPcl.csproj",
                @"\Rx.NET\Integration\FacadeTest\FacadeTest.csproj",
                @"\Rx.NET\Integration\Installation\Win81Wpa81\Win81Wpa81.csproj",
                @"\Rx.NET\Integration\Installation\Pcl259\Pcl259.csproj",
                @"\Rx.NET\Samples\Portable\PortableClassLibrary\PortableClassLibrary.csproj"
            ),
            new TestRepository("https://github.com/serilog/serilog.git"),
            new TestRepository("https://github.com/Abc-Arbitrage/ZeroLog.git"),
            new TestRepository("https://github.com/cake-build/cake"),
            new TestRepository("https://github.com/Wyamio/Wyam.git"),
        };

        private static IEnumerable<(string, string)> SolutionAndProjectPaths => 
            _repositories
                .SelectMany(repository =>
                    Directory.GetFiles(GetRepositoryPath(repository.Url), "*.sln", SearchOption.AllDirectories)
                        .Where(solutionPath => !repository.Excluded.Any(e => solutionPath.EndsWith(e)))
                        .SelectMany(solutionPath => AnalyzerManager.GetProjectsInSolution(solutionPath)
                            .Select(project => project.AbsolutePath)
                            .Where(projectPath => !repository.Excluded.Any(e => projectPath.EndsWith(e)))
                            .Select(projectPath => (solutionPath, projectPath))));

        [OneTimeSetUp]
        public void SetUp()
        {
            foreach (TestRepository repository in _repositories)
            {
                string path = GetRepositoryPath(repository.Url);
                CloneRepository(repository.Url, path);
            }
        }

        [TestCaseSource(nameof(SolutionAndProjectPaths))]
        public void CompilesProject((string, string) solutionAndProjectPath)
        {
            // Given
            StringWriter log = new StringWriter();
            AnalyzerManager manager = new AnalyzerManager(solutionAndProjectPath.Item1, new AnalyzerManagerOptions
            {
                LogWriter = log,
                LoggerVerbosity = Verbosity
            });
            ProjectAnalyzer analyzer = manager.GetProject(solutionAndProjectPath.Item2);

            // When
            DeleteProjectDirectory(analyzer.ProjectFile.Path, "obj");
            DeleteProjectDirectory(analyzer.ProjectFile.Path, "bin");
            analyzer.IgnoreFaultyImports = false;
            if (BinaryLog)
            {
                analyzer.AddBinaryLogger($@"E:\Temp\{Path.GetFileNameWithoutExtension(solutionAndProjectPath.Item1)}.{Path.GetFileNameWithoutExtension(analyzer.ProjectFile.Path)}.framework.binlog");
            }
            AnalyzerResults results = analyzer.Build();

            // Then
            results.Count.ShouldBeGreaterThan(0, log.ToString());
            results.ShouldAllBe(x => x.OverallSuccess, log.ToString());
            //results.ShouldAllBe(x => x.ProjectInstance != null, log.ToString());
        }

        private static void CloneRepository(string repository, string path)
        {
            if (!Directory.Exists(path))
            {
                TestContext.Progress.WriteLine($"Cloning { path }");
                Directory.CreateDirectory(path);
                Repository.Clone(repository, path);
            }
            else if (!Repository.IsValid(path))
            {
                TestContext.Progress.WriteLine($"Recloning { path }");
                Directory.Delete(path, true);
                Thread.Sleep(1000);
                Directory.CreateDirectory(path);
                Repository.Clone(repository, path);
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
                    Path.GetDirectoryName(typeof(FrameworkIntegrationTestFixture).Assembly.Location),
                    @"..\..\..\repos\" + Path.GetFileNameWithoutExtension(repository)));

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
#endif
}
