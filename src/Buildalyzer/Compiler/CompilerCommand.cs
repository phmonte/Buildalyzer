#nullable enable

using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using Microsoft.CodeAnalysis;

namespace Buildalyzer;

[DebuggerDisplay("{Language.Display()}: {Text}")]
public abstract record CompilerCommand
{
    /// <summary>The compiler lanuague.</summary>
    public abstract CompilerLanguage Language { get; }

    /// <summary>The original text of the compiler command.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>The parsed command line arguments.</summary>
    public ImmutableArray<string> Arguments { get; init; }

    /// <summary>The location of the used compiler.</summary>
    public FileInfo? CompilerLocation { get; init; }

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

    /// <inheritdoc  cref="CommandLineArguments.MetadataReferences" />
    public abstract ImmutableArray<CommandLineReference> MetadataReferences { get; }

    /// <inheritdoc />
    [Pure]
    public override string ToString() => Text ?? string.Empty;
}
