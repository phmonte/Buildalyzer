using System.Collections.Immutable;
using FSharp.Compiler.CodeAnalysis;
using FSharp.Compiler.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.FSharp.Collections;

namespace Buildalyzer;

public sealed record FSharpCompilerCommand : CompilerCommand
{
    public FSharpCompilerCommand(FSharpParsingOptions options, FSharpList<FSharpDiagnostic> diagnostics)
    {
        Options = Guard.NotNull(options);
        Errors = diagnostics.Select(AsDiagnostic).ToImmutableArray();
        SourceFiles = Options.SourceFiles.Select(AsCommandLineSourceFile).ToImmutableArray();
        PreprocessorSymbolNames = Options.ConditionalDefines.ToImmutableArray();
    }

    private static Diagnostic AsDiagnostic(FSharpDiagnostic d)
        => Diagnostic.Create(
            id: d.ErrorNumberText,
            category: d.Subcategory,
            message: d.Message,
            severity: DiagnosticSeverity.Error, // d.Severity,
            defaultSeverity:  DiagnosticSeverity.Error, ///d.Severity,
            isEnabledByDefault: true, // TODO
            warningLevel: 3, //TODO
            location: Location.Create(d.FileName, default, default)); // TODO: map location

    [Pure]
    private static CommandLineSourceFile AsCommandLineSourceFile(string path) => new(path, isScript: false); // TODO: resolve when it is a script.

    public FSharpParsingOptions Options { get; }

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

    public override ImmutableArray<CommandLineAnalyzerReference> AnalyzerReferences => throw new NotImplementedException();

    public override ImmutableArray<string> PreprocessorSymbolNames { get; }

    public override ImmutableArray<string> ReferencePaths => throw new NotImplementedException();
}
