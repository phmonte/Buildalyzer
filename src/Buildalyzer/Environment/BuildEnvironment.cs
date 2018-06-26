using System;
using System.Collections.Generic;
using System.IO;

namespace Buildalyzer.Environment
{
    public sealed class BuildEnvironment
    {
        public static bool IsRunningOnCoreClr =>
            Type.GetType("System.Runtime.Loader.AssemblyLoadContext, System.Runtime.Loader, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", false) != null;

        public string MsBuildExePath { get; }

        public string ExtensionsPath { get; }

        public string SDKsPath { get; }

        public string RoslynTargetsPath { get; }

        public string ToolsPath => Path.GetDirectoryName(MsBuildExePath);
        
        internal Dictionary<string, string> GlobalProperties { get; }

        internal Dictionary<string, string> EnvironmentVariables { get; }

        public BuildEnvironment(string msBuildExePath, string extensionsPath, string sdksPath, string roslynTargetsPath)
        {
            // Check if we've already specified a path to MSBuild
            string envMsBuildExePath = System.Environment.GetEnvironmentVariable(Environment.EnvironmentVariables.MSBUILD_EXE_PATH);
            MsBuildExePath = !string.IsNullOrEmpty(envMsBuildExePath) && File.Exists(envMsBuildExePath)
                ? envMsBuildExePath : msBuildExePath;

            ExtensionsPath = extensionsPath;
            SDKsPath = sdksPath;
            RoslynTargetsPath = roslynTargetsPath;

            // Set default global properties
            // MsBuildProperties.SolutionDir will get set by ProjectAnalyzer
            GlobalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
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

            EnvironmentVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { MsBuildProperties.MSBuildExtensionsPath, ExtensionsPath },
                { MsBuildProperties.MSBuildExtensionsPath32, ExtensionsPath },
                { MsBuildProperties.MSBuildExtensionsPath64, ExtensionsPath },
                { MsBuildProperties.MSBuildSDKsPath, SDKsPath },
                { Environment.EnvironmentVariables.MSBUILD_EXE_PATH, MsBuildExePath }
            };
        }

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