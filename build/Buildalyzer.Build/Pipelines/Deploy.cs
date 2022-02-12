using Statiq.Common;
using Statiq.Core;

namespace Buildalyzer.Build.Pipelines
{
    // This is just to trigger the publishing step on a deploy command
    public class Deploy : Pipeline, INamedPipeline
    {
        private readonly ProjectSet _projectSet;

        public Deploy(ProjectSet projectSet)
        {
            _projectSet = projectSet.ThrowIfNull(nameof(projectSet));

            Deployment = true;

            Dependencies.Add($"{nameof(Publish)}{projectSet.Name}");
        }

        public string PipelineName => $"{nameof(Deploy)}{_projectSet.Name}";
    }
}