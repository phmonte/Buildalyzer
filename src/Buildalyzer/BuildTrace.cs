using System.Runtime.CompilerServices;
using Microsoft.Build.Framework;

namespace Buildalyzer;

[DebuggerDisplay("{DebuggerDisplay}")]
public sealed class BuildTrace(BuildTraceId id)
{
    public BuildTraceId Id { get; } = id;

    public BuildTrace? Parent { get; internal set; }

    public List<BuildTraceEvent> Events { get; } = [];

    public IEnumerable<BuildTraceEvent> ProjectStarted => Events.Where(e => e.Event is ProjectStartedEventArgs);

    public BuildTrace Add(BuildEventArgs? e, [CallerMemberName] string? paramName = null)
    {
        if (e is { })
        {
            lock (Locker)
            {
                Events.Add(new() { Context = e.BuildEventContext, Event = e, Name = paramName! });
            }
        }

        return this;
    }

    public BuildTrace Add(BuildStatusEventArgs? e, [CallerMemberName] string? paramName = null)
    {
        if (e is { })
        {
            lock (Locker)
            {
                Events.Add(new() { Context = e.BuildEventContext, Event = e, Name = paramName! });
            }
        }

        return this;
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private static readonly object Locker = new();

    private string DebuggerDisplay
        => $"{Id}, "
        + $"Events = {Events.Count}, "
        + $"ProjectStarted = {ProjectStarted.Count()}"
        ;
        //+ $"Items = {Items.Count}, "
        //+ $"Message = {Message}";
}
