using System;
using System.Threading.Tasks;
using Statiq.Common;

namespace Buildalyzer.Build;

public static class ExecutionContextExtensions
{
    public static async Task<string> GetVersionFromReleaseFileAsync(
        this IExecutionContext context, NormalizedPath rootPath)
    {
        context.ThrowIfNull(nameof(context));
        IFile releaseFile = context.FileSystem.GetInputFile(rootPath.Combine("RELEASE.md"));
        string content = await releaseFile.ReadAllTextAsync(context.CancellationToken);
        string firstLine = content.Trim().Split('\r', '\n', StringSplitOptions.RemoveEmptyEntries)[0];
        return firstLine.TrimStart('#').Trim();
    }
}