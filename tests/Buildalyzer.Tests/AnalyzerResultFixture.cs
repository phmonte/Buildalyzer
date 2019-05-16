using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Shouldly;

namespace Buildalyzer.Tests
{
    [TestFixture]
    public class AnalyzerResultFixture
    {
        [TestCase("foo.cs", new[] { "foo.cs" })]
        [TestCase("foo.cs bar.cs", new[] { "foo.cs", "bar.cs" })]
        [TestCase("\"foo.cs\"", new[] { "foo.cs" })]
        [TestCase("\"fizz buzz.cs\"", new[] { "fizz buzz.cs" })]
        [TestCase("foo.cs \"fizz buzz.cs\"", new[] { "foo.cs", "fizz buzz.cs" })]
        [TestCase("\"fizz - buzz.cs\"", new[] { "fizz - buzz.cs" })] // #89
        [TestCase("\"f oo.cs\"", new[] { "f oo.cs" })]
        [TestCase("\" foo.cs\"", new[] { " foo.cs" })]
        [TestCase("\"foo.cs \"", new[] { "foo.cs " })]
        [TestCase("\"foo.cs\\\"\"", new[] { "foo.cs\"" })]
        [TestCase("\"f oo.cs\" bar.cs", new[] { "f oo.cs", "bar.cs" })]
        [TestCase("\" foo.cs\" bar.cs", new[] { " foo.cs", "bar.cs" })]
        [TestCase("\"foo.cs \" bar.cs", new[] { "foo.cs ", "bar.cs" })]
        [TestCase("\"foo.cs\\\"\" bar.cs", new[] { "foo.cs\"", "bar.cs" })]
        [TestCase("\"fo\\\"o.cs\" bar.cs", new[] { "fo\"o.cs", "bar.cs" })]
        [TestCase("bar.cs \"f oo.cs\"", new[] { "bar.cs", "f oo.cs" })]
        [TestCase("bar.cs \" foo.cs\"", new[] { "bar.cs", " foo.cs" })]
        [TestCase("bar.cs \"foo.cs\\\"\"", new[] { "bar.cs", "foo.cs\"" })]
        [TestCase("\"foo.cs\\\" bar.cs\"", new[] { "foo.cs\" bar.cs" })]
        public void ParsesCscCommandLineSourceFiles(string commandLine, string[] sourceFiles)
        {
            // Given
            commandLine = Path.Combine("/", "Fizz", "Buzz", "csc.exe") + " "
                + "/noconfig /unsafe- /checked- /nowarn:1701,1702,1701,1702,1701,1702 /nostdlib+ /errorreport:prompt /warn:4 /define:TRACE;DEBUG;NETCOREAPP;NETCOREAPP2_1 "
                + commandLine;

            // When
            List<(string, string)> result = AnalyzerResult.ProcessCscCommandLine(commandLine);

            // Then
            result.Where(x => x.Item1 == null).Select(x => x.Item2).Skip(1).ShouldBe(sourceFiles);
        }

        [TestCase("foo.cs bar.cs csc.dll", new[] { "foo.cs", "bar.cs" })]
        [TestCase("foo.cs csc.exe bar.cs", new[] { "foo.cs", "bar.cs" })]
        [TestCase("foo.cs bar.cs", new[] { "foo.cs", "bar.cs" })]
        public void RemovesCscAssembliesFromSourceFiles(string commandLine, string[] sourceFiles)
        {
            // Given
            commandLine = Path.Combine("/", "Fizz", "Buzz", "csc.exe") + " "
                + "/noconfig /unsafe- /checked- /nowarn:1701,1702,1701,1702,1701,1702 /nostdlib+ /errorreport:prompt /warn:4 /define:TRACE;DEBUG;NETCOREAPP;NETCOREAPP2_1 "
                + commandLine;
            string projectFilePath = Path.Combine("/", "Code", "Project", "project.csproj");
            AnalyzerResult result = new AnalyzerResult(projectFilePath, null, null);

            // When
            result.ProcessCscCommandLine(commandLine, false);

            // Then
            result.SourceFiles.ShouldBe(sourceFiles.Select(x => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectFilePath), x))));
        }

        [Test]
        public void ParsesCscCommandLineWithAliasReference()
        {
            // Given
            string commandLine = Path.Combine("/", "Fizz", "Buzz", "csc.exe")
                + @" /reference:Data1=""C:\Program Files(x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Data.dll""";

            // When
            List<(string, string)> result = AnalyzerResult.ProcessCscCommandLine(commandLine);

            // Then
            result.Count.ShouldBe(2);
            result.Where(x => x.Item1 == "reference")
                .Select(x => x.Item2)
                .Single()
                .ShouldBe(@"Data1=C:\Program Files(x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Data.dll");
        }

        [TestCase("C:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\Professional\\MSBuild\\Current\\Bin\\Roslyn\\csc.exe /noconfig")]
        [TestCase("/one two/three/csc.dll /noconfig")]
        public void TreatsCscCommandAsSingleArg(string commandLine)
        {
            // Given, When
            List<(string, string)> result = AnalyzerResult.ProcessCscCommandLine(commandLine);

            // Then
            result.Count.ShouldBe(2);
        }
    }
}
