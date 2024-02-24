using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Buildalyzer;

public abstract record RoslynBasedCompilerCommand<TArguments> : CompilerCommand
    where TArguments : CommandLineArguments
{
    protected RoslynBasedCompilerCommand(TArguments arguments)
    {
        Arguments = Guard.NotNull(arguments);
        PreprocessorSymbolNames = Arguments.ParseOptions.PreprocessorSymbolNames.ToImmutableArray();
    }

    /// <summary>The Roslyn comppiler arguments.</summary>
    public TArguments Arguments { get; }

    /// <inheritdoc />
    public override CompilerLanguage Language => CompilerLanguage.VisualBasic;

    /// <inheritdoc />
    public override ImmutableArray<Diagnostic> Errors => Arguments.Errors;

    /// <inheritdoc />
    public override ImmutableArray<CommandLineSourceFile> SourceFiles => Arguments.SourceFiles;

    /// <inheritdoc />
    public override ImmutableArray<CommandLineSourceFile> AdditionalFiles => Arguments.AdditionalFiles;

    /// <inheritdoc />
    public override ImmutableArray<CommandLineSourceFile> EmbeddedFiles => Arguments.EmbeddedFiles;

    /// <inheritdoc />
    public override ImmutableArray<CommandLineAnalyzerReference> AnalyzerReferences => Arguments.AnalyzerReferences;

    /// <inheritdoc />
    public override ImmutableArray<string> PreprocessorSymbolNames { get; }

    /// <inheritdoc />
    public override ImmutableArray<string> ReferencePaths => Arguments.ReferencePaths;
}
