using Buildalyzer.IO;
using Microsoft.CodeAnalysis;

namespace Buildalyzer.Processors;

public abstract class RoslynBasedCommandLineProcessor : CommandLineProcessor
{
    [Pure]
    internal static CompilerCommand Enrich(CompilerCommand command, CommandLineArguments arguments)
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
    protected static string[]? SplitCommandLineIntoArguments(string? commandLine, params string[] execs)
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

    [Pure]
    private static IOPath AsIOPath(CommandLineAnalyzerReference file) => IOPath.Parse(file.FilePath);

    [Pure]
    private static IOPath AsIOPath(CommandLineSourceFile file) => IOPath.Parse(file.Path);
}
