using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Buildalyzer.Environment
{
    /// <summary>
    /// An immutable representation of a particular build environment (paths, properties, etc).
    /// </summary>
    public sealed class BuildEnvironment
    {
        // https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.runtimeinformation.frameworkdescription
        // .NET "Core" will return ".NET Core" up to 3.x and ".NET" for > 5
        public static bool IsRunningOnCore =>
            !System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
                .Replace(" ", string.Empty)
                .Trim()
                .StartsWith(".NETFramework", StringComparison.OrdinalIgnoreCase);

        private readonly Dictionary<string, string> _globalProperties;
        private readonly Dictionary<string, string> _environmentVariables;

        // Used for cloning
        private readonly IDictionary<string, string> _additionalGlobalProperties;
        private readonly IDictionary<string, string> _additionalEnvironmentVariables;

        /// <summary>
        /// Indicates that a design-time build should be performed.
        /// </summary>
        /// <remarks>
        /// See https://github.com/dotnet/project-system/blob/master/docs/design-time-builds.md.
        /// </remarks>
        public bool DesignTime { get; }

        /// <summary>
        /// Indicates that the app is self-contained.
        /// </summary>
        /// <remarks>
        /// See https://docs.microsoft.com/en-us/dotnet/core/deploying/.
        /// </remarks>
        public bool SelfContained { get; }

        /// <summary>
        /// Runs the restore target prior to any other targets using the MSBuild <c>restore</c> switch.
        /// </summary>
        public bool Restore { get; }

        public string[] TargetsToBuild { get; }

        public string MsBuildExePath { get; }

        public string DotnetExePath { get; }

        public IEnumerable<string> Arguments { get; }

        public IReadOnlyDictionary<string, string> GlobalProperties => _globalProperties;

        public IReadOnlyDictionary<string, string> EnvironmentVariables => _environmentVariables;

        public BuildEnvironment(
            bool designTime,
            bool restore,
            bool isSelfContained,
            string[] targetsToBuild,
            string msBuildExePath,
            string dotnetExePath,
            IEnumerable<string> arguments,
            IDictionary<string, string> additionalGlobalProperties = null,
            IDictionary<string, string> additionalEnvironmentVariables = null)
        {
            DesignTime = designTime;
            Restore = restore;
            TargetsToBuild = targetsToBuild ?? throw new ArgumentNullException(nameof(targetsToBuild));
            Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));

            // Check if we've already specified a path to MSBuild
            string envMsBuildExePath = System.Environment.GetEnvironmentVariable(Environment.EnvironmentVariables.MSBUILD_EXE_PATH);
            MsBuildExePath = !string.IsNullOrEmpty(envMsBuildExePath) && File.Exists(envMsBuildExePath)
                ? envMsBuildExePath : msBuildExePath;
            if (MsBuildExePath == null)
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
            if (DesignTime)
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

                if (!isSelfContained)
                {
                    _globalProperties.Add(MsBuildProperties.UseAppHost, "false"); // Prevent creation of native host executable https://docs.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props#useapphost
                }
            }
            _additionalGlobalProperties = CopyItems(_globalProperties, additionalGlobalProperties);

            // Set environment variables
            _environmentVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _additionalEnvironmentVariables = CopyItems(_environmentVariables, additionalEnvironmentVariables);
        }

        private Dictionary<string, string> CopyItems(Dictionary<string, string> destination, IDictionary<string, string> source)
        {
            if (source != null)
            {
                foreach (KeyValuePair<string, string> item in source)
                {
                    destination[item.Key] = item.Value;
                }

                // Copy to a new dictionary in case the source dictionary is mutated
                return new Dictionary<string, string>(source, StringComparer.OrdinalIgnoreCase);
            }
            return null;
        }
    }
}