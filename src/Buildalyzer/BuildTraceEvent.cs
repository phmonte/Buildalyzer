using Buildalyzer.IO;
using Microsoft.Build.Framework;

namespace Buildalyzer;

[DebuggerDisplay("{DebuggerDisplay}")]
public sealed class BuildTraceEvent
{
    public DateTime Created { get; } = DateTime.Now;

    public BuildEventContext Context { get; init; }

    public string Name { get; init; }

    public object Event { get; init; }

    public string? Message => Event switch
    {
        BuildEventArgs a => a.Message,
        _ => null,
    };

    private string DebuggerDisplay
        => $"{Created:HH:mm:ss.ff}, "
        + $"Project = {Context.ProjectContextId}, "
        + $"Instance = {Context.ProjectInstanceId}, "
        + $"Name = {Name}, "
        + $"Event = {Event.GetType().Name}, "
        + $"Message = {Message}, "
        ;
    //+ $"Items = {Items.Count}, "
    //+ $"Message = {Message}";
}
