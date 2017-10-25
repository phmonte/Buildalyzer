using System;
using System.Collections.Generic;

namespace Buildalyzer.Environment
{
    internal abstract class BuildEnvironment
    {
        private string _oldMsBuildExtensionsPath = null;
        private string _oldMsBuildSdksPath = null;

        public abstract string GetToolsPath();

        public virtual Dictionary<string, string> GetGlobalProperties(string solutionDir) =>
            new Dictionary<string, string>
            {
                { MsBuildProperties.SolutionDir, solutionDir },
                { MsBuildProperties.DesignTimeBuild, "true" },
                { MsBuildProperties.BuildProjectReferences, "false" },
                { MsBuildProperties.SkipCompilerExecution, "true" },
                { MsBuildProperties.ProvideCommandLineArgs, "true" },
                // Workaround for a problem with resource files, see https://github.com/dotnet/sdk/issues/346#issuecomment-257654120
                { MsBuildProperties.GenerateResourceMSBuildArchitecture, "CurrentArchitecture" }
            };

        public virtual void SetEnvironmentVars(IReadOnlyDictionary<string, string> globalProperties)
        {
            if (globalProperties.TryGetValue(MsBuildProperties.MSBuildExtensionsPath, out var msBuildExtensionsPath))
            {
                _oldMsBuildExtensionsPath = System.Environment.GetEnvironmentVariable(MsBuildProperties.MSBuildExtensionsPath);
                System.Environment.SetEnvironmentVariable(MsBuildProperties.MSBuildExtensionsPath, msBuildExtensionsPath);
            }
            if (globalProperties.TryGetValue(MsBuildProperties.MSBuildSDKsPath, out var msBuildSDKsPath))
            {
                _oldMsBuildSdksPath = System.Environment.GetEnvironmentVariable(MsBuildProperties.MSBuildSDKsPath);
                System.Environment.SetEnvironmentVariable(MsBuildProperties.MSBuildSDKsPath, msBuildSDKsPath);
            }
        }

        public virtual void UnsetEnvironmentVars()
        {
            if (_oldMsBuildExtensionsPath != null)
            {
                System.Environment.SetEnvironmentVariable(MsBuildProperties.MSBuildExtensionsPath, _oldMsBuildExtensionsPath);
            }
            if (_oldMsBuildSdksPath != null)
            {
                System.Environment.SetEnvironmentVariable(MsBuildProperties.MSBuildSDKsPath, _oldMsBuildSdksPath);
            }
        }
    }
}