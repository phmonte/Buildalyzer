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

namespace NetCoreIntegrationTests
{
    [TestFixture]
    public class NetCoreIntegrationTestFixture
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
            //new TestRepository("https://github.com/AngleSharp/AngleSharp.git"),  contains portable project, can't build
            new TestRepository("https://github.com/autofac/Autofac.git"),
            new TestRepository("https://github.com/AutoMapper/AutoMapper.git"),
            //new TestRepository("https://github.com/MarimerLLC/csla.git"),  contains portable project, can't build
            new TestRepository("https://github.com/SixLabors/ImageSharp.git"),
            //new TestRepository("https://github.com/moq/moq.git"),  does not use Build as the default target, see https://github.com/moq/moq/issues/21
            //new TestRepository("https://github.com/JamesNK/Newtonsoft.Json.git"),  contains portable project, can't build
            //new TestRepository("https://github.com/nodatime/nodatime.git"),  fails when running Blazor commands due to design-time build and missing artifacts at E:\NuGet\microsoft.aspnetcore.blazor.build\0.4.0\targets\Blazor.MonoRuntime.targets(529,5)
            new TestRepository("https://github.com/JasonBock/Rocks.git"),
            new TestRepository("https://github.com/dotnet/roslyn.git"),
            new TestRepository("https://github.com/Reactive-Extensions/Rx.NET.git",
                @"\Ix.NET\Integration\Uwp\Uwp.csproj",  // Can't build XAML projects
                @"\Ix.NET\Integration\Win81Wpa81\Win81Wpa81.csproj",  // Can't build portable projects from a .NET Core host
                @"\Ix.NET\Integration\Pcl259\Pcl259.csproj",  // Can't build portable projects from a .NET Core host
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
                @"\Rx.NET\Source\src\System.Reactive\System.Reactive.csproj", // Can't restore from .NET Core when using MSBuild.Sdk.Extras (see https://github.com/onovotny/MSBuildSdkExtras/issues/85)
                @"_NuGet.csproj"  // These projects uses local packages
            ),  
            new TestRepository("https://github.com/serilog/serilog.git"),
            new TestRepository("https://github.com/Abc-Arbitrage/ZeroLog.git"),
            new TestRepository("https://github.com/cake-build/cake"),
        };

        [TestCaseSource(nameof(_repositories))]
        public void CompilesProject(TestRepository repository)
        {
            // Given
            string path = GetRepositoryPath(repository.Url);
            string[] solutionFiles = CloneOrFetchRepository(repository.Url, path);
            foreach (string solutionFile in solutionFiles
                .Where(x => !repository.Excluded.Any(e => x.EndsWith(e))))
            {
                StringWriter log = new StringWriter();
                AnalyzerManager manager = new AnalyzerManager(solutionFile, new AnalyzerManagerOptions
                {
                    LogWriter = log,
                    LoggerVerbosity = Verbosity
                });

                foreach (ProjectAnalyzer analyzer in manager.Projects.Values
                    .Where(x => !repository.Excluded.Any(e => x.ProjectFile.Path.EndsWith(e))))
                {
                    // When
                    Console.WriteLine(analyzer.ProjectFile.Path);
                    if (BinaryLog)
                    {
                        analyzer.AddBinaryLogger($@"E:\Temp\{Path.GetFileNameWithoutExtension(solutionFile)}.{Path.GetFileNameWithoutExtension(analyzer.ProjectFile.Path)}.integration.core.binlog");
                    }
                    AnalyzerResults results = analyzer.BuildAllTargetFrameworks();

                    // Then
                    results.Count.ShouldBeGreaterThan(0, log.ToString());
                    results.First().OverallSuccess.ShouldBeTrue(log.ToString());
                    results.First().ProjectInstance.ShouldNotBeNull(log.ToString());
                }
            }
        }

        private static string[] CloneOrFetchRepository(string repository, string path)
        {
            if(!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                Repository.Clone(repository, path);
            }
            else
            {
                Repository repo = new Repository(path);
                foreach(Remote remote in repo.Network.Remotes)
                {
                    IEnumerable<string> refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
                    Commands.Fetch(repo, remote.Name, refSpecs, null, string.Empty);
                }
            }
            return Directory.GetFiles(path, "*.sln", SearchOption.AllDirectories).ToArray();            
        }

        private static string GetRepositoryPath(string repository)
        {
            string path = Path.GetFullPath(
                Path.Combine(
                    Path.GetDirectoryName(typeof(NetCoreIntegrationTestFixture).Assembly.Location),
                    @"..\..\..\..\repos\" + Path.GetFileNameWithoutExtension(repository)));

            return path.Replace('\\', Path.DirectorySeparatorChar);
        }
    }
}
