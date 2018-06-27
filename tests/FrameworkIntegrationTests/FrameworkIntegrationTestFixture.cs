using Buildalyzer;
using LibGit2Sharp;
using Microsoft.Build.Execution;
using NUnit.Framework;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrameworkIntegrationTests
{

#if Is_Windows
    [TestFixture]
    public class FrameworkIntegrationTestFixture
    {
        private static string[] _repositories =
        {
            "https://github.com/AngleSharp/AngleSharp.git",
            "https://github.com/autofac/Autofac.git",
            "https://github.com/AutoMapper/AutoMapper.git",
            "https://github.com/MarimerLLC/csla.git",
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
                LogWriter = log
            });

            foreach (ProjectAnalyzer analyzer in manager.Projects.Values)
            {
                // When
                //analyzer.WithBinaryLog(Path.Combine(@"E:\Temp\", Path.ChangeExtension(Path.GetFileName(analyzer.ProjectFile.Path), "integration.binlog")));
                ProjectInstance projectInstance = analyzer.Build();

                // Then
                projectInstance.ShouldNotBeNull(log.ToString());
                analyzer.GetSourceFiles().Count.ShouldBeGreaterThan(0);
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
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                Repository.Clone(repository, path);
            }
            else
            {
                Repository repo = new Repository(path);
                foreach (Remote remote in repo.Network.Remotes)
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
                    Path.GetDirectoryName(typeof(FrameworkIntegrationTestFixture).Assembly.Location),
                    @"..\..\..\repos\" + Path.GetFileNameWithoutExtension(repository)));

            return path.Replace('\\', Path.DirectorySeparatorChar);
        }
    }
#endif
}
