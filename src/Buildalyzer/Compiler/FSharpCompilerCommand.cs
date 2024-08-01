namespace Buildalyzer;

public sealed record FSharpCompilerCommand : CompilerCommand
{
    /// <inheritdoc />
    public override CompilerLanguage Language => CompilerLanguage.FSharp;
}
