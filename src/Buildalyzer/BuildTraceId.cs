using Microsoft.Build.Framework;

namespace Buildalyzer;

public readonly record struct BuildTraceId(
    int ProjectContextId,
    int ProjectInstanceId)
{
    internal static BuildTraceId New(BuildEventContext c)
        => new(
            c.ProjectContextId,
            c.ProjectInstanceId);
}
