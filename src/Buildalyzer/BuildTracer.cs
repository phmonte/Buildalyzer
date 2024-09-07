using System.Collections.Concurrent;
using Buildalyzer.IO;
using Microsoft.Build.Framework;

namespace Buildalyzer;

public sealed class BuildTracer : IDisposable
{
    private readonly IEventSource EventSource;

    public ConcurrentBag<BuildEventArgs> Events { get; } = [];

    public ConcurrentBag<KeyValuePair<BuildEventContext, IOPath>> Paths { get; } = [];
    public ConcurrentBag<KeyValuePair<BuildEventContext, CompilerProperties>> Properties { get; } = [];
    public ConcurrentBag<KeyValuePair<BuildEventContext, CompilerItemsCollection>> Items { get; } = [];

    public ConcurrentBag<KeyValuePair<BuildEventContext, CompilerCommand>> Commands { get; } = [];

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
        Events.Add(e);
    }

    private void TargetStarted(object? sender, TargetStartedEventArgs e)
    {
        Events.Add(e);
    }

    private void TargetFinished(object? sender, TargetFinishedEventArgs e)
    {
        Events.Add(e);
    }

    private void ProjectStarted(object? sender, ProjectStartedEventArgs e)
    {
        Events.Add(e);

        var path = IOPath.Parse(e.ProjectFile);
        var properties = CompilerProperties.FromDictionaryEntries(e.Properties);
        var items = CompilerItemsCollection.FromDictionaryEntries(e.Items);

        Paths.Add(KeyValuePair.Create(e.BuildEventContext, path));
        Properties.Add(KeyValuePair.Create(e.BuildEventContext, properties));
        Items.Add(KeyValuePair.Create(e.BuildEventContext, items));
    }

    private void MessageRaised(object? sender, BuildMessageEventArgs e)
    {
        Events.Add(e);

        var path = Paths.First(p => p.Key.ProjectInstanceId == e.BuildEventContext.ProjectInstanceId);

        var dir = path.Value.File().Directory;

        var command = e switch
        {
            TaskCommandLineEventArgs a when a.SenderName.IsMatch("Csc") => Compiler.CommandLine.Parse(dir, a.CommandLine, CompilerLanguage.CSharp),
            TaskCommandLineEventArgs a when a.SenderName.IsMatch("Vbc") => Compiler.CommandLine.Parse(dir, a.CommandLine, CompilerLanguage.VisualBasic),
           _ when e.SenderName.IsMatch("Fsc") => Compiler.CommandLine.Parse(dir, e.Message, CompilerLanguage.FSharp),
            _ => null,
        };

        if (command is { })
        {
            Commands.Add(KeyValuePair.Create(e.BuildEventContext, command));
        }
    }

    private void StatusEventRaised(object? sender, BuildStatusEventArgs e)
    {
        Events.Add(e);
    }

    private void BuildStarted(object? sender, BuildStartedEventArgs e)
    {
        Events.Add(e);
    }

    private void ErrorRaised(object? sender, BuildErrorEventArgs e)
    {
        Events.Add(e);
    }

    private void CustomEventRaised(object? sender, CustomBuildEventArgs e)
    {
        Events.Add(e);
    }

    private void TaskFinished(object? sender, TaskFinishedEventArgs e)
    {
        Events.Add(e);
    }

    private void ProjectFinished(object? sender, ProjectFinishedEventArgs e)
    {
        Events.Add(e);
    }

    private void BuildFinished(object? sender, BuildFinishedEventArgs e)
    {
        Events.Add(e);
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
