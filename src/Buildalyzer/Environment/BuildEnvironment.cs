using System;
using System.Collections.Generic;
using System.IO;

namespace Buildalyzer.Environment
{
    public sealed class BuildEnvironment
    {
        public string ToolsPath { get; set; }

        public string ExtensionsPath { get; set; }

        public string SDKsPath { get; set; }

        public string RoslynTargetsPath { get; set; }

        internal Dictionary<string, string> GlobalProperties { get; }

        public BuildEnvironment(string toolsPath, string extensionsPath, string sdksPath, string roslynTargetsPath)
        {
            ToolsPath = toolsPath;
            ExtensionsPath = extensionsPath;
            SDKsPath = sdksPath;
            RoslynTargetsPath = roslynTargetsPath;

            // Set default global properties
            GlobalProperties = new Dictionary<string, string>
            {
                { MsBuildProperties.DesignTimeBuild, "true" },
                { MsBuildProperties.BuildProjectReferences, "false" },
                { MsBuildProperties.SkipCompilerExecution, "true" },
                { MsBuildProperties.ProvideCommandLineArgs, "true" },
                // Workaround for a problem with resource files, see https://github.com/dotnet/sdk/issues/346#issuecomment-257654120
                { MsBuildProperties.GenerateResourceMSBuildArchitecture, "CurrentArchitecture" },
                { MsBuildProperties.MSBuildExtensionsPath, ExtensionsPath },
                { MsBuildProperties.MSBuildExtensionsPath32, ExtensionsPath },
                { MsBuildProperties.MSBuildExtensionsPath64, ExtensionsPath },
                { MsBuildProperties.MSBuildSDKsPath, SDKsPath },
                { MsBuildProperties.RoslynTargetsPath, RoslynTargetsPath },
            };
        }

        internal IDisposable SetEnvironmentVariables()
        {
            Dictionary<string, string> newVariables = new Dictionary<string, string>
            {
                { MsBuildProperties.MSBuildExtensionsPath, ExtensionsPath },
                { MsBuildProperties.MSBuildExtensionsPath32, ExtensionsPath },
                { MsBuildProperties.MSBuildExtensionsPath64, ExtensionsPath },
                { MsBuildProperties.MSBuildSDKsPath, SDKsPath }
            };

            // Special case for MSBUILD_EXE_PATH - only set a new value if one isn't already set
            string msbuildExePath = System.Environment.GetEnvironmentVariable("MSBUILD_EXE_PATH");
            if (string.IsNullOrEmpty(msbuildExePath) || !File.Exists(msbuildExePath))
            {
                newVariables.Add("MSBUILD_EXE_PATH", ToolsPath);
            }

            return new EnvironmentVariableSetter(newVariables);
        }
    }
}