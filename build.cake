// The following environment variables need to be set for Publish target:
// BUILDALYZER_NUGET_KEY
// BUILDALYZER_GITHUB_TOKEN
// BUILDALYZER_NETLIFY_TOKEN

#tool nuget:?package=Wyam&version=1.4.1
#addin nuget:?package=Cake.Wyam&version=1.4.1
#addin "Octokit"
#addin "NetlifySharp"
#addin "Newtonsoft.Json"

using Octokit;
using NetlifySharp;

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

var isLocal = BuildSystem.IsLocalBuild;
var isRunningOnAppVeyor = AppVeyor.IsRunningOnAppVeyor;
var isPullRequest = AppVeyor.Environment.PullRequest.IsPullRequest;
var buildNumber = AppVeyor.Environment.Build.Number;

var releaseNotes = ParseReleaseNotes("./ReleaseNotes.md");

var version = releaseNotes.Version.ToString();
var semVersion = version + (isLocal ? string.Empty : string.Concat("-build-", buildNumber));
var msBuildSettings = new DotNetCoreMSBuildSettings()
    .WithProperty("Version", semVersion)
    .WithProperty("AssemblyVersion", version)
    .WithProperty("FileVersion", version);

var buildDir = Directory("./src/Buildalyzer/bin") + Directory(configuration);
var releaseDir = Directory("./build");
var docsDir = Directory("./docs");

var zipFile = "Buildalyzer-v" + semVersion + ".zip";
var zipPath = releaseDir + File(zipFile);

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(context =>
{
    Information("Building version {0} of Buildalyzer.", semVersion);
});

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Description("Cleans the build directory.")
    .Does(() =>
    {
        CleanDirectories(new DirectoryPath[] { buildDir });
    });

Task("Restore")
    .Description("Restores all NuGet packages.")
    .IsDependentOn("Clean")
    .Does(() =>
    {        
        DotNetCoreRestore("./Buildalyzer.sln", new DotNetCoreRestoreSettings
        {
            MSBuildSettings = msBuildSettings
        });
    });

Task("Build")
    .Description("Builds the solution.")
    .IsDependentOn("Restore")
    .Does(() =>
    {
        DotNetCoreBuild("./Buildalyzer.sln", new DotNetCoreBuildSettings
        {
            Configuration = configuration,
            NoRestore = true,
            MSBuildSettings = msBuildSettings
        });
    });

Task("Test")
    .Description("Runs all tests.")
    .IsDependentOn("Build")
    .Does(() =>
    {
        DotNetCoreTestSettings testSettings = new DotNetCoreTestSettings()
        {
            NoBuild = true,
            NoRestore = true,
            Configuration = configuration
        };
        if (isRunningOnAppVeyor)
        {
            testSettings.Filter = "TestCategory!=ExcludeFromAppVeyor";
        }

        foreach (var project in GetFiles("./tests/*Tests/*.csproj"))
        {
            Information($"Running tests in {project}");
            DotNetCoreTest(MakeAbsolute(project).ToString(), testSettings);
        }
    });
    
Task("Pack")
    .Description("Packs the NuGet packages.")
    .IsDependentOn("Build")
    .Does(() =>
    {
        MSBuild("./Buildalyzer.sln", new MSBuildSettings()
            .WithTarget("pack")
            .SetConfiguration(configuration)
            .WithProperty("Version", semVersion)
            .WithProperty("FileVersion", version)
        );
    });

Task("Zip")
    .Description("Zips the build output.")
    .IsDependentOn("Build")
    .Does(() =>
    {  
        CopyFiles(new FilePath[] { "LICENSE", "README.md", "ReleaseNotes.md" }, buildDir);        
        var files = GetFiles(buildDir.Path.FullPath + "/**/*");
        files.Remove(files.Where(x => x.GetExtension() == "nupkg").ToList());
        Zip(buildDir, zipPath, files);
    });

Task("Push")
    .Description("Pushes the packages to the NuGet gallery.")
    .IsDependentOn("Pack")
    .WithCriteria(() => isLocal)
    .Does(() =>
    {
        var nugetKey = EnvironmentVariable("BUILDALYZER_NUGET_KEY");
        if (string.IsNullOrEmpty(nugetKey))
        {
            throw new InvalidOperationException("Could not resolve NuGet API key.");
        }

        foreach (var nupkg in GetFiles(buildDir.Path.FullPath + "/*.nupkg"))
        {
            NuGetPush(nupkg, new NuGetPushSettings 
            {
                ApiKey = nugetKey,
                Source = "https://api.nuget.org/v3/index.json"
            });
        }
    });

Task("Release")
    .Description("Generates a release on GitHub.")
    .IsDependentOn("Zip")
    .WithCriteria(() => isLocal)
    .Does(() =>
    {
        var githubToken = EnvironmentVariable("BUILDALYZER_GITHUB_TOKEN");
        if (string.IsNullOrEmpty(githubToken))
        {
            throw new InvalidOperationException("Could not resolve GitHub token.");
        }
        
        var github = new GitHubClient(new ProductHeaderValue("CakeBuild"))
        {
            Credentials = new Credentials(githubToken)
        };
        var release = github.Repository.Release.Create("daveaglick", "Buildalyzer", new NewRelease("v" + semVersion) 
        {
            Name = semVersion,
            Body = string.Join(Environment.NewLine, releaseNotes.Notes),
            TargetCommitish = "master"
        }).Result;
        
        using (var zipStream = System.IO.File.OpenRead(zipPath.Path.FullPath))
        {
            var releaseAsset = github.Repository.Release.UploadAsset(release, new ReleaseAssetUpload(zipFile, "application/zip", zipStream, null)).Result;
        }
    });

Task("Docs")
    .Description("Generates and previews the docs.")
    .IsDependentOn("Build")
    .Does(() =>
    {
        Wyam(new WyamSettings
        {
            RootPath = docsDir,
            Recipe = "Docs",
            Theme = "Samson",
            Preview = true
        });  
    });

Task("Web")
    .Description("Generates and deploys the docs.")
    .IsDependentOn("Build")
    .Does(() =>
    {
        var netlifyToken = EnvironmentVariable("BUILDALYZER_NETLIFY_TOKEN");
        Wyam(new WyamSettings
        {
            RootPath = docsDir,
            Recipe = "Docs",
            Theme = "Samson",
            UpdatePackages = true
        });  

        Information("Deploying output to Netlify");
        var client = new NetlifyClient(netlifyToken);
        client.UpdateSite("buildalyzer.netlify.com", MakeAbsolute(docsDir).FullPath + "/output").SendAsync().Wait();
    });


//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////
    
Task("Default")
    .IsDependentOn("Test");

Task("Publish")
    .Description("Generates a GitHub release, pushes the NuGet package, and deploys the docs site.")
    .IsDependentOn("Release")
    .IsDependentOn("Push")
    .IsDependentOn("Web");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);