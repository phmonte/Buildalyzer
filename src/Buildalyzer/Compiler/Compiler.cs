#nullable enable

using System.ComponentModel;
using FSharp.Compiler.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.FSharp.Collections;

namespace Buildalyzer;

public static class Compiler
{
    public static class CommandLine
    {
        [Pure]
        public static string[]? Tokenize(string commandline, CompilerLanguage language)
        {
            var args = CommandLineParser.SplitCommandLineIntoArguments(commandline, removeHashComments: true).ToArray();

            return language switch
            {
                CompilerLanguage.CSharp => Split(args, "csc.dll", "csc.exe"),
                CompilerLanguage.VisualBasic => Split(args, "vbc.dll", "vbc.exe"),
                CompilerLanguage.FSharp => Split(args, "fsc.dll", "fsc.exe"),
                _ => throw new NotSupportedException($"The {language} language is not supported."),
            };

            static string[]? Split(string[] args, params string[] execs)
            {
                foreach (var exec in execs)
                {
                    for (var i = 0; i < args.Length - 1; i++)
                    {
                        if (args[i].EndsWith(exec, StringComparison.OrdinalIgnoreCase))
                        {
                            return args[(i + 1)..];
                        }
                    }
                }
                return null;
            }
        }

        [Pure]
        public static CompilerCommand Parse(string commandline, CompilerLanguage language)
        {
            var tokens = Tokenize(commandline, language) ?? throw new FormatException("Commandline could not be parsed.");

            return Parse(tokens[0], tokens[1..], language) with
            {
                Text = commandline,
            };

            CompilerCommand Parse(string root, string[] args, CompilerLanguage language)
            {
                return language switch
                {
                    CompilerLanguage.CSharp => new CSharpCompilerCommand(CSharpCommandLineParser.Default.Parse(args, ".", root)),
                    CompilerLanguage.VisualBasic => new VisualBasicCompilerCommand(VisualBasicCommandLineParser.Default.Parse(args, ".", root)),
                    CompilerLanguage.FSharp => FSharp(root, args),
                    _ => throw new NotSupportedException($"The {language} language is not supported."),
                };
            }
        }

        private static CompilerCommand FSharp(string root, string[] args)
        {
            var checker = FSharpChecker.Instance;
            var result = checker.GetParsingOptionsFromCommandLineArgs(ListModule.OfArray(args), isInteractive: true, isEditing: false);
            return new FSharpCompilerCommand(result.Item1, result.Item2);
        }
    }
}
