using System.Collections.Concurrent;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

namespace Buildalyzer;

[DebuggerDisplay("Count = {Count}")]
[DebuggerTypeProxy(typeof(Diagnostics.CollectionDebugView<BuildEventArgs>))]
internal sealed class BuildEventArgsCollector : IReadOnlyCollection<BuildEventArgs>, IDisposable
{
    public BuildEventArgsCollector(EventArgsDispatcher server)
    {
        Server = server;
        Server.AnyEventRaised += EventRaised;
    }

    /// <inheritdoc />
    public int Count => Bag.Count;

    /// <inheritdoc />
    public IEnumerator<BuildEventArgs> GetEnumerator() => Bag.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void EventRaised(object? sender, BuildEventArgs e) => Bag.Add(e);

    private readonly EventArgsDispatcher Server;

    private readonly ConcurrentBag<BuildEventArgs> Bag = [];

    public void Dispose()
    {
        if (!Disposed)
        {
            Server.AnyEventRaised -= EventRaised;
            Disposed = true;
        }
    }

    private bool Disposed;
}
