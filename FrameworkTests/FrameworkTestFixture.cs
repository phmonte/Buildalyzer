using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Buildalyzer;
using NUnit.Framework;
using Shouldly;

namespace FrameworkTests
{
    [TestFixture]
    public class FrameworkTestFixture
    {
        [Test]
        public void AnalyzesLegacyFrameworkProject()
        {
            // Given
            string projectPath = Path.GetFullPath(
                Path.Combine(
                    Path.GetDirectoryName(typeof(FrameworkTestFixture).Assembly.Location),
                    @"..\..\..\LegacyFrameworkProject\LegacyFrameworkProject.csproj"));

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
                    Path.GetDirectoryName(typeof(FrameworkTestFixture).Assembly.Location),
                    @"..\..\..\SdkNetCoreProject\SdkNetCoreProject.csproj"));

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
                    Path.GetDirectoryName(typeof(FrameworkTestFixture).Assembly.Location),
                    @"..\..\..\SdkNetStandardProject\SdkNetStandardProject.csproj"));

            // When
            Analyzer analyzer = Analyzer.Analyze(projectPath);

            // Then
            analyzer.SourceFiles.ShouldContain(x => x.EndsWith("Class1.cs"));
        }
    }
}
