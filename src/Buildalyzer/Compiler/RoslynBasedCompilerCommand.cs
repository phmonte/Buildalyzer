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

    public TArguments Arguments { get; }

    public override CompilerLanguage Language => CompilerLanguage.VisualBasic;

    public override ImmutableArray<Diagnostic> Errors => Arguments.Errors;

    public override ImmutableArray<CommandLineSourceFile> SourceFiles => Arguments.SourceFiles;

    public override ImmutableArray<CommandLineSourceFile> AdditionalFiles => Arguments.AdditionalFiles;

    public override ImmutableArray<CommandLineSourceFile> EmbeddedFiles => Arguments.EmbeddedFiles;

    public override ImmutableArray<CommandLineAnalyzerReference> AnalyzerReferences => Arguments.AnalyzerReferences;

    public override ImmutableArray<string> PreprocessorSymbolNames { get; }

    public override ImmutableArray<string> ReferencePaths => Arguments.ReferencePaths;
}
