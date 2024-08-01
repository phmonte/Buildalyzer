#nullable enable

using Buildalyzer.IO;
using Microsoft.CodeAnalysis;

namespace Buildalyzer;

internal static class RoslynParser
{
    public static TCommand Enrich<TCommand>(TCommand command, CommandLineArguments arguments)
        where TCommand : CompilerCommand
        => command with
        {
            AnalyzerReferences = arguments.AnalyzerReferences.Select(AsIOPath).ToImmutableArray(),
            AnalyzerConfigPaths = arguments.AnalyzerConfigPaths.Select(IOPath.Parse).ToImmutableArray(),
            MetadataReferences = arguments.MetadataReferences.Select(m => m.Reference).ToImmutableArray(),
            PreprocessorSymbolNames = arguments.ParseOptions.PreprocessorSymbolNames.ToImmutableArray(),

            SourceFiles = arguments.SourceFiles.Select(AsIOPath).ToImmutableArray(),
            AdditionalFiles = arguments.AdditionalFiles.Select(AsIOPath).ToImmutableArray(),
            EmbeddedFiles = arguments.EmbeddedFiles.Select(AsIOPath).ToImmutableArray(),
        };

    [Pure]
    internal static IOPath AsIOPath(CommandLineAnalyzerReference file) => IOPath.Parse(file.FilePath);

    [Pure]
    internal static IOPath AsIOPath(CommandLineSourceFile file) => IOPath.Parse(file.Path);

    [Pure]
    public static string[]? SplitCommandLineIntoArguments(string? commandLine, params string[] execs)
        => Split(CommandLineParser.SplitCommandLineIntoArguments(commandLine ?? string.Empty, removeHashComments: true).ToArray(), execs);

    [Pure]
    private static string[]? Split(string[] args, string[] execs)
    {
        foreach (var exec in execs)
        {
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i].EndsWith(exec, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i..];
                }
            }
        }
        return null;
    }
}
