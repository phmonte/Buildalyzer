namespace Buildalyzer.Environment
{
    public class EnvironmentOptions
    {
        /// <summary>
        /// Indicates that the <c>Restore</c> target should be used during build.
        /// </summary>
        public bool RestoreTarget { get; set; } = true;

        /// <summary>
        /// Indicates that the <c>Clean</c> target should be used during build.
        /// This also does some other tricks to ensure that even if the <c>Clean</c>
        /// target doesn't delete all the artifacts, the <c>Compile</c> target is still
        /// evaluated if it's specified.
        /// </summary>
        public bool CleanTarget { get; set; } = true;

        /// <summary>
        /// Indicates that the <c>Compile</c> target should be used during build.
        /// </summary>
        public bool CompileTarget { get; set; } = true;

        /// <summary>
        /// Indicates that the <c>Build</c> target should be used during build.
        /// </summary>
        public bool BuildTarget { get; set; } = false;

        /// <summary>
        /// Indicates that a design-time build should be performed.
        /// </summary>
        /// <remarks>
        /// See https://github.com/dotnet/project-system/blob/master/docs/design-time-builds.md
        /// </remarks>
        public bool DesignTime { get; set; } = true;
    }
}