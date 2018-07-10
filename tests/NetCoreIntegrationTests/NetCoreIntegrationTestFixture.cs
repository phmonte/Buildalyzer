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

        private static string[] _repositories =
        {
			//"https://github.com/AngleSharp/AngleSharp.git", contains portable project, can't build
			"https://github.com/autofac/Autofac.git",
			"https://github.com/AutoMapper/AutoMapper.git",
			//"https://github.com/MarimerLLC/csla.git", contains portable project, can't build
			"https://github.com/SixLabors/ImageSharp.git",
			//"https://github.com/moq/moq.git", build is broken, see https://github.com/moq/moq/issues/21
			//"https://github.com/JamesNK/Newtonsoft.Json.git", contains portable project, can't build
			//"https://github.com/nodatime/nodatime.git", fails when running Blazor commands due to design-time build and missing artifacts at E:\NuGet\microsoft.aspnetcore.blazor.build\0.4.0\targets\Blazor.MonoRuntime.targets(529,5)
			"https://github.com/JasonBock/Rocks.git",
			"https://github.com/dotnet/roslyn.git",
			"https://github.com/Reactive-Extensions/Rx.NET.git",
			"https://github.com/serilog/serilog.git",
			"https://github.com/Abc-Arbitrage/ZeroLog.git",
            "https://github.com/cake-build/cake"
        };

        [TestCaseSource(nameof(_repositories))]
        public void CompilesProject(string repository)
        {
            // Given
            string[] solutionFiles = CloneOrFetchRepository(repository);
            foreach (string solutionFile in solutionFiles)
            {
                StringWriter log = new StringWriter();
                AnalyzerManager manager = new AnalyzerManager(solutionFile, new AnalyzerManagerOptions
                {
                    LogWriter = log,
                    LoggerVerbosity = Verbosity
                });

                foreach (ProjectAnalyzer analyzer in manager.Projects.Values)
                {
                    // When
                    Console.WriteLine(analyzer.ProjectFile.Path);
                    if (BinaryLog)
                    {
                        analyzer.AddBinaryLogger($@"E:\Temp\{Path.GetFileNameWithoutExtension(solutionFile)}.{Path.GetFileNameWithoutExtension(analyzer.ProjectFile.Path)}.integration.core.binlog");
                    }
                    AnalyzerResults results = analyzer.Build();

                    // Then
                    results.Count.ShouldBeGreaterThan(0, log.ToString());
                    results.First().OverallSuccess.ShouldBeTrue(log.ToString());
                    results.First().ProjectInstance.ShouldNotBeNull(log.ToString());
                }
            }
        }

        private static string[] CloneOrFetchRepository(string repository)
        {
            string path = GetRepositoryPath(repository);
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
