using System.IO;
using Buildalyzer.IO;
using Microsoft.Build.Framework;
using Microsoft.CodeAnalysis.VisualBasic;

namespace Buildalyzer;

/// <summary>
/// A parser for the fsc compiler options (F#).
/// </summary>
public sealed class FscOptionsParser : ICompilerOptionsParser
{
    /// <summary>
    /// A singleton instance of the parser.
    /// </summary>
    public static FscOptionsParser Instance { get; } = new FscOptionsParser();

    public string Language => "F#";

    private FscOptionsParser()
    {
    }

    public bool IsSupportedInvocation(object sender, BuildMessageEventArgs eventArgs, CompilerOptionsContext context) =>
        eventArgs.SenderName?.Equals("Fsc", StringComparison.OrdinalIgnoreCase) == true
        && !string.IsNullOrWhiteSpace(eventArgs.Message)
        && context.TargetStack.Any(x => x.TargetName == "CoreCompile")
        && context.IsFirstInvocation;

    public CompilerCommand? Parse(string commandLine, CompilerOptionsContext context)
    {
        var tokens = FSharpParser.SplitCommandLineIntoArguments(commandLine)
                  ?? throw new FormatException("Commandline could not be parsed.");

        var location = new FileInfo(tokens[0]);
        var args = tokens[1..];

        var sourceFiles = args.Where(a => a[0] != '-').Select(IOPath.Parse);
        var preprocessorSymbolNames = args.Where(a => a.StartsWith("--define:")).Select(a => a[9..]);
        var metadataReferences = args.Where(a => a.StartsWith("-r:")).Select(a => a[3..]);

        return new FSharpCompilerCommand()
        {
            MetadataReferences = metadataReferences.ToImmutableArray(),
            PreprocessorSymbolNames = preprocessorSymbolNames.ToImmutableArray(),
            SourceFiles = sourceFiles.ToImmutableArray(),
        };
    }
}
