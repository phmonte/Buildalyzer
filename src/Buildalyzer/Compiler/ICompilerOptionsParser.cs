using System.IO;
using Microsoft.Build.Framework;

namespace Buildalyzer;

/// <summary>
/// Parses compiler options from a string.
/// </summary>
public interface ICompilerOptionsParser
{
    /// <summary>
    /// The name of the language that this parser supports.
    /// </summary>
    public string Language { get; }

    /// <summary>
    /// Checks, if the given invocation is one for the language compiler that this parser supports.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="eventArgs">The build event arguments.</param>
    /// <param name="context">Contextual information for the parser.</param>
    /// <returns>True, if this parser supports the event and should be invoked.</returns>
    public bool IsSupportedInvocation(object sender, BuildMessageEventArgs eventArgs, CompilerOptionsContext context);

    /// <summary>
    /// Parses the compiler options from the given command line.
    /// </summary>
    /// <param name="commandLine">The command line to parse.</param>
    /// <param name="context">Contextual information for the parser.</param>
    /// <returns>The parsed <see cref="CompilerCommand"/>.</returns>
    CompilerCommand? Parse(string commandLine, CompilerOptionsContext context);
}

/// <summary>
/// Contextual information for parsing compiler options.
/// </summary>
public readonly struct CompilerOptionsContext
{
    /// <summary>
    /// True, if this is the first compiler invocation.
    /// False, if one is already found.
    /// </summary>
    public bool IsFirstInvocation { get; init; }

    /// <summary>
    /// True, if this is a call inside CoreCompile.
    /// </summary>
    public bool CoreCompile { get; init; }

    /// <summary>
    /// The base directory of the project.
    /// </summary>
    public DirectoryInfo? BaseDirectory { get; init; }

    /// <summary>
    /// The target stack.
    /// </summary>
    public IReadOnlyCollection<TargetStartedEventArgs> TargetStack { get; init; }
}
