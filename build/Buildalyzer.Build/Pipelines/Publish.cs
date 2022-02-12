using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Octokit;
using Statiq.Common;
using Statiq.Core;

namespace Buildalyzer.Build.Pipelines
{
    public class Publish : Pipeline, INamedPipeline
    {
        private readonly ProjectSet _projectSet;

        public Publish(ProjectSet projectSet)
        {
            _projectSet = projectSet.ThrowIfNull(nameof(projectSet));

            // Don't publish unless told to (I.e. during deployment as a dependency of the corresponding DeployXyz pipeline)
            ExecutionPolicy = ExecutionPolicy.Manual;

            Dependencies.Add($"{nameof(Pack)}{projectSet.Name}");

            if (projectSet.ProjectSetDependencies is object)
            {
                foreach (string dependency in projectSet.ProjectSetDependencies)
                {
                    Dependencies.Add($"{nameof(Publish)}{dependency}");
                }
            }

            ProcessModules = new ModuleList
            {
                new ThrowExceptionIf(Config.ContainsSettings("DAVEAGLICK_NUGET_API_KEY").IsFalse(), "DAVEAGLICK_NUGET_API_KEY setting missing"),
                new ThrowExceptionIf(Config.ContainsSettings("GITHUB_TOKEN").IsFalse(), "GITHUB_TOKEN setting missing"),
                new ReadFiles(Config.FromContext(ctx => ctx.FileSystem.GetOutputPath($"{projectSet.Name}/*.nupkg").FullPath)),
                new StartProcess("nuget")
                    .WithArgument("push")
                    .WithArgument(Config.FromDocument(doc => doc.Source.FullPath), true)
                    .WithArgument("-ApiKey", Config.FromSetting("DAVEAGLICK_NUGET_API_KEY"), true)
                    .WithArgument("-Source", "https://api.nuget.org/v3/index.json", true)
                    .WithArgument("-SkipDuplicate")
                    .WithParallelExecution(false)
                    .LogOutput(),
                new ExecuteConfig(Config.FromContext(async ctx =>
                {
                    // Get release notes
                    IFile releaseNotesFile = ctx.FileSystem.GetInputFile(projectSet.RootPath.Combine("RELEASE.md"));
                    string releaseNotes = await releaseNotesFile.ReadAllTextAsync();
                    string[] lines = releaseNotes.Trim().Split("\n#", StringSplitOptions.RemoveEmptyEntries)[0].Trim().Split("\n").Select(x => x.Trim()).ToArray();
                    string version = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries)[1];
                    string notes = string.Join(Environment.NewLine, lines.Skip(1).SkipWhile(x => string.IsNullOrWhiteSpace(x)));

                    ctx.LogInformation("Version " + version);
                    ctx.LogInformation("Notes " + Environment.NewLine + notes);

                    // Add release to GitHub
                    GitHubClient github = new GitHubClient(new ProductHeaderValue("Statiq"))
                    {
                        Credentials = new Credentials(ctx.GetString("GITHUB_TOKEN"))
                    };

                    try
                    {
                        Release release = await github.Repository.Release.Create(projectSet.GitHubOwner, projectSet.GitHubName, new NewRelease("v" + version)
                        {
                            Name = version,
                            Body = string.Join(Environment.NewLine, notes),
                            TargetCommitish = "main",
                            Prerelease = version.Contains('-')
                        });
                        ctx.LogInformation($"Added {projectSet.Name} release {release.Name} to GitHub");
                    }
                    catch (Exception e)
                    {
                        ctx.LogWarning($"Could not add {projectSet.Name} release to GitHub: {e.Message}");
                    }
                }))
            };
        }

        public string PipelineName => $"{nameof(Publish)}{_projectSet.Name}";
    }
}