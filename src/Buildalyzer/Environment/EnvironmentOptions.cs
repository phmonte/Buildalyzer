using System.Collections.Generic;

namespace Buildalyzer.Environment
{
    public class EnvironmentOptions
    {
        /// <summary>
        /// Indicates a preferences towards the build environment to use.
        /// The default is a preference for the .NET Core SDK.
        /// </summary>
        public EnvironmentPreference Preference { get; set; } = EnvironmentPreference.Core;

        /// <summary>
        /// The default targets to build.
        /// </summary>
        public List<string> TargetsToBuild { get; } = new List<string> { "Clean", "Build" };

        /// <summary>
        /// Indicates that a design-time build should be performed.
        /// The default value is <code>true</code>.
        /// </summary>
        /// <remarks>
        /// See https://github.com/dotnet/project-system/blob/master/docs/design-time-builds.md
        /// </remarks>
        public bool DesignTime { get; set; } = true;

        /// <summary>
        /// Runs the restore target prior to any other targets using the MSBuild <code>restore</code> switch.
        /// </summary>
        /// <remarks>
        /// See https://github.com/Microsoft/msbuild/pull/2414
        /// </remarks>
        public bool Restore { get; set; } = true;

        public IDictionary<string, string> GlobalProperties { get; } = new Dictionary<string, string>();

        public IDictionary<string, string> EnvironmentVariables { get; } = new Dictionary<string, string>();
        public string DotNetExePath { get; set; } = "dotnet";
    }
}