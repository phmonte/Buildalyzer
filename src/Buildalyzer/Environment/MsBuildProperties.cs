namespace Buildalyzer.Environment
{
    public static class MsBuildProperties
    {
        // MSBuild Project Loading
        public const string MSBuildExtensionsPath = nameof(MSBuildExtensionsPath);
        public const string MSBuildExtensionsPath32 = nameof(MSBuildExtensionsPath32);
        public const string MSBuildExtensionsPath64 = nameof(MSBuildExtensionsPath64);
        public const string MSBuildSDKsPath = nameof(MSBuildSDKsPath);
        public const string RoslynTargetsPath = nameof(RoslynTargetsPath);
        public const string SolutionDir = nameof(SolutionDir);

        // Design-time Build
        public const string DesignTimeBuild = nameof(DesignTimeBuild);
        public const string BuildProjectReferences = nameof(BuildProjectReferences);
        public const string SkipCompilerExecution = nameof(SkipCompilerExecution);
        public const string ProvideCommandLineArgs = nameof(ProvideCommandLineArgs);
        public const string DisableRarCache = nameof(DisableRarCache);
        public const string AutoGenerateBindingRedirects = nameof(AutoGenerateBindingRedirects);

        // Others
        public const string GenerateResourceMSBuildArchitecture = nameof(GenerateResourceMSBuildArchitecture);
    }
}