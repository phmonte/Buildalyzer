using Microsoft.CodeAnalysis.CSharp;

namespace Buildalyzer;

public sealed record CSharpCompilerCommand : RoslynBasedCompilerCommand<CSharpCommandLineArguments>
{
    public CSharpCompilerCommand(CSharpCommandLineArguments arguments) 
        : base(arguments)
    {
    }
}