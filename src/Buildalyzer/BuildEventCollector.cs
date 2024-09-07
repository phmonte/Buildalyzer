using System.Collections.Concurrent;
using Microsoft.Build.Framework;

namespace Buildalyzer;

public sealed class BuildEventCollector : IDisposable
{
    public BuildEventCollector(IEventSource eventSource)
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

    public ImmutableArray<BuildEventArgs> Events => Bag.ToImmutableArray();

    private readonly IEventSource EventSource;

    private readonly ConcurrentBag<BuildEventArgs> Bag = [];

    private void Add(BuildEventArgs e) => Bag.Add(e);

    private void TaskStarted(object? sender, TaskStartedEventArgs e) => Add(e);

    private void TargetStarted(object? sender, TargetStartedEventArgs e) => Add(e);

    private void TargetFinished(object? sender, TargetFinishedEventArgs e) => Add(e);

    private void ProjectStarted(object? sender, ProjectStartedEventArgs e) => Add(e);

    private void MessageRaised(object? sender, BuildMessageEventArgs e) => Add(e);

    private void StatusEventRaised(object? sender, BuildStatusEventArgs e) => Add(e);

    private void BuildStarted(object? sender, BuildStartedEventArgs e) => Add(e);

    private void ErrorRaised(object? sender, BuildErrorEventArgs e) => Add(e);

    private void CustomEventRaised(object? sender, CustomBuildEventArgs e) => Add(e);

    private void TaskFinished(object? sender, TaskFinishedEventArgs e) => Add(e);

    private void ProjectFinished(object? sender, ProjectFinishedEventArgs e) => Add(e);

    private void BuildFinished(object? sender, BuildFinishedEventArgs e) => Add(e);

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
