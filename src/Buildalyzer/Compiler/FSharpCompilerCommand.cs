using Buildalyzer.IO;
using Microsoft.CodeAnalysis;

namespace Buildalyzer;

public sealed record FSharpCompilerCommand : CompilerCommand
{
    public FSharpCompilerCommand(
        IEnumerable<string> sourceFiles,
        IEnumerable<string> preprocessorSymbolNames,
        IEnumerable<string> metadataReferences)
    {
        SourceFiles = sourceFiles.Select(AsIOPath).ToImmutableArray();
        PreprocessorSymbolNames = preprocessorSymbolNames.ToImmutableArray();
        MetadataReferences = metadataReferences.Select(AsMetadataReference).ToImmutableArray();

        IOPath AsIOPath(string file) => IOPath.Parse(file);

        CommandLineReference AsMetadataReference(string r) => new(r, new MetadataReferenceProperties(MetadataImageKind.Assembly));
    }

    /// <inheritdoc />
    public override CompilerLanguage Language => CompilerLanguage.FSharp;

    /// <inheritdoc />
    public override ImmutableArray<Diagnostic> Errors { get; }

    /// <inheritdoc />
    public override ImmutableArray<IOPath> SourceFiles { get; }

    /// <inheritdoc />
    /// <remarks>
    /// Not supported by F#.
    /// </remarks>
    public override ImmutableArray<IOPath> AdditionalFiles { get; }

    public override ImmutableArray<IOPath> EmbeddedFiles => throw new NotImplementedException();

    /// <inheritdoc />
    /// <remarks>
    /// Not supported by F#.
    /// </remarks>
    public override ImmutableArray<CommandLineAnalyzerReference> AnalyzerReferences { get; }

    public override ImmutableArray<string> PreprocessorSymbolNames { get; }

    /// <inheritdoc />
    public override ImmutableArray<CommandLineReference> MetadataReferences { get; }
}
