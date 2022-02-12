using Microsoft.Extensions.Logging;
using Statiq.Common;
using Statiq.Core;

namespace Buildalyzer.Build.Pipelines
{
    public class GetVersions : Pipeline
    {
        public GetVersions()
        {
            ProcessModules = new ModuleList
            {
                new ExecuteConfig(Config.FromContext(async context =>
                {
                    MetadataItems metadata = new MetadataItems();
                    foreach (ProjectSet projectSet in ProjectSet.All)
                    {
                        string version = await context.GetVersionFromReleaseFileAsync(projectSet.RootPath);
                        context.LogInformation($"{projectSet.Name} version {version}");
                        metadata.Add(projectSet.Name, version);
                    }
                    return context.CreateDocument(metadata);
                }))
            };
        }
    }
}