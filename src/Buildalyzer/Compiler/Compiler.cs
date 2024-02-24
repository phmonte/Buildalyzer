#nullable enable

using System.IO;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.VisualBasic;

namespace Buildalyzer;

public static class Compiler
{
    public static class CommandLine
    {
        [Pure]
        public static string[]? SplitCommandLineIntoArguments(string? commandLine, CompilerLanguage language) => language switch
        {
            CompilerLanguage.CSharp => RoslynCommandLineParser.SplitCommandLineIntoArguments(commandLine, "csc.dll", "csc.exe"),
            CompilerLanguage.VisualBasic => RoslynCommandLineParser.SplitCommandLineIntoArguments(commandLine, "vbc.dll", "vbc.exe"),
            CompilerLanguage.FSharp => FSharpCommandLineParser.SplitCommandLineIntoArguments(commandLine),
            _ => throw new NotSupportedException($"The {language} language is not supported."),
        };

        [Pure]
        public static CompilerCommand Parse(DirectoryInfo? baseDir, string commandLine, CompilerLanguage language)
        {
            var tokens = SplitCommandLineIntoArguments(commandLine, language) ?? throw new FormatException("Commandline could not be parsed.");
            var location = new FileInfo(tokens[0]);
            var args = tokens[1..];

            return Parse(baseDir?.ToString(), location.Directory?.ToString(), args, language) with
            {
                Text = commandLine,
                CompilerLocation = location,
                Arguments = args.ToImmutableArray(),
            };

            CompilerCommand Parse(string? baseDir, string? root, string[] args, CompilerLanguage language)
            {
                return language switch
                {
                    CompilerLanguage.CSharp => new CSharpCompilerCommand(CSharpCommandLineParser.Default.Parse(args, baseDir, root)),
                    CompilerLanguage.VisualBasic => new VisualBasicCompilerCommand(VisualBasicCommandLineParser.Default.Parse(args, baseDir, root)),
                    CompilerLanguage.FSharp => FSharpCommandLineParser.Parse(args),
                    _ => throw new NotSupportedException($"The {language} language is not supported."),
                };
            }
        }
    }
}
