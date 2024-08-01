using Microsoft.Build.Framework;
using Microsoft.CodeAnalysis.CSharp;

namespace Buildalyzer.Processors;

public sealed class CSharpCommandLineProcessor : RoslynBasedCommandLineProcessor
{
    [Pure]
    public override bool IsApplicable(BuildMessageEventArgs e)
        => e is TaskCommandLineEventArgs cmd
        && cmd.CommandLine is { Length: > 0 }
        && cmd.TaskName?.ToUpperInvariant() == "CSC";

    [Pure]
    public override string[]? SplitCommandLineIntoArguments(string commandLine)
       => SplitCommandLineIntoArguments(commandLine, "csc.dll", "csc.exe");

    [Pure]
    protected override CompilerCommand Parse(string? baseDir, string? root, string[] args)
    {
        var arguments = CSharpCommandLineParser.Default.Parse(args, baseDir, root);
        var command = new CSharpCompilerCommand()
        {
            CommandLineArguments = arguments,
        };
        return Enrich(command, arguments);
    }
}
