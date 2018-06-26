using System;
using System.Collections.Generic;
using System.IO;

namespace Buildalyzer.Environment
{
    /// <summary>
    /// An immutable representation of a particular build environment (paths, properties, etc).
    /// </summary>
    public sealed class BuildEnvironment
    {
        // https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.runtimeinformation.frameworkdescription
        public static bool IsRunningOnCore =>
            System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
                .Replace(" ", string.Empty).Trim().StartsWith(".NETCore", StringComparison.OrdinalIgnoreCase);

        public string MsBuildExePath { get; }

        public string ExtensionsPath { get; }

        public string SDKsPath { get; }

        public string RoslynTargetsPath { get; }

        public string ToolsPath => Path.GetDirectoryName(MsBuildExePath);
        
        internal IReadOnlyDictionary<string, string> GlobalProperties { get; }

        internal IReadOnlyDictionary<string, string> EnvironmentVariables { get; }
        
        public BuildEnvironment(
            string msBuildExePath,
            string extensionsPath,
            string sdksPath,
            string roslynTargetsPath,
            IDictionary<string, string> additionalGlobalProperties = null,
            IDictionary<string, string> additionalEnvironmentVariables = null)
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
            Dictionary<string, string> globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
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
            if(additionalGlobalProperties != null)
            {
                foreach(var globalProperty in additionalGlobalProperties)
                {
                    globalProperties[globalProperty.Key] = globalProperty.Value;
                }
            }
            GlobalProperties = globalProperties;

            Dictionary<string, string> environmentVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { MsBuildProperties.MSBuildExtensionsPath, ExtensionsPath },
                { MsBuildProperties.MSBuildExtensionsPath32, ExtensionsPath },
                { MsBuildProperties.MSBuildExtensionsPath64, ExtensionsPath },
                { MsBuildProperties.MSBuildSDKsPath, SDKsPath },
                { Environment.EnvironmentVariables.MSBUILD_EXE_PATH, MsBuildExePath }
            };
            if (additionalEnvironmentVariables != null)
            {
                foreach (var environmentVariable in additionalEnvironmentVariables)
                {
                    environmentVariables[environmentVariable.Key] = environmentVariable.Value;
                }
            }
            EnvironmentVariables = environmentVariables;
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