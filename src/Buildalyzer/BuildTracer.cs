using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework;

namespace Buildalyzer;

public sealed class BuildTracer : IDisposable
{
    private readonly IEventSource EventSource;

    private ConcurrentDictionary<BuildTraceId, BuildTrace> Tracer = [];

    public IReadOnlyCollection<BuildTrace> Traces => Tracer.Values.ToArray();

    public IReadOnlyCollection<BuildTraceEvent> Events => Tracer.Values.SelectMany(e => e.Events).ToArray();

    public IEnumerable<KeyValuePair<BuildEventContext, CompilerProperties>> Properties
        => Events
        .Where(e => e.Event is ProjectStartedEventArgs)
        .Select(e => KeyValuePair.Create(
            e.Context, CompilerProperties.FromDictionaryEntries(((ProjectStartedEventArgs)e.Event).Properties)));

    public IEnumerable<KeyValuePair<BuildEventContext, CompilerItemsCollection>> CompilerItemsCollections
        => Events
        .Where(e => e.Event is ProjectStartedEventArgs)
        .Select(e => KeyValuePair.Create(
            e.Context, CompilerItemsCollection.FromDictionaryEntries(((ProjectStartedEventArgs)e.Event).Properties)));

    public IEnumerable<KeyValuePair<BuildEventContext, CompilerCommand>> Commands
        => Events
            .Where(e => e.Event is ProjectStartedEventArgs)
            .Select(e => KeyValuePair.Create(
                e.Context, CompilerProperties.FromDictionaryEntries(((ProjectStartedEventArgs)e.Event).Properties)));

    public BuildTracer(IEventSource eventSource)
    {
        EventSource = Guard.NotNull(eventSource);

        EventSource.BuildStarted += BuildStarted;
        EventSource.ProjectStarted += ProjectStarted;
        EventSource.TaskStarted += TaskStarted;
        EventSource.TargetStarted += TargetStarted;

        EventSource.CustomEventRaised += CustomEventRaised;
        EventSource.MessageRaised += MessageRaised;
        EventSource.ErrorRaised += ErrorRaised;
        EventSource.StatusEventRaised += StatusEventRaised;

        EventSource.BuildFinished += BuildFinished;
        EventSource.ProjectFinished += ProjectFinished;
        EventSource.TaskFinished += TaskFinished;
        EventSource.TargetFinished += TargetFinished;
    }

    private void TaskStarted(object? sender, TaskStartedEventArgs e)
    {
        var trace = Trace(e);
        trace?.Add(e);
        Log(e);
    }

    private void TargetStarted(object? sender, TargetStartedEventArgs e)
    {
        var trace = Trace(e);
        trace?.Add(e);
        Log(e);
    }

    private void TargetFinished(object? sender, TargetFinishedEventArgs e)
    {
        var trace = Trace(e);
        trace?.Add(e);
        Log(e);
    }

    private void ProjectStarted(object? sender, ProjectStartedEventArgs e)
    {
        var trace = Trace(e);
        trace?.Add(e);
        Log(e);
    }

    private void MessageRaised(object? sender, BuildMessageEventArgs e)
    {
        var trace = Trace(e);
        trace?.Add(e);
        Log(e);
    }

    private void StatusEventRaised(object? sender, BuildStatusEventArgs e)
    {
        var trace = Trace(e);
        trace?.Add(e);
        Log(e);
    }

    private void BuildStarted(object? sender, BuildStartedEventArgs e)
    {
        var trace = Trace(e);
        trace?.Add(e);
        Log(e);
    }

    private void ErrorRaised(object? sender, BuildErrorEventArgs e)
    {
        var trace = Trace(e);
        trace?.Add(e);
        Log(e);
    }

    private void CustomEventRaised(object? sender, CustomBuildEventArgs e)
    {
        var trace = Trace(e);
        trace?.Add(e);
        Log(e);
    }

    private void TaskFinished(object? sender, TaskFinishedEventArgs e)
    {
        var trace = Trace(e);
        trace?.Add(e);
        Log(e);
    }

    private void ProjectFinished(object? sender, ProjectFinishedEventArgs e)
    {
        var trace = Trace(e);
        trace?.Add(e);
        Log(e);
    }

    private void BuildFinished(object? sender, BuildFinishedEventArgs e)
    {
        var trace = Trace(e);
        trace?.Add(e);
        Log(e);
    }

    private static void Log(object obj, [CallerMemberName] string? paramName = null)
    {
        //var json = System.Text.Json.JsonSerializer.Serialize(obj, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}: {paramName}");
    }

    private BuildTrace? Trace(BuildEventArgs e) => Trace(e.BuildEventContext);

    private BuildTrace? Trace(BuildStatusEventArgs e) => Trace(e.BuildEventContext);

    private BuildTrace? Trace(BuildEventContext? context)
    {
        if (context is null)
        {
            return null;
        }

        var id = BuildTraceId.New(context);

        if (!Tracer.TryGetValue(id, out var trace))
        {
            trace = new BuildTrace(id);
            Tracer[id] = trace;
        }
        return trace;
    }

    public void Dispose()
    {
        if (!Disposed)
        {
            EventSource.BuildStarted -= BuildStarted;
            EventSource.ProjectStarted -= ProjectStarted;
            EventSource.TaskStarted -= TaskStarted;
            EventSource.TargetStarted -= TargetStarted;

            EventSource.CustomEventRaised -= CustomEventRaised;
            EventSource.MessageRaised -= MessageRaised;
            EventSource.ErrorRaised -= ErrorRaised;
            EventSource.StatusEventRaised -= StatusEventRaised;

            EventSource.BuildFinished -= BuildFinished;
            EventSource.ProjectFinished -= ProjectFinished;
            EventSource.TaskFinished -= TaskFinished;
            EventSource.TargetFinished -= TargetFinished;
            Disposed = true;
        }
    }
    private bool Disposed;
}
