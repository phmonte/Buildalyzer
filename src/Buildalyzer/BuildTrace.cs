using Buildalyzer.IO;
using Microsoft.Build.Framework;

namespace Buildalyzer;

[DebuggerDisplay("{DebuggerDisplay}")]
public sealed class BuildTrace(BuildEventContext context)
{
    public BuildEventContext Context { get; } = context;

    public BuildTrace? Parent { get; internal set; }

    public IOPath ProjectFile { get; internal set; }

    public IOPath TargetFile { get; internal set; }

    public CompilerProperties GlobalProperties { get; internal set; } = new([]);

    public CompilerProperties Properties { get; internal set; } = new([]);

    public CompilerItemsCollection Items { get; internal set; } = new([]);

    public DateTime Timestamp { get; internal set; }

    public string? Message { get; internal set; }

    public MessageImportance Importance { get; internal set; }

    public TargetBuiltReason BuildReason { get; internal set; }

    public bool? Succeeded { get; internal set; }

    public void Update(Action<BuildTrace> update)
    {
        Guard.NotNull(update);

        lock (Locker)
        {
            update(this);
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private static readonly object Locker = new();

    private string DebuggerDisplay
        => $"{Context.ProjectContextId}, "
        + $"Properties = {Properties.Count}, "
        + $"Items = {Items.Count}, "
        + $"Message = {Message}";
}
