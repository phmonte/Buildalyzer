#nullable enable

using Microsoft.CodeAnalysis.VisualBasic;

namespace Buildalyzer;

public sealed record VisualBasicCompilerCommand : RoslynBasedCompilerCommand<VisualBasicCommandLineArguments>
{
    /// <inheritdoc />
    public override string Language => "VB.NET";

    /// <inheritdoc cref="VisualBasicParseOptions.PreprocessorSymbols" />
    public ImmutableDictionary<string, object>? PreprocessorSymbols { get; init; }
}
