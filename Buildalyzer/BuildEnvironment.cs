using System;
using System.Collections.Generic;

namespace Buildalyzer
{
    internal class BuildEnvironment : IDisposable
    {
        private readonly string _oldMsBuildExtensionsPath = null;
        private readonly string _oldMsBuildSdksPath = null;

        public BuildEnvironment(IReadOnlyDictionary<string, string> globalProperties)
        {
            if (globalProperties.TryGetValue(MsBuildProperties.MSBuildExtensionsPath, out var msBuildExtensionsPath))
            {
                _oldMsBuildExtensionsPath = Environment.GetEnvironmentVariable(MsBuildProperties.MSBuildExtensionsPath);
                Environment.SetEnvironmentVariable(MsBuildProperties.MSBuildExtensionsPath, msBuildExtensionsPath);
            }
            if (globalProperties.TryGetValue(MsBuildProperties.MSBuildSDKsPath, out var msBuildSDKsPath))
            {
                _oldMsBuildSdksPath = Environment.GetEnvironmentVariable(MsBuildProperties.MSBuildSDKsPath);
                Environment.SetEnvironmentVariable(MsBuildProperties.MSBuildSDKsPath, msBuildSDKsPath);
            }
        }

        public void Dispose()
        {
            if (_oldMsBuildExtensionsPath != null)
            {
                Environment.SetEnvironmentVariable(MsBuildProperties.MSBuildExtensionsPath, _oldMsBuildExtensionsPath);
            }
            if (_oldMsBuildSdksPath != null)
            {
                Environment.SetEnvironmentVariable(MsBuildProperties.MSBuildSDKsPath, _oldMsBuildSdksPath);
            }
        }
    }
}