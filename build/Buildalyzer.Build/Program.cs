using System;
using System.Threading.Tasks;
using Statiq.App;
using Statiq.Common;

namespace Buildalyzer.Build;

public class Program
{
    private static readonly NormalizedPath ArtifactsFolder = "artifacts";

    public static async Task<int> Main(string[] args)
    {
        Bootstrapper bootstrapper = Bootstrapper
            .Factory
            .CreateDefaultWithout(args, DefaultFeatures.Pipelines)
            .ConfigureFileSystem(x =>
            {
                x.RootPath = new NormalizedPath(AppDomain.CurrentDomain.BaseDirectory).Parent.Parent.Parent.Parent.Parent;
                x.OutputPath = x.RootPath / ArtifactsFolder;
                x.InputPaths.Clear();
                x.InputPaths.Add(x.RootPath);
            })
            .ConfigureSettings(settings =>
            {
                settings.Add(Settings.IsBuildServer, settings.ContainsAnyKeys("GITHUB_ACTIONS", "TF_BUILD"));
                settings[Keys.CleanMode] = CleanMode.Full;
            })
            .AddPipeline<Pipelines.GetVersions>();

        foreach (ProjectSet projectSet in ProjectSet.All)
        {
            bootstrapper.AddPipeline(new Pipelines.Build(projectSet));
            bootstrapper.AddPipeline(new Pipelines.Test(projectSet));
            bootstrapper.AddPipeline(new Pipelines.Pack(projectSet));
            bootstrapper.AddPipeline(new Pipelines.Publish(projectSet));
            bootstrapper.AddPipeline(new Pipelines.Deploy(projectSet));
        }

        return await bootstrapper.RunAsync();
    }
}