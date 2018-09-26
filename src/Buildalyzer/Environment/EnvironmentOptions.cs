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
        /// The default targets to build. The eventual build environment may remove one or more of these
        /// targets depending on project file format and build tools support.
        /// </summary>
        public List<string> TargetsToBuild { get; } = new List<string> { "Restore", "Clean", "Build" };

        /// <summary>
        /// Indicates that a design-time build should be performed.
        /// </summary>
        /// <remarks>
        /// See https://github.com/dotnet/project-system/blob/master/docs/design-time-builds.md
        /// </remarks>
        public bool DesignTime { get; set; } = true;

        public IDictionary<string, string> GlobalProperties { get; } = new Dictionary<string, string>();

        public IDictionary<string, string> EnvironmentVariables { get; } = new Dictionary<string, string>();
    }
}