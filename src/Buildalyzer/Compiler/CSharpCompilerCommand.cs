using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Buildalyzer;

public sealed record CSharpCompilerCommand : CompilerCommand
{
    public CSharpCompilerCommand(CSharpCommandLineArguments arguments)
    {
        Arguments = Guard.NotNull(arguments);
        PreprocessorSymbolNames = Arguments.ParseOptions.PreprocessorSymbolNames.ToImmutableArray();
    }

    public CSharpCommandLineArguments Arguments { get; }

    public override CompilerLanguage Language => CompilerLanguage.CSharp;

    public override ImmutableArray<Diagnostic> Errors => Arguments.Errors;

    public override ImmutableArray<CommandLineSourceFile> SourceFiles => Arguments.SourceFiles;

    public override ImmutableArray<CommandLineSourceFile> AdditionalFiles => Arguments.AdditionalFiles;

    public override ImmutableArray<CommandLineSourceFile> EmbeddedFiles => Arguments.EmbeddedFiles;

    public override ImmutableArray<CommandLineAnalyzerReference> AnalyzerReferences => Arguments.AnalyzerReferences;

    public override ImmutableArray<string> PreprocessorSymbolNames { get; }

    public override ImmutableArray<string> ReferencePaths => Arguments.ReferencePaths;
}