using Statiq.Common;
using Statiq.Core;

namespace Buildalyzer.Build.Pipelines
{
    public class Pack : Pipeline, INamedPipeline
    {
        private readonly ProjectSet _projectSet;

        public Pack(ProjectSet projectSet)
        {
            _projectSet = projectSet.ThrowIfNull(nameof(projectSet));

            Dependencies.Add($"{nameof(Test)}{projectSet.Name}");

            if (projectSet.ProjectSetDependencies is object)
            {
                foreach (string dependency in projectSet.ProjectSetDependencies)
                {
                    Dependencies.Add($"{nameof(Pack)}{dependency}");
                }
            }

            ProcessModules = new ModuleList
            {
                new ThrowExceptionIf(Config.ContainsSettings("DAVIDGLICK_CERTPASS").IsFalse(), "DAVIDGLICK_CERTPASS setting missing"),
                new ReadFiles(_projectSet.RootPath.Combine(_projectSet.SourceProjects).FullPath),
                new StartProcess("dotnet")
                    .WithArgument("pack")
                    .WithArgument("--no-build")
                    .WithArgument("--no-restore")
                    .WithVersions()
                    .WithArgument("-o", Config.FromContext(ctx => ctx.FileSystem.GetOutputPath(projectSet.Name).FullPath), true)
                    .WithArgument(Config.FromDocument(doc => doc.Source.FullPath), true)
                    .WithParallelExecution(false)
                    .LogOutput(),
                new ReadFiles(Config.FromContext(ctx => ctx.FileSystem.GetOutputPath($"{projectSet.Name}/*.nupkg").FullPath)),
                new StartProcess("nuget")
                    .WithArgument("sign")
                    .WithArgument(Config.FromDocument(doc => doc.Source.FullPath), true)
                    .WithArgument("-CertificatePath", Config.FromContext(ctx => ctx.FileSystem.GetRootFile("davidglick.pfx").Path.FullPath), true)
                    .WithArgument("-CertificatePassword", Config.FromSetting("DAVIDGLICK_CERTPASS"), true)
                    .WithArgument("-Timestamper", "http://timestamp.digicert.com", true)
                    .WithArgument("-NonInteractive")
                    .WithParallelExecution(false)
                    .HideArguments(true)
                    .LogOutput()
            };
        }

        public string PipelineName => $"{nameof(Pack)}{_projectSet.Name}";
    }
}