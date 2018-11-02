using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

        private readonly Dictionary<string, string> _globalProperties;
        private readonly Dictionary<string, string> _environmentVariables;

        // Used for cloning
        private IDictionary<string, string> _additionalGlobalProperties;
        private IDictionary<string, string> _additionalEnvironmentVariables;

        /// <summary>
        /// Indicates that a design-time build should be performed.
        /// </summary>
        /// <remarks>
        /// See https://github.com/dotnet/project-system/blob/master/docs/design-time-builds.md
        /// </remarks>
        public bool DesignTime { get; }

        /// <summary>
        /// Runs the restore target prior to any other targets using the MSBuild <code>restore</code> switch.
        /// </summary>
        public bool Restore { get; }

        public string[] TargetsToBuild { get; }

        public string MsBuildExePath { get; }

        public string DotnetExePath { get; }

        public IReadOnlyDictionary<string, string> GlobalProperties => _globalProperties;

        public IReadOnlyDictionary<string, string> EnvironmentVariables => _environmentVariables;

        public BuildEnvironment(
            bool designTime,
            bool restore,
            string[] targetsToBuild,
            string msBuildExePath,
            string dotnetExePath,
            IDictionary<string, string> additionalGlobalProperties = null,
            IDictionary<string, string> additionalEnvironmentVariables = null)
        {
            DesignTime = designTime;
            Restore = restore;
            TargetsToBuild = targetsToBuild ?? throw new ArgumentNullException(nameof(targetsToBuild));

            // Check if we've already specified a path to MSBuild
            string envMsBuildExePath = System.Environment.GetEnvironmentVariable(Environment.EnvironmentVariables.MSBUILD_EXE_PATH);
            MsBuildExePath = !string.IsNullOrEmpty(envMsBuildExePath) && File.Exists(envMsBuildExePath)
                ? envMsBuildExePath : msBuildExePath;
            if(MsBuildExePath == null)
            {
                throw new ArgumentNullException(nameof(msBuildExePath));
            }

            // The dotnet path defaults to "dotnet" - if it's null then the user changed it and we should warn them
            DotnetExePath = dotnetExePath ?? throw new ArgumentNullException(nameof(dotnetExePath));

            // Set global properties
            _globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { MsBuildProperties.ProvideCommandLineArgs, "true" },

                // Workaround for a problem with resource files, see https://github.com/dotnet/sdk/issues/346#issuecomment-257654120
                { MsBuildProperties.GenerateResourceMSBuildArchitecture, "CurrentArchitecture" },

                // MsBuildProperties.SolutionDir will get set by ProjectAnalyzer
            };
            if(DesignTime)
            {
                _globalProperties.Add(MsBuildProperties.DesignTimeBuild, "true");
                _globalProperties.Add(MsBuildProperties.BuildProjectReferences, "false");
                _globalProperties.Add(MsBuildProperties.SkipCompilerExecution, "true");
                _globalProperties.Add(MsBuildProperties.DisableRarCache, "true");
                _globalProperties.Add(MsBuildProperties.AutoGenerateBindingRedirects, "false");
                _globalProperties.Add(MsBuildProperties.CopyBuildOutputToOutputDirectory, "false");
                _globalProperties.Add(MsBuildProperties.CopyOutputSymbolsToOutputDirectory, "false");
                _globalProperties.Add(MsBuildProperties.SkipCopyBuildProduct, "true");
                _globalProperties.Add(MsBuildProperties.AddModules, "false");
                _globalProperties.Add(MsBuildProperties.UseCommonOutputDirectory, "true");  // This is used in a condition to prevent copying in _CopyFilesMarkedCopyLocal
                _globalProperties.Add(MsBuildProperties.GeneratePackageOnBuild, "false");  // Prevent NuGet.Build.Tasks.Pack.targets from running the pack targets (since we didn't build anything)
            }
            _additionalGlobalProperties = CopyItems(_globalProperties, additionalGlobalProperties);

            // Set environment variables 
            _environmentVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _additionalEnvironmentVariables = CopyItems(_environmentVariables, additionalEnvironmentVariables);
        }

        private Dictionary<string, string> CopyItems(Dictionary<string, string> destination, IDictionary<string, string> source)
        {
            if(source != null)
            {
                foreach(KeyValuePair<string, string> item in source)
                {
                    destination[item.Key] = item.Value;
                }

                // Copy to a new dictionary in case the source dictionary is mutated
                return new Dictionary<string, string>(source, StringComparer.OrdinalIgnoreCase);
            }
            return null;
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
                Restore,
                targets,
                MsBuildExePath,
                DotnetExePath,
                _additionalGlobalProperties,
                _additionalEnvironmentVariables);
    }
}