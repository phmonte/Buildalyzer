using Buildalyzer.Build.Pipelines;
using Statiq.Common;
using Statiq.Core;

namespace Buildalyzer.Build;

public static class StartProcessExtensions
{
    public static StartProcess WithVersions(this StartProcess startProcess)
    {
        startProcess.ThrowIfNull(nameof(startProcess));

        // Add the versions of all project sets prefixed with their name
        // This way projects can reference other projects with the correct versioning
        // The project file should have something like this:
        // <Version Condition="'$(ProjectSetNameVersion)' == ''">1.0.0</Version>
        // <Version Condition="'$(ProjectSetNameVersion)' != ''">$(ProjectSetNameVersion)</Version>
        foreach (ProjectSet project in ProjectSet.All)
        {
            startProcess = startProcess.WithArgument(Config.FromContext(context =>
                $"-p:{project.Name}Version=\"{context.Outputs.FromPipeline(nameof(GetVersions))[0].GetString(project.Name)}\""));
        }

        return startProcess;
    }
}