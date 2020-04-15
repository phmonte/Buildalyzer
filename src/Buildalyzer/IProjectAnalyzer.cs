using System;
using System.Collections.Generic;
using Buildalyzer.Construction;
using Buildalyzer.Environment;
using Microsoft.Build.Construction;
using Microsoft.Build.Logging;
using Microsoft.Extensions.Logging;

namespace Buildalyzer
{
    public interface IProjectAnalyzer
    {
        IEnumerable<Microsoft.Build.Framework.ILogger> BuildLoggers { get; }
        EnvironmentFactory EnvironmentFactory { get; }
        IReadOnlyDictionary<string, string> EnvironmentVariables { get; }
        IReadOnlyDictionary<string, string> GlobalProperties { get; }
        bool IgnoreFaultyImports { get; set; }
        ILogger<ProjectAnalyzer> Logger { get; set; }
        AnalyzerManager Manager { get; }
        IProjectFile ProjectFile { get; }
        Guid ProjectGuid { get; }
        ProjectInSolution ProjectInSolution { get; }
        string SolutionDirectory { get; }

        void AddBinaryLogger(string binaryLogFilePath = null, BinaryLogger.ProjectImportsCollectionMode collectProjectImports = BinaryLogger.ProjectImportsCollectionMode.Embed);
        void AddBuildLogger(Microsoft.Build.Framework.ILogger logger);
        IAnalyzerResults Build();
        IAnalyzerResults Build(BuildEnvironment buildEnvironment);
        IAnalyzerResults Build(EnvironmentOptions environmentOptions);
        IAnalyzerResults Build(string targetFramework);
        IAnalyzerResults Build(string targetFramework, BuildEnvironment buildEnvironment);
        IAnalyzerResults Build(string targetFramework, EnvironmentOptions environmentOptions);
        IAnalyzerResults Build(string[] targetFrameworks);
        IAnalyzerResults Build(string[] targetFrameworks, BuildEnvironment buildEnvironment);
        IAnalyzerResults Build(string[] targetFrameworks, EnvironmentOptions environmentOptions);
        void RemoveBuildLogger(Microsoft.Build.Framework.ILogger logger);
        void RemoveGlobalProperty(string key);
        void SetEnvironmentVariable(string key, string value);
        void SetGlobalProperty(string key, string value);
    }
}