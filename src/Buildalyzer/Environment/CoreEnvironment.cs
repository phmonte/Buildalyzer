using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;

namespace Buildalyzer.Environment
{

    // Based on code from OmniSharp
    // https://github.com/OmniSharp/omnisharp-roslyn/blob/78ccc8b4376c73da282a600ac6fb10fce8620b52/src/OmniSharp.Abstractions/Services/DotNetCliService.cs
    internal class CoreEnvironment : BuildEnvironment
    {
        public string ToolsPath { get; }
        public string ExtensionsPath { get; }
        public string SDKsPath { get; }
        public string RoslynTargetsPath { get; }

        public CoreEnvironment(string projectPath)
        {
            string dotnetPath = DotnetPathResolver.ResolvePath(projectPath);
            ToolsPath = dotnetPath;
            ExtensionsPath = dotnetPath;
            SDKsPath = Path.Combine(dotnetPath, "Sdks");
            RoslynTargetsPath = Path.Combine(dotnetPath, "Roslyn");
        }

        public override string GetToolsPath() => ToolsPath;

        public override Dictionary<string, string> GetGlobalProperties(string solutionDir)
        {
            Dictionary<string, string> globalProperties = base.GetGlobalProperties(solutionDir);
            globalProperties.Add(MsBuildProperties.MSBuildExtensionsPath, ExtensionsPath);
            globalProperties.Add(MsBuildProperties.MSBuildSDKsPath, SDKsPath);
            globalProperties.Add(MsBuildProperties.RoslynTargetsPath, RoslynTargetsPath);
            return globalProperties;
        }   
    }
}