using Microsoft.CodeAnalysis;

namespace Buildalyzer;

public sealed record FSharpCompilerCommand : CompilerCommand
{
    public FSharpCompilerCommand(
        IEnumerable<string> sourceFiles,
        IEnumerable<string> preprocessorSymbolNames,
        IEnumerable<string> metadataReferences)
    {
        SourceFiles = sourceFiles.Select(AsCommandLineSourceFile).ToImmutableArray();
        PreprocessorSymbolNames = preprocessorSymbolNames.ToImmutableArray();
        MetadataReferences = metadataReferences.Select(AsMetadataReference).ToImmutableArray();
    }

    [Pure]
    public static CommandLineReference AsMetadataReference(string r)
        => new CommandLineReference(r, new MetadataReferenceProperties(MetadataImageKind.Assembly));

    [Pure]
    private static CommandLineSourceFile AsCommandLineSourceFile(string path) => new(path, isScript: false); // TODO: resolve when it is a script.

    /// <inheritdoc />
    public override CompilerLanguage Language => CompilerLanguage.FSharp;

    /// <inheritdoc />
    public override ImmutableArray<Diagnostic> Errors { get; }

    /// <inheritdoc />
    public override ImmutableArray<CommandLineSourceFile> SourceFiles { get; }

    /// <inheritdoc />
    /// <remarks>
    /// Not supported by F#.
    /// </remarks>
    public override ImmutableArray<CommandLineSourceFile> AdditionalFiles { get; }

    public override ImmutableArray<CommandLineSourceFile> EmbeddedFiles => throw new NotImplementedException();

    /// <inheritdoc />
    /// <remarks>
    /// Not supported by F#.
    /// </remarks>
    public override ImmutableArray<CommandLineAnalyzerReference> AnalyzerReferences { get; }

    public override ImmutableArray<string> PreprocessorSymbolNames { get; }

    /// <inheritdoc />
    public override ImmutableArray<CommandLineReference> MetadataReferences { get; }
}
