using Statiq.Common;

namespace Buildalyzer.Build;

public class ProjectSet
{
    public static readonly ProjectSet[] All = new ProjectSet[]
    {
        new ProjectSet(
            "Buildalyzer",
            NormalizedPath.Empty,
            "src/*/*.csproj",
            "tests/*/*.csproj",
            "daveaglick",
            "Buildalyzer")
    };

    public ProjectSet(
        string name,
        NormalizedPath rootPath,
        string sourceProjects,
        string testProjects,
        string gitHubOwner,
        string gitHubName,
        params string[] projectSetDependencies)
    {
        Name = name;
        RootPath = rootPath.IsNull ? NormalizedPath.Empty : rootPath;
        SourceProjects = sourceProjects;
        TestProjects = testProjects;
        GitHubOwner = gitHubOwner;
        GitHubName = gitHubName;
        ProjectSetDependencies = projectSetDependencies;
    }

    public string Name { get; }

    public NormalizedPath RootPath { get; }

    public string SourceProjects { get; }

    public string TestProjects { get; }

    public string GitHubOwner { get; }

    public string GitHubName { get; }

    public string[] ProjectSetDependencies { get; }
}