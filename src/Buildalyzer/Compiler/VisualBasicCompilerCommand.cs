using Microsoft.CodeAnalysis.VisualBasic;

namespace Buildalyzer;

public sealed record VisualBasicCompilerCommand : RoslynBasedCompilerCommand<VisualBasicCommandLineArguments>
{
    public VisualBasicCompilerCommand(VisualBasicCommandLineArguments arguments)
       : base(arguments)
    {
    }

    /// <inheritdoc />
    public override CompilerLanguage Language => CompilerLanguage.VisualBasic;
}
