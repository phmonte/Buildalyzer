using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.VisualBasic;

namespace Buildalyzer;

/// <summary>
/// A parser for the vbc compiler options (Roslyn).
/// </summary>
public sealed class VbcOptionsParser : ICompilerOptionsParser
{
    /// <summary>
    /// A singleton instance of the parser.
    /// </summary>
    public static VbcOptionsParser Instance { get; } = new VbcOptionsParser();

    public string Language => "VB.NET";

    private VbcOptionsParser()
    {
    }

    public bool IsSupportedInvocation(object sender, BuildMessageEventArgs eventArgs, CompilerOptionsContext context) =>
        eventArgs is TaskCommandLineEventArgs cmd
        && string.Equals(cmd.TaskName, "Vbc", StringComparison.OrdinalIgnoreCase);

    public CompilerCommand? Parse(string commandLine, CompilerOptionsContext context)
    {
        var tokens = RoslynParser.SplitCommandLineIntoArguments(commandLine, "vbc.dll", "vbc.exe")
                  ?? throw new FormatException("Commandline could not be parsed.");
        var location = new FileInfo(tokens[0]);
        var args = tokens[1..];

        var arguments = VisualBasicCommandLineParser.Default.Parse(args, context.BaseDirectory?.ToString(), location.Directory?.ToString());
        var command = new VisualBasicCompilerCommand()
        {
            CommandLineArguments = arguments,
            PreprocessorSymbols = arguments.ParseOptions.PreprocessorSymbols.ToImmutableDictionary(),
            Text = commandLine,
            CompilerLocation = location,
            Arguments = args.ToImmutableArray(),
        };
        return RoslynParser.Enrich(command, arguments);
    }
}
