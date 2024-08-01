using System.IO;
using Microsoft.Build.Framework;

namespace Buildalyzer.Processors;

public abstract class CommandLineProcessor
{
    [Pure]
    public abstract bool IsApplicable(BuildMessageEventArgs e);

    [Pure]
    public abstract string[]? SplitCommandLineIntoArguments(string commandLine);

    [Pure]
    public CompilerCommand Parse(string commandLine, DirectoryInfo? baseDir)
    {
        var tokens = SplitCommandLineIntoArguments(commandLine) ?? throw new FormatException("Commandline could not be parsed.");
        var location = new FileInfo(tokens[0]);
        var args = tokens[1..];

        return Parse(baseDir?.ToString(), location.Directory?.ToString(), args) with
        {
            Text = commandLine,
            CompilerLocation = location,
            Arguments = args.ToImmutableArray(),
        };
    }

    [Pure]
    protected abstract CompilerCommand Parse(string? baseDir, string? root, string[] args);
}
