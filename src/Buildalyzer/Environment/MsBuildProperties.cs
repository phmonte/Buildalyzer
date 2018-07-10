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
        public const string TargetFramework = nameof(TargetFramework);

        // Design-time Build
        public const string DesignTimeBuild = nameof(DesignTimeBuild);
        public const string BuildProjectReferences = nameof(BuildProjectReferences);
        public const string SkipCompilerExecution = nameof(SkipCompilerExecution);
        public const string ProvideCommandLineArgs = nameof(ProvideCommandLineArgs);
        public const string DisableRarCache = nameof(DisableRarCache);
        public const string AutoGenerateBindingRedirects = nameof(AutoGenerateBindingRedirects);
        public const string CopyBuildOutputToOutputDirectory = nameof(CopyBuildOutputToOutputDirectory);
        public const string CopyOutputSymbolsToOutputDirectory = nameof(CopyOutputSymbolsToOutputDirectory);
        public const string SkipCopyBuildProduct = nameof(SkipCopyBuildProduct);
        public const string AddModules = nameof(AddModules);
        public const string UseCommonOutputDirectory = nameof(UseCommonOutputDirectory);
        public const string GeneratePackageOnBuild = nameof(GeneratePackageOnBuild);

        // .NET Framework code analysis rulesets
        public const string CodeAnalysisRuleDirectories = nameof(CodeAnalysisRuleDirectories);
        public const string CodeAnalysisRuleSetDirectories = nameof(CodeAnalysisRuleSetDirectories);

        // NuGet
        public const string ResolveNuGetPackages = nameof(ResolveNuGetPackages);
        public const string NuGetRestoreTargets = nameof(NuGetRestoreTargets);

        // Others
        public const string GenerateResourceMSBuildArchitecture = nameof(GenerateResourceMSBuildArchitecture);
        public const string NonExistentFile = nameof(NonExistentFile);
    }
}