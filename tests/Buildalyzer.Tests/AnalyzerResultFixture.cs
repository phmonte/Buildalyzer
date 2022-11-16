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
        private const string CscOptions = "/noconfig /unsafe- /checked- /nowarn:1701,1702,1701,1702,1701,1702 /nostdlib+ /errorreport:prompt /warn:4 /define:TRACE;DEBUG;NETCOREAPP;NETCOREAPP2_1 ";
        private const string VbcOptions = "/noconfig /imports:Microsoft.VisualBasic,System,System.Collections,System.Collections.Generic,System.Diagnostics,System.Linq,System.Xml.Linq,System.Threading.Tasks /optioncompare:Binary /optionexplicit+ /optionstrict:custom /nowarn:41999,42016,42017,42018,42019,42020,42021,42022,42032,42036 /nosdkpath /optioninfer+ /nostdlib /errorreport:prompt /rootnamespace:ConsoleApp21 /highentropyva+ /define:CONFIG=Debug,DEBUG=-1 /warnaserror+:NU1605 ";

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
                + CscOptions
                + commandLine;

            // When
            AnalyzerResult.ProcessedCommandLine result = AnalyzerResult.ProcessCscCommandLine(commandLine);

            // Then
            result.Command.ShouldBe(commandLine);
            result.FileName.ShouldBe(Path.Combine("/", "Fizz", "Buzz", "csc.exe"));
            result.Arguments.ShouldBe(CscOptions.Split(' ', StringSplitOptions.RemoveEmptyEntries).Concat(sourceFiles));
            result.ProcessedArguments.Where(x => x.Item1 == null).Select(x => x.Item2).Skip(1).ShouldBe(sourceFiles);
        }

        /*
        [TestCase("foo.cs bar.cs csc.dll", new[] { "foo.cs", "bar.cs" })]
        [TestCase("foo.cs csc.exe bar.cs", new[] { "foo.cs", "bar.cs" })]
        [TestCase("foo.cs bar.cs", new[] { "foo.cs", "bar.cs" })]
        public void RemovesCscAssembliesFromSourceFiles(string input, string[] sourceFiles)
        {
            // Given
            string commandLine = Path.Combine("/", "Fizz", "Buzz", "csc.exe") + " "
                + CscOptions
                + input;
            string projectFilePath = Path.Combine("/", "Code", "Project", "project.csproj");
            AnalyzerResult result = new AnalyzerResult(projectFilePath, null, null);

            // When
            result.ProcessCscCommandLine(commandLine, false);

            // Then
            result.Command.ShouldBe(commandLine);
            result.CompilerFilePath.ShouldBe(Path.Combine("/", "Fizz", "Buzz", "csc.exe"));
            result.CompilerArguments.ShouldBe(CscOptions.Split(' ', StringSplitOptions.RemoveEmptyEntries).Concat(input.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
            result.SourceFiles.ShouldBe(sourceFiles.Select(x => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectFilePath), x))));
        }
        */

        [Test]
        public void ParsesCscCommandLineWithAliasReference()
        {
            // Given
            string commandLine = Path.Combine("/", "Fizz", "Buzz", "csc.exe")
                + @" /reference:Data1=""C:\Program Files(x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Data.dll""";

            // When
            AnalyzerResult.ProcessedCommandLine result = AnalyzerResult.ProcessCscCommandLine(commandLine);

            // Then
            result.Command.ShouldBe(commandLine);
            result.FileName.ShouldBe(Path.Combine("/", "Fizz", "Buzz", "csc.exe"));
            result.Arguments.ShouldBe(new[] { @"/reference:Data1=C:\Program Files(x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Data.dll" });
            result.ProcessedArguments.Count.ShouldBe(2);
            result.ProcessedArguments.Where(x => x.Item1 == "reference")
                .Select(x => x.Item2)
                .Single()
                .ShouldBe(@"Data1=C:\Program Files(x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Data.dll");
        }

        /*
        [TestCase("C:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\Professional\\MSBuild\\Current\\Bin\\Roslyn\\csc.exe /noconfig")]
        [TestCase("/one two/three/csc.dll /noconfig")]
        public void TreatsCscCommandAsSingleArg(string commandLine)
        {
            // Given, When
            AnalyzerResult.ProcessedCommandLine result = AnalyzerResult.ProcessCscCommandLine(commandLine);

            // Then
            result.Command.ShouldBe(commandLine);
            result.FileName.ShouldBe(commandLine.Replace(" /noconfig", string.Empty));
            result.Arguments.ShouldBe(new[] { "/noconfig" });
            result.ProcessedArguments.Count.ShouldBe(2);
        }
        */

        [TestCase("foo.vb", new[] { "foo.vb" })]
        [TestCase("foo.vb bar.vb", new[] { "foo.vb", "bar.vb" })]
        [TestCase("\"foo.vb\"", new[] { "foo.vb" })]
        [TestCase("\"fizz buzz.vb\"", new[] { "fizz buzz.vb" })]
        [TestCase("foo.vb \"fizz buzz.vb\"", new[] { "foo.vb", "fizz buzz.vb" })]
        [TestCase("\"fizz - buzz.vb\"", new[] { "fizz - buzz.vb" })] // #89
        [TestCase("\"f oo.vb\"", new[] { "f oo.vb" })]
        [TestCase("\" foo.vb\"", new[] { " foo.vb" })]
        [TestCase("\"foo.vb \"", new[] { "foo.vb " })]
        [TestCase("\"foo.vb\\\"\"", new[] { "foo.vb\"" })]
        [TestCase("\"f oo.vb\" bar.vb", new[] { "f oo.vb", "bar.vb" })]
        [TestCase("\" foo.vb\" bar.vb", new[] { " foo.vb", "bar.vb" })]
        [TestCase("\"foo.vb \" bar.vb", new[] { "foo.vb ", "bar.vb" })]
        [TestCase("\"foo.vb\\\"\" bar.vb", new[] { "foo.vb\"", "bar.vb" })]
        [TestCase("\"fo\\\"o.vb\" bar.vb", new[] { "fo\"o.vb", "bar.vb" })]
        [TestCase("bar.vb \"f oo.vb\"", new[] { "bar.vb", "f oo.vb" })]
        [TestCase("bar.vb \" foo.vb\"", new[] { "bar.vb", " foo.vb" })]
        [TestCase("bar.vb \"foo.vb\\\"\"", new[] { "bar.vb", "foo.vb\"" })]
        [TestCase("\"foo.vb\\\" bar.vb\"", new[] { "foo.vb\" bar.vb" })]
        public void ParsesVbcCommandLineSourceFiles(string input, string[] sourceFiles)
        {
            // Given
            string commandLine = Path.Combine("/", "Fizz", "Buzz", "vbc.exe") + " "
                + VbcOptions
                + input;

            // When
            AnalyzerResult.ProcessedCommandLine result = AnalyzerResult.ProcessCommandLine(commandLine, "vbc.");

            // Then
            result.Command.ShouldBe(commandLine);
            result.FileName.ShouldBe(Path.Combine("/", "Fizz", "Buzz", "vbc.exe"));
            result.Arguments.ShouldBe(VbcOptions.Split(' ', StringSplitOptions.RemoveEmptyEntries).Concat(sourceFiles));
            result.ProcessedArguments.Where(x => x.Item1 == null).Select(x => x.Item2).Skip(1).ShouldBe(sourceFiles);
        }

        [TestCase("foo.vb bar.vb vbc.dll", new[] { "foo.vb", "bar.vb" })]
        [TestCase("foo.vb vbc.exe bar.vb", new[] { "foo.vb", "bar.vb" })]
        [TestCase("foo.vb bar.vb", new[] { "foo.vb", "bar.vb" })]
        public void RemovesVbcAssembliesFromSourceFiles(string commandLine, string[] sourceFiles)
        {
            // Given
            commandLine = Path.Combine("/", "Fizz", "Buzz", "vbc.exe") + " "
                + VbcOptions
                + commandLine;
            string projectFilePath = Path.Combine("/", "Code", "Project", "project.vbproj");
            AnalyzerResult result = new AnalyzerResult(projectFilePath, null, null);

            // When
            result.ProcessVbcCommandLine(commandLine);

            // Then
            result.SourceFiles.ShouldBe(sourceFiles.Select(x => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectFilePath), x))));
        }

        [Test]
        public void ParsesVbcCommandLineWithAliasReference()
        {
            // Given
            string commandLine = Path.Combine("/", "Fizz", "Buzz", "vbc.exe")
                + @" /reference:Data1=""C:\Program Files(x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Data.dll""";

            // When
            AnalyzerResult.ProcessedCommandLine result = AnalyzerResult.ProcessCommandLine(commandLine, "vbc.");

            // Then
            result.Command.ShouldBe(commandLine);
            result.FileName.ShouldBe(Path.Combine("/", "Fizz", "Buzz", "vbc.exe"));
            result.Arguments.ShouldBe(new[] { @"/reference:Data1=C:\Program Files(x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Data.dll" });
            result.ProcessedArguments.Count.ShouldBe(2);
            result.ProcessedArguments.Where(x => x.Item1 == "reference")
                .Select(x => x.Item2)
                .Single()
                .ShouldBe(@"Data1=C:\Program Files(x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Data.dll");
        }

        [TestCase("C:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\Professional\\MSBuild\\Current\\Bin\\Roslyn\\vbc.exe /noconfig")]
        [TestCase("/one two/three/vbc.dll /noconfig")]
        public void TreatsVbcCommandAsSingleArg(string commandLine)
        {
            AnalyzerResult.ProcessedCommandLine result = AnalyzerResult.ProcessCommandLine(commandLine, "vbc.");
            result.ProcessedArguments.Count.ShouldBe(2);
        }

        [Test]
        public void ParseVbcCommandLineWithMultipleReferences()
        {
            // Given
            string commandLine = Path.Combine("/", "Fizz", "Buzz", "vbc.exe")
                                 + @" /reference:""C:\Program Files(x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Data.dll"", ""C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\Facades\System.Collections.dll""";

            string projectFilePath = Path.Combine("/", "Code", "Project", "project.vbproj");
            AnalyzerResult result = new AnalyzerResult(projectFilePath, null, null);

            // When
            result.ProcessVbcCommandLine(commandLine);

            // Then
            result.References.Count().ShouldBe(2);
        }
    }
}
