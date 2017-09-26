using System;
using System.IO;
using Buildalyzer;
using NUnit.Framework;
using Shouldly;

namespace NetCoreTests
{
    [TestFixture]
    public class NetCoreTestFixture
    {
        [Test]
        public void AnalyzesLegacyFrameworkProject()
        {
            // Given
            string projectPath = Path.GetFullPath(
                Path.Combine(
                    Path.GetDirectoryName(typeof(NetCoreTestFixture).Assembly.Location),
                    @"..\..\..\..\LegacyFrameworkProject\LegacyFrameworkProject.csproj"));

            // When
            Analyzer analyzer = Analyzer.Analyze(projectPath);

            // Then
            analyzer.SourceFiles.ShouldContain(x => x.EndsWith("Class1.cs"));
        }

        [Test]
        public void AnalyzesSdkNetCoreProject()
        {
            // Given
            string projectPath = Path.GetFullPath(
                Path.Combine(
                    Path.GetDirectoryName(typeof(NetCoreTestFixture).Assembly.Location),
                    @"..\..\..\..\SdkNetCoreProject\SdkNetCoreProject.csproj"));

            // When
            Analyzer analyzer = Analyzer.Analyze(projectPath);

            // Then
            analyzer.SourceFiles.ShouldContain(x => x.EndsWith("Class1.cs"));
        }

        [Test]
        public void AnalyzesSdkNetStandardProject()
        {
            // Given
            string projectPath = Path.GetFullPath(
                Path.Combine(
                    Path.GetDirectoryName(typeof(NetCoreTestFixture).Assembly.Location),
                    @"..\..\..\..\SdkNetStandardProject\SdkNetStandardProject.csproj"));

            // When
            Analyzer analyzer = Analyzer.Analyze(projectPath);

            // Then
            analyzer.SourceFiles.ShouldContain(x => x.EndsWith("Class1.cs"));
        }
    }
}
