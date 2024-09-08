namespace Buildalyzer;

/// <summary>The data received during a <see cref="Process" />.</summary>
[DebuggerDisplay("Output = {Output.Length}, Error = {Error.Length}")]
public sealed class ProcessData(
    ImmutableArray<string> output,
    ImmutableArray<string> error)
{
    /// <summary>The collected output of the process.</summary>
    public ImmutableArray<string> Output { get; } = output;

    /// <summary>The collected errors of the process.</summary>
    public ImmutableArray<string> Error { get; } = error;
}
