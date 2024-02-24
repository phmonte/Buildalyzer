#nullable enable

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Buildalyzer;

public abstract record CompilerCommand
{
    /// <summary>The compiler lanuague.</summary>
    public abstract CompilerLanguage Language { get; }

    /// <summary>The original text of the compiler command.</summary>
    public string Text { get; init; } = string.Empty;

    /// <inheritdoc  cref="CommandLineArguments.Errors" />
    public abstract ImmutableArray<Diagnostic> Errors { get; }

    /// <inheritdoc  cref="CommandLineArguments.SourceFiles" />
    public abstract ImmutableArray<CommandLineSourceFile> SourceFiles { get; }

    /// <inheritdoc  cref="CommandLineArguments.AdditionalFiles" />
    public abstract ImmutableArray<CommandLineSourceFile> AdditionalFiles { get; }

    /// <inheritdoc  cref="CommandLineArguments.EmbeddedFiles" />
    public abstract ImmutableArray<CommandLineSourceFile> EmbeddedFiles { get; }

    /// <inheritdoc  cref="CommandLineArguments.AnalyzerReferences" />
    public abstract ImmutableArray<CommandLineAnalyzerReference> AnalyzerReferences { get; }

    /// <inheritdoc  cref="ParseOptions.PreprocessorSymbolNames" />
    public abstract ImmutableArray<string> PreprocessorSymbolNames { get; }

    /// <inheritdoc  cref="CommandLineArguments.AnalyzerReferences" />
    public abstract ImmutableArray<string> ReferencePaths { get; }

    /// <inheritdoc />
    [Pure]
    public override string ToString() => Text ?? string.Empty;
}
