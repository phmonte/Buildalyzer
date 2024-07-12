using System.IO;
using Buildalyzer.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Buildalyzer;

/// <summary>
/// A parser for the csc compiler options (Roslyn).
/// </summary>
public sealed class CscOptionsParser : ICompilerOptionsParser
{
    /// <summary>
    /// A singleton instance of the parser.
    /// </summary>
    public static CscOptionsParser Instance { get; } = new CscOptionsParser();

    public string Language => "C#";

    private CscOptionsParser()
    {
    }

    public bool IsSupportedInvocation(object sender, BuildMessageEventArgs eventArgs, CompilerOptionsContext context) =>
        eventArgs is TaskCommandLineEventArgs cmd
        && string.Equals(cmd.TaskName, "Csc", StringComparison.OrdinalIgnoreCase);

    public CompilerCommand? Parse(string commandLine, CompilerOptionsContext context)
    {
        if (string.IsNullOrWhiteSpace(commandLine) || (!context.IsFirstInvocation && !context.CoreCompile))
        {
            return null;
        }

        var tokens = RoslynParser.SplitCommandLineIntoArguments(commandLine, "csc.dll", "csc.exe")
                  ?? throw new FormatException("Commandline could not be parsed.");
        var location = new FileInfo(tokens[0]);
        var args = tokens[1..];

        var arguments = CSharpCommandLineParser.Default.Parse(args, context.BaseDirectory?.ToString(), location.Directory?.ToString());
        var command = new CSharpCompilerCommand()
        {
            CommandLineArguments = arguments,
        };
        return RoslynParser.Enrich(command, arguments);
    }
}
