using Buildalyzer.IO;
using Microsoft.CodeAnalysis;

namespace Buildalyzer;

public sealed record FSharpCompilerCommand : CompilerCommand
{
    /// <inheritdoc />
    public override string Language => "F#";
}
