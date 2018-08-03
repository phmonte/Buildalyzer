// The following environment variables need to be set for Publish target:
// BUILDALYZER_NUGET_KEY
// BUILDALYZER_MYGET_KEY
// BUILDALYZER_GITHUB_TOKEN
// BUILDALYZER_NETLIFY_TOKEN

#tool nuget:?package=Wyam&version=1.4.1
#addin nuget:?package=Cake.Wyam&version=1.4.1
#addin "Octokit"
#addin "NetlifySharp"
#addin "Newtonsoft.Json"
            
// The built-in AppVeyor logger doesn't work yet,
// but when it does we can remove the tool directive and TestAdapterPath property
// https://github.com/appveyor/ci/issues/1601
#tool "Appveyor.TestLogger&version=2.0.0"

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
var isPullRequest = AppVeyor.Environment.PullRequest.IsPullRequest;
var buildNumber = AppVeyor.Environment.Build.Number;

var releaseNotes = ParseReleaseNotes("./ReleaseNotes.md");

var version = releaseNotes.Version.ToString();
var semVersion = version + (isLocal ? string.Empty : string.Concat("-build-", buildNumber));
var msBuildSettings = new DotNetCoreMSBuildSettings()
    .WithProperty("Version", semVersion)
    .WithProperty("AssemblyVersion", version)
    .WithProperty("FileVersion", version);

var buildDir = Directory("./build");
var docsDir = Directory("./docs");

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
    .Description("Cleans the build directories.")
    .Does(() =>
    {
        CleanDirectories(GetDirectories($"./src/*/bin/{ configuration }"));
        CleanDirectories(GetDirectories($"./tests/*Tests/*/bin/{ configuration }"));
        CleanDirectories(buildDir);
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
        
        // Run NuGet CLI restore to handle the Framework test projects       
        NuGetRestore("./Buildalyzer.sln"); 
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
        if (AppVeyor.IsRunningOnAppVeyor)
        {
            testSettings.Filter = "TestCategory!=ExcludeFromBuildServer";
            testSettings.Logger = "Appveyor";

            // Remove this when no longer using the tool (see above)
            testSettings.TestAdapterPath = GetDirectories($"./tools/Appveyor.TestLogger.*/build/_common").First();
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
        DotNetCorePackSettings packSettings = new DotNetCorePackSettings
        {
            Configuration = configuration,
            OutputDirectory = buildDir,            
            MSBuildSettings = msBuildSettings
        };
        
        foreach (var project in GetFiles("./src/*/*.csproj"))
        {
            DotNetCorePack(MakeAbsolute(project).ToString(), packSettings);
        }
    });

Task("Zip")
    .Description("Zips the build output.")
    .IsDependentOn("Build")
    .Does(() =>
    {  
        foreach(var projectDir in GetDirectories("./src/*"))
        {
            CopyFiles(new FilePath[] { "LICENSE", "README.md", "ReleaseNotes.md" }, $"{ projectDir.FullPath }/bin/{ configuration }");  
            var files = GetFiles($"{ projectDir.FullPath }/bin/{ configuration }/**/*");
            files.Remove(files.Where(x => x.GetExtension() == "nupkg").ToList());
            var zipFile = File($"{ projectDir.GetDirectoryName() }-v{ semVersion }.zip");
            Zip(
                $"{ projectDir.FullPath }/bin/{ configuration }",
                $"{ buildDir }/{ zipFile }",
                files);
        }   
    });

Task("MyGet")
    .Description("Pushes the packages to the MyGet feed.")
    .IsDependentOn("Pack")
    .WithCriteria(() => !isPullRequest)
    .Does(() =>
    {
        // Resolve the API key.
        var mygetKey = EnvironmentVariable("BUILDALYZER_MYGET_KEY");
        if (string.IsNullOrEmpty(mygetKey))
        {
            throw new InvalidOperationException("Could not resolve MyGet API key.");
        }

        foreach (var nupkg in GetFiles($"{ buildDir }/*.nupkg"))
        {
            NuGetPush(nupkg, new NuGetPushSettings 
            {
                ApiKey = mygetKey,
                Source = "https://www.myget.org/F/buildalyzer/api/v2/package"
            });
        }
    });

Task("NuGet")
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

        foreach (var nupkg in GetFiles($"{ buildDir }/*.nupkg"))
        {
            NuGetPush(nupkg, new NuGetPushSettings 
            {
                ApiKey = nugetKey,
                Source = "https://api.nuget.org/v3/index.json"
            });
        }
    });

Task("GitHub")
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
        
        foreach(var zipFile in GetFiles($"{ buildDir }/*.zip"))
        {
            using (var zipStream = System.IO.File.OpenRead(zipFile.FullPath))
            {
                var releaseAsset = github.Repository.Release.UploadAsset(
                    release,
                    new ReleaseAssetUpload(zipFile.GetFilename().FullPath, "application/zip", zipStream, null)).Result;
            }
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

Task("Netlify")
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

Task("AppVeyor")
    .Description("Runs a build from the build server and updates build server data.")
    .IsDependentOn("Test")
    .IsDependentOn("Pack")
    .IsDependentOn("Zip")
    .IsDependentOn("MyGet")
    .WithCriteria(() => AppVeyor.IsRunningOnAppVeyor)
    .Does(() =>
    {
        AppVeyor.UpdateBuildVersion(semVersion);
        
        foreach(var file in GetFiles($"{ buildDir }/**/*"))
        {
            Information(file.FullPath);
            AppVeyor.UploadArtifact(file);
        }
    });


//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////
    
Task("Default")
    .IsDependentOn("Test");
    
Task("Release")
    .Description("Generates a GitHub release, pushes the NuGet package, and deploys the docs site.")
    .IsDependentOn("GitHub")
    .IsDependentOn("NuGet")
    .IsDependentOn("Netlify");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);