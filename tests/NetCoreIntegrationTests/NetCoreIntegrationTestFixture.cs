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
        private const bool BinaryLog = true;

        private static string[] _repositories =
        {
			// "https://github.com/AngleSharp/AngleSharp.git", contains portable project, can't build
			"https://github.com/autofac/Autofac.git",
			"https://github.com/AutoMapper/AutoMapper.git",
			//"https://github.com/MarimerLLC/csla.git", contains portable project, can't build
			"https://github.com/SixLabors/ImageSharp.git",
			"https://github.com/moq/moq.git",
			"https://github.com/JamesNK/Newtonsoft.Json.git",
			"https://github.com/nodatime/nodatime.git",
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
            string solutionFile = CloneOrFetchRepository(repository);
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
                    analyzer.AddBinaryLogger(Path.Combine(@"E:\Temp\", Path.ChangeExtension(Path.GetFileName(analyzer.ProjectFile.Path), ".integration.core.binlog")));
                }
                AnalyzerResults results = analyzer.Build();

                // Then
                results.Count.ShouldBeGreaterThan(0, log.ToString());
                results.First().OverallSuccess.ShouldBeTrue(log.ToString());
                results.First().ProjectInstance.ShouldNotBeNull(log.ToString());
                results.First().GetSourceFiles().Count.ShouldBeGreaterThan(0);
            }
        }

        private static ProjectAnalyzer GetProjectAnalyzer(string projectFile, StringWriter log) =>
            new AnalyzerManager(new AnalyzerManagerOptions
            {
                LogWriter = log
            })
            .GetProject(projectFile);

        private static string CloneOrFetchRepository(string repository)
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
            return Directory.GetFiles(path, "*.sln", SearchOption.AllDirectories)[0];
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
