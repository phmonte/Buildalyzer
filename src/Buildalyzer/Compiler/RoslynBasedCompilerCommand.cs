using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Buildalyzer;

public abstract record RoslynBasedCompilerCommand<TArguments> : CompilerCommand
    where TArguments : CommandLineArguments
{
    protected RoslynBasedCompilerCommand(TArguments arguments)
    {
        CommandLineArguments = Guard.NotNull(arguments);
        PreprocessorSymbolNames = CommandLineArguments.ParseOptions.PreprocessorSymbolNames.ToImmutableArray();
    }

    /// <summary>The Roslyn comppiler arguments.</summary>
    public TArguments CommandLineArguments { get; }

    /// <inheritdoc />
    public override ImmutableArray<Diagnostic> Errors => CommandLineArguments.Errors;

    /// <inheritdoc />
    public override ImmutableArray<CommandLineSourceFile> SourceFiles => CommandLineArguments.SourceFiles;

    /// <inheritdoc />
    public override ImmutableArray<CommandLineSourceFile> AdditionalFiles => CommandLineArguments.AdditionalFiles;

    /// <inheritdoc />
    public override ImmutableArray<CommandLineSourceFile> EmbeddedFiles => CommandLineArguments.EmbeddedFiles;

    /// <inheritdoc />
    public override ImmutableArray<CommandLineAnalyzerReference> AnalyzerReferences => CommandLineArguments.AnalyzerReferences;

    /// <inheritdoc />
    public override ImmutableArray<string> PreprocessorSymbolNames { get; }

    /// <inheritdoc />
    public override ImmutableArray<CommandLineReference> MetadataReferences => CommandLineArguments.MetadataReferences;
}
