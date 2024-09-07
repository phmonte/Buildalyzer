using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Buildalyzer.IO;
using Microsoft.Build.Framework;

namespace Buildalyzer;

public sealed class BuildTracer : IDisposable
{
    private readonly IEventSource EventSource;

    private ConcurrentDictionary<BuildEventContext, BuildTrace> Tracer = [];

    public IReadOnlyCollection<BuildTrace> Traces => Tracer.Values.ToArray();

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
        Log(e);
    }

    private void TargetStarted(object? sender, TargetStartedEventArgs e)
    {
        var trace = Trace(e);
        trace.Update(t =>
        {
            t.BuildReason = e.BuildReason;
            t.Message = e.Message;
            t.ProjectFile = IOPath.Parse(e.ProjectFile);
            t.TargetFile = IOPath.Parse(e.TargetFile);
            t.Timestamp = e.Timestamp;
        });
        Log(e);
    }

    private void TargetFinished(object? sender, TargetFinishedEventArgs e)
    {
        var trace = Trace(e);
        Log(e);
    }

    private void ProjectStarted(object? sender, ProjectStartedEventArgs e)
    {
        var trace = Trace(e);
        trace.Update(t =>
        {
            t.Parent = e.ParentProjectBuildEventContext is { } p ? Trace(p) : null;
            t.Message = e.Message;
            t.ProjectFile = IOPath.Parse(e.ProjectFile);
            t.GlobalProperties = CompilerProperties.FromDictionaryEntries(e.GlobalProperties);
            t.Properties = CompilerProperties.FromDictionaryEntries(e.Properties);
            t.Items = CompilerItemsCollection.FromDictionaryEntries(e.Items);
            t.Timestamp = e.Timestamp;
        });
        Log(e);
    }

    private void MessageRaised(object? sender, BuildMessageEventArgs e)
    {
        var trace = Trace(e);
        trace.Update(t =>
        {
            t.Message = e.Message;
            t.ProjectFile = IOPath.Parse(e.ProjectFile);
            t.Importance = e.Importance;
            t.Timestamp = e.Timestamp;
        });
        Log(e);
    }

    private void StatusEventRaised(object? sender, BuildStatusEventArgs e)
    {
        var trace = Trace(e);
        Log(e);
    }

    private void BuildStarted(object? sender, BuildStartedEventArgs e)
    {
        var trace = Trace(e);
        Log(e);
    }

    private void ErrorRaised(object? sender, BuildErrorEventArgs e)
    {
        var trace = Trace(e);
        Log(e);
    }

    private void CustomEventRaised(object? sender, CustomBuildEventArgs e)
    {
        var trace = Trace(e);
        Log(e);
    }

    private void TaskFinished(object? sender, TaskFinishedEventArgs e)
    {
        var trace = Trace(e);
        Log(e);
    }

    private void ProjectFinished(object? sender, ProjectFinishedEventArgs e)
    {
        var trace = Trace(e);
        trace.Update(t =>
        {
            t.Succeeded = e.Succeeded;
        });
        Log(e);
    }

    private void BuildFinished(object? sender, BuildFinishedEventArgs e)
    {
        if (e.BuildEventContext is { } ctx)
        {
            var trace = Trace(ctx);
        }
        Log(e);
    }

    private static void Log(object obj, [CallerMemberName] string? paramName = null)
    {
        //var json = System.Text.Json.JsonSerializer.Serialize(obj, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}: {paramName}");
    }

    private BuildTrace Trace(BuildEventArgs e) => Trace(e.BuildEventContext);

    private BuildTrace Trace(BuildStatusEventArgs e) => Trace(e.BuildEventContext);

    private BuildTrace Trace(BuildEventContext? context)
    {
        context = Guard.NotNull(context);

        if (!Tracer.TryGetValue(context, out var trace))
        {
            trace = new BuildTrace(context);
            Tracer[context] = trace;
        }
        return trace;
    }

    public void Dispose()
    {
        if (!disposed)
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
            disposed = true;
        }
    }
    private bool disposed;
}

public sealed class ProjectTrace
{
    public ProjectFinishedTrace? Finished { get; internal set; }
}
public sealed record ProjectFinishedTrace(bool Succeeded, string Message);