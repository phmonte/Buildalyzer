using Microsoft.Build.Framework;
using Microsoft.CodeAnalysis.VisualBasic;

namespace Buildalyzer.Processors;

public sealed class VisualBasicCommandLineProcessor : RoslynBasedCommandLineProcessor
{
    [Pure]
    public override bool IsApplicable(BuildMessageEventArgs e)
        => e is TaskCommandLineEventArgs cmd
        && cmd.CommandLine is { Length: > 0 }
        && cmd.TaskName?.ToUpperInvariant() == "VBC";

    [Pure]
    public override string[]? SplitCommandLineIntoArguments(string commandLine)
        => SplitCommandLineIntoArguments(commandLine, "vbc.dll", "vbc.exe");

    protected override CompilerCommand Parse(string? baseDir, string? root, string[] args)
    {
        var arguments = VisualBasicCommandLineParser.Default.Parse(args, baseDir, root);
        var command = new VisualBasicCompilerCommand()
        {
            CommandLineArguments = arguments,
            PreprocessorSymbols = arguments.ParseOptions.PreprocessorSymbols.ToImmutableDictionary(),
        };
        return Enrich(command, arguments);
    }
}
