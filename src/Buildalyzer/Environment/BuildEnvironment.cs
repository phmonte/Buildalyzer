using System;
using System.Collections.Generic;
using System.IO;

namespace Buildalyzer.Environment
{
    public sealed class BuildEnvironment
    {
        public string MsBuildExePath { get; set; }

        public string ExtensionsPath { get; set; }

        public string SDKsPath { get; set; }

        public string RoslynTargetsPath { get; set; }

        public string ToolsPath => Path.GetDirectoryName(MsBuildExePath);

        internal Dictionary<string, string> GlobalProperties { get; }

        public BuildEnvironment(string msBuildExePath, string extensionsPath, string sdksPath, string roslynTargetsPath)
        {
            // Check if we've already specified a path to MSBuild
            string envMsBuildExePath = System.Environment.GetEnvironmentVariable(EnvironmentVariables.MSBUILD_EXE_PATH);
            MsBuildExePath = !string.IsNullOrEmpty(envMsBuildExePath) && File.Exists(envMsBuildExePath)
                ? envMsBuildExePath : msBuildExePath;

            ExtensionsPath = extensionsPath;
            SDKsPath = sdksPath;
            RoslynTargetsPath = roslynTargetsPath;

            // Set default global properties
            // MsBuildProperties.SolutionDir will get set by ProjectAnalyzer
            GlobalProperties = new Dictionary<string, string>
            {
                { MsBuildProperties.DesignTimeBuild, "true" },
                { MsBuildProperties.BuildProjectReferences, "false" },
                { MsBuildProperties.SkipCompilerExecution, "true" },
                { MsBuildProperties.ProvideCommandLineArgs, "true" },
                { MsBuildProperties.DisableRarCache, "true" },
                { MsBuildProperties.AutoGenerateBindingRedirects, "false" },
                // Workaround for a problem with resource files, see https://github.com/dotnet/sdk/issues/346#issuecomment-257654120
                { MsBuildProperties.GenerateResourceMSBuildArchitecture, "CurrentArchitecture" },
                { MsBuildProperties.MSBuildExtensionsPath, ExtensionsPath },
                { MsBuildProperties.MSBuildExtensionsPath32, ExtensionsPath },
                { MsBuildProperties.MSBuildExtensionsPath64, ExtensionsPath },
                { MsBuildProperties.MSBuildSDKsPath, SDKsPath },
                { MsBuildProperties.RoslynTargetsPath, RoslynTargetsPath },
            };
        }

        internal IDisposable SetEnvironmentVariables() => new TemporaryEnvironment(
            new Dictionary<string, string>
            {
                { MsBuildProperties.MSBuildExtensionsPath, ExtensionsPath },
                { MsBuildProperties.MSBuildExtensionsPath32, ExtensionsPath },
                { MsBuildProperties.MSBuildExtensionsPath64, ExtensionsPath },
                { MsBuildProperties.MSBuildSDKsPath, SDKsPath },
                { EnvironmentVariables.MSBUILD_EXE_PATH, MsBuildExePath }
            });

        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(MsBuildExePath))
            {
                throw new ArgumentException($"The value for {nameof(BuildEnvironment)}.{nameof(MsBuildExePath)} must be provided.");
            }
            if (string.IsNullOrWhiteSpace(ExtensionsPath))
            {
                throw new ArgumentException($"The value for {nameof(BuildEnvironment)}.{nameof(ExtensionsPath)} must be provided.");
            }
            if (string.IsNullOrWhiteSpace(SDKsPath))
            {
                throw new ArgumentException($"The value for {nameof(BuildEnvironment)}.{nameof(SDKsPath)} must be provided.");
            }
            if (string.IsNullOrWhiteSpace(RoslynTargetsPath))
            {
                throw new ArgumentException($"The value for {nameof(BuildEnvironment)}.{nameof(RoslynTargetsPath)} must be provided.");
            }
        }
    }
}