using System.Collections.Generic;
using System.IO;
using System.Linq;
using Buildalyzer.Environment;
using NUnit.Framework;
using Shouldly;

namespace Buildalyzer.Tests.Integration
{
    [TestFixture]
    [NonParallelizable]
    public class SolutionWithProjectsFixture
    {
        private static readonly string[] SolutionFiles =
        {
            "NetCoreProjects.sln",
            "NetStandardProjects.sln",
            "NetCoreAndStandardProjects.sln"
        };

        [Test]
        public void DesignTimeLoadsSolutionLogs(
            [ValueSource(nameof(SolutionFiles))] string solutionFile)
        {
            // Given
            StringWriter log = new StringWriter();
            AnalyzerManager analyzerManager = GetAnalyzerManager(log);
            EnvironmentOptions options = new EnvironmentOptions
            {
                Preference = EnvironmentPreference.Core
            };
            string binlogPath = GetBinlogPath(solutionFile);
            string solutionPath = GetSolutionPath(solutionFile);

            // When
            /*
            if (File.Exists(binlogPath))
            {
                File.Delete(binlogPath);
            }
            Dictionary<string, string> environmentVariables = new Dictionary<string, string>
            {
                { EnvironmentVariables.DOTNET_CLI_UI_LANGUAGE, "en-US" },
                { EnvironmentVariables.MSBUILD_EXE_PATH, null },
                { MsBuildProperties.MSBuildExtensionsPath, null }
            };

            using (ProcessRunner processRunner = new ProcessRunner("dotnet", $"clean {solutionPath}", Path.GetDirectoryName(solutionPath), environmentVariables, null))
            {
                processRunner.Start();
                processRunner.WaitForExit(60000);
            }

            using (ProcessRunner processRunner = new ProcessRunner("dotnet", $"build {solutionPath} /bl:{binlogPath}", Path.GetDirectoryName(solutionPath), environmentVariables, null))
            {
                processRunner.Start();
                processRunner.WaitForExit(60000);
            }
            */
            AnalyzerResults results = analyzerManager.Analyze(binlogPath);

            // Then
            results.Count.ShouldBeGreaterThan(0, log.ToString());
            results.OverallSuccess.ShouldBeTrue(log.ToString());
            results.ShouldAllBe(x => x.Succeeded, log.ToString());
        }

        private static AnalyzerManager GetAnalyzerManager(StringWriter log)
        {
            AnalyzerManager analyzer = new AnalyzerManager(
                    new AnalyzerManagerOptions
                    {
                        LogWriter = log
                    });

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

        private static string GetSolutionPath(string file)
        {
            string path = Path.GetFullPath(
                Path.Combine(
                    Path.GetDirectoryName(typeof(SolutionWithProjectsFixture).Assembly.Location),
                    "..",
                    "..",
                    "..",
                    "..",
                    "projects",
                    file));

            return path.Replace('\\', Path.DirectorySeparatorChar);
        }

        private static string GetBinlogPath(string file)
        {
            string directory = Path.Combine(Path.GetDirectoryName(GetSolutionPath(file)), $"{Path.GetFileNameWithoutExtension(file)}.binlog");

            return directory.Replace('\\', Path.DirectorySeparatorChar);
        }
    }
}