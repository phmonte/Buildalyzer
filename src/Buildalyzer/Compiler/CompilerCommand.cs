#nullable enable

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Buildalyzer;

public abstract record CompilerCommand
{
    public abstract CompilerLanguage Language { get; }

    public abstract ImmutableArray<Diagnostic> Errors { get; }

    public abstract ImmutableArray<CommandLineSourceFile> SourceFiles { get; }

    public abstract ImmutableArray<CommandLineSourceFile> AdditionalFiles { get; }

    public abstract ImmutableArray<CommandLineSourceFile> EmbeddedFiles { get; }

    public abstract ImmutableArray<CommandLineAnalyzerReference> AnalyzerReferences { get; }

    public abstract ImmutableArray<string> PreprocessorSymbolNames { get; }

    public abstract ImmutableArray<string> ReferencePaths { get; }

    public string Text { get; init; } = string.Empty;

    /// <inheritdoc />
    [Pure]
    public override string ToString() => Text ?? string.Empty;
}
