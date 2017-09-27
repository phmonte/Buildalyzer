namespace Buildalyzer
{
    public static class MsBuildProperties
    {
        // MSBuild Project Loading
        public const string MSBuildExtensionsPath = nameof(MSBuildExtensionsPath);
        public const string MSBuildSDKsPath = nameof(MSBuildSDKsPath);
        public const string RoslynTargetsPath = nameof(RoslynTargetsPath);
        public const string SolutionDir = nameof(SolutionDir);

        // Design-time Build
        public const string DesignTimeBuild = nameof(DesignTimeBuild);
        public const string BuildProjectReferences = nameof(BuildProjectReferences);
        public const string SkipCompilerExecution = nameof(SkipCompilerExecution);
        public const string ProvideCommandLineArgs = nameof(ProvideCommandLineArgs);
    }
}