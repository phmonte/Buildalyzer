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

        // Used for cloning
        private IDictionary<string, string> _additionalGlobalProperties;
        private IDictionary<string, string> _additionalEnvironmentVariables;

        public bool DesignTime { get; }

        public string[] TargetsToBuild { get; }

        public string MsBuildExePath { get; }

        public string ExtensionsPath { get; }

        public string SDKsPath { get; }

        public string RoslynTargetsPath { get; }

        public string ToolsPath => Path.GetDirectoryName(MsBuildExePath);
        
        public IReadOnlyDictionary<string, string> GlobalProperties { get; }

        public IReadOnlyDictionary<string, string> EnvironmentVariables { get; }

        public BuildEnvironment(
            bool designTime,
            string[] targetsToBuild,
            string msBuildExePath,
            string extensionsPath,
            string sdksPath,
            string roslynTargetsPath,
            IDictionary<string, string> additionalGlobalProperties = null,
            IDictionary<string, string> additionalEnvironmentVariables = null)
        {
            DesignTime = designTime;
            TargetsToBuild = targetsToBuild ?? throw new ArgumentNullException(nameof(targetsToBuild));

            // Check if we've already specified a path to MSBuild
            string envMsBuildExePath = System.Environment.GetEnvironmentVariable(Environment.EnvironmentVariables.MSBUILD_EXE_PATH);
            MsBuildExePath = !string.IsNullOrEmpty(envMsBuildExePath) && File.Exists(envMsBuildExePath)
                ? envMsBuildExePath : msBuildExePath;
            if(MsBuildExePath == null)
            {
                throw new ArgumentNullException(nameof(msBuildExePath));
            }

            ExtensionsPath = extensionsPath ?? throw new ArgumentNullException(nameof(extensionsPath));
            SDKsPath = sdksPath ?? throw new ArgumentNullException(nameof(sdksPath));
            RoslynTargetsPath = roslynTargetsPath ?? throw new ArgumentNullException(nameof(roslynTargetsPath));

            // Set default global properties
            Dictionary<string, string> globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { MsBuildProperties.ProvideCommandLineArgs, "true" },
                { MsBuildProperties.MSBuildExtensionsPath, ExtensionsPath },
                { MsBuildProperties.MSBuildExtensionsPath32, ExtensionsPath },
                { MsBuildProperties.MSBuildExtensionsPath64, ExtensionsPath },
                { MsBuildProperties.MSBuildSDKsPath, SDKsPath },
                { MsBuildProperties.RoslynTargetsPath, RoslynTargetsPath },

                // Workaround for a problem with resource files, see https://github.com/dotnet/sdk/issues/346#issuecomment-257654120
                { MsBuildProperties.GenerateResourceMSBuildArchitecture, "CurrentArchitecture" },

                // MsBuildProperties.SolutionDir will get set by ProjectAnalyzer
            };
            if(DesignTime)
            {
                globalProperties.Add(MsBuildProperties.DesignTimeBuild, "true");
                globalProperties.Add(MsBuildProperties.BuildProjectReferences, "false");
                globalProperties.Add(MsBuildProperties.SkipCompilerExecution, "true");
                globalProperties.Add(MsBuildProperties.DisableRarCache, "true");
                globalProperties.Add(MsBuildProperties.AutoGenerateBindingRedirects, "false");
                globalProperties.Add(MsBuildProperties.CopyBuildOutputToOutputDirectory, "false");
                globalProperties.Add(MsBuildProperties.CopyOutputSymbolsToOutputDirectory, "false");
                globalProperties.Add(MsBuildProperties.SkipCopyBuildProduct, "true");
                globalProperties.Add(MsBuildProperties.AddModules, "false");
                globalProperties.Add(MsBuildProperties.UseCommonOutputDirectory, "true");  // This is used in a condition to prevent copying in _CopyFilesMarkedCopyLocal
            }
            if(additionalGlobalProperties != null)
            {
                foreach(var globalProperty in additionalGlobalProperties)
                {
                    globalProperties[globalProperty.Key] = globalProperty.Value;
                }

                // Copy to a new dictionary in case the source dictionary is mutated
                _additionalGlobalProperties = new Dictionary<string, string>(additionalGlobalProperties);
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

                // Copy to a new dictionary in case the source dictionary is mutated
                _additionalEnvironmentVariables = new Dictionary<string, string>(additionalEnvironmentVariables);
            }
            EnvironmentVariables = environmentVariables;
        }

        /// <summary>
        /// Clones the build environment with a different set of build targets.
        /// </summary>
        /// <param name="targets">
        /// The targets that should be used to build the project.
        /// Specifying an empty array indicates that the <see cref="ProjectAnalyzer"/> should
        /// return a <see cref="Microsoft.Build.Execution.ProjectInstance"/> without building the project.
        /// </param>
        /// <returns>A new build environment with the specified targets.</returns>
        public BuildEnvironment WithTargetsToBuild(params string[] targets) =>
            new BuildEnvironment(
                DesignTime,
                targets,
                MsBuildExePath,
                ExtensionsPath,
                SDKsPath,
                RoslynTargetsPath,
                _additionalGlobalProperties,
                _additionalEnvironmentVariables);
    }
}