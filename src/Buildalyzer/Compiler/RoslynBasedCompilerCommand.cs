using System.Collections.Immutable;
using Buildalyzer.IO;
using Microsoft.CodeAnalysis;

namespace Buildalyzer;

public abstract record RoslynBasedCompilerCommand<TArguments> : CompilerCommand
    where TArguments : CommandLineArguments
{
    protected RoslynBasedCompilerCommand(TArguments arguments)
    {
        CommandLineArguments = Guard.NotNull(arguments);
        PreprocessorSymbolNames = CommandLineArguments.ParseOptions.PreprocessorSymbolNames.ToImmutableArray();

        SourceFiles = CommandLineArguments.SourceFiles.Select(AsIOPath).ToImmutableArray();
        AdditionalFiles = CommandLineArguments.AdditionalFiles.Select(AsIOPath).ToImmutableArray();
        EmbeddedFiles = CommandLineArguments.EmbeddedFiles.Select(AsIOPath).ToImmutableArray();

        IOPath AsIOPath(CommandLineSourceFile file) => IOPath.Parse(file.Path);
    }

    /// <summary>The Roslyn comppiler arguments.</summary>
    public TArguments CommandLineArguments { get; }

    /// <inheritdoc />
    public override ImmutableArray<Diagnostic> Errors => CommandLineArguments.Errors;

    /// <inheritdoc />
    public override ImmutableArray<IOPath> SourceFiles { get; }

    /// <inheritdoc />
    public override ImmutableArray<IOPath> AdditionalFiles { get; }

    /// <inheritdoc />
    public override ImmutableArray<IOPath> EmbeddedFiles { get; }

    /// <inheritdoc />
    public override ImmutableArray<CommandLineAnalyzerReference> AnalyzerReferences => CommandLineArguments.AnalyzerReferences;

    /// <inheritdoc />
    public override ImmutableArray<string> PreprocessorSymbolNames { get; }

    /// <inheritdoc />
    public override ImmutableArray<CommandLineReference> MetadataReferences => CommandLineArguments.MetadataReferences;
}
