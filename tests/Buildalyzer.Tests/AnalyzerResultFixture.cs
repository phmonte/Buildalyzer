using NUnit.Framework;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Buildalyzer.Tests
{
    [TestFixture]
    public class AnalyzerResultFixture
    {
        [TestCase("foo.cs", new[] { "foo.cs" })]
        [TestCase("foo.cs bar.cs", new[] { "foo.cs", "bar.cs" })]
        [TestCase("\"foo.cs\"", new [] { "foo.cs" })]
        [TestCase("\"fizz buzz.cs\"", new[] { "fizz buzz.cs" })]
        [TestCase("foo.cs \"fizz buzz.cs\"", new[] { "foo.cs", "fizz buzz.cs" })]
        [TestCase("\"fizz - buzz.cs\"", new[] { "fizz - buzz.cs" })]  // #89
        [TestCase("\"f oo.cs\"", new[] { "f oo.cs" })]
        [TestCase("\" foo.cs\"", new[] { " foo.cs" })]
        [TestCase("\"foo.cs \"", new[] { "foo.cs " })]
        [TestCase("\"foo.cs\\\"\"", new[] { "foo.cs\\\"" })]
        [TestCase("\"f oo.cs\" bar.cs", new[] { "f oo.cs", "bar.cs" })]
        [TestCase("\" foo.cs\" bar.cs", new[] { " foo.cs", "bar.cs" })]
        [TestCase("\"foo.cs \" bar.cs", new[] { "foo.cs ", "bar.cs" })]
        [TestCase("\"foo.cs\\\"\" bar.cs", new[] { "foo.cs\\\"", "bar.cs" })]
        [TestCase("\"fo\\\"o.cs\" bar.cs", new[] { "fo\\\"o.cs", "bar.cs" })]
        [TestCase("bar.cs \"f oo.cs\"", new[] { "bar.cs", "f oo.cs" })]
        [TestCase("bar.cs \" foo.cs\"", new[] { "bar.cs", " foo.cs" })]
        [TestCase("bar.cs \"foo.cs\\\"\"", new[] { "bar.cs", "foo.cs\\\"" })]
        [TestCase("\"foo.cs\\\" bar.cs\"", new[] { "foo.cs\\\" bar.cs" })]
        public void ParsesCscCommandLineSourceFiles(string commandLine, string[] sourceFiles)
        {
            // Given
            commandLine = @"csc.exe "
                + @"/noconfig /unsafe- /checked- /nowarn:1701,1702,1701,1702,1701,1702 /nostdlib+ "
                + @"/errorreport:prompt /warn:4 /define:TRACE;DEBUG;NETCOREAPP;NETCOREAPP2_1 "
                + commandLine;

            // When
            List<(string, string)> result = AnalyzerResult.ProcessCscCommandLine(commandLine);

            // Then
            result.Where(x => x.Item1 == null).Select(x => x.Item2).Skip(1).ShouldBe(sourceFiles);
        }

        [TestCase("foo.cs bar.cs csc.dll", new[] { "foo.cs", "bar.cs" })]
        [TestCase("foo.cs csc.exe bar.cs", new[] { "foo.cs", "bar.cs" })]
        public void RemovesCscAssembliesFromSourceFiles(string commandLine, string[] sourceFiles)
        {
            // Given
            commandLine = @"csc.exe "
                + @"/noconfig /unsafe- /checked- /nowarn:1701,1702,1701,1702,1701,1702 /nostdlib+ "
                + @"/errorreport:prompt /warn:4 /define:TRACE;DEBUG;NETCOREAPP;NETCOREAPP2_1 "
                + commandLine;
            AnalyzerResult result = new AnalyzerResult(@"C:\Code\Project\project.csproj", null, null);

            // When
            result.ProcessCscCommandLine(commandLine, false);

            // Then
            result.SourceFiles.ShouldBe(sourceFiles.Select(x => $@"C:\Code\Project\{ x }"));
        }
    }
}
