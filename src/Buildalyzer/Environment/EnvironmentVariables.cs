namespace Buildalyzer.Environment
{
    public static class EnvironmentVariables
    {
#pragma warning disable SA1310 // Field names should not contain underscore
        public const string DOTNET_CLI_UI_LANGUAGE = nameof(DOTNET_CLI_UI_LANGUAGE);
        public const string MSBUILD_EXE_PATH = nameof(MSBUILD_EXE_PATH);
        public const string COREHOST_TRACE = nameof(COREHOST_TRACE);
        public const string DOTNET_HOST_PATH = nameof(DOTNET_HOST_PATH);
#pragma warning restore SA1310 // Field names should not contain underscore
        public const string MSBUILDDISABLENODEREUSE = nameof(MSBUILDDISABLENODEREUSE);
        public const string MSBuildExtensionsPath = nameof(MSBuildExtensionsPath);
        public const string MSBuildSDKsPath = nameof(MSBuildSDKsPath);
    }
}