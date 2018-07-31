using Buildalyzer.Construction;
using Buildalyzer.Environment;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Buildalyzer
{
    public class AnalyzerResult
    {

        internal AnalyzerResult(
            ProjectAnalyzer analyzer,
            Project project,
            ProjectInstance projectInstance,
            BuildResult buildResult,
            BuildEnvironment buildEnvironment)
        {
            Analyzer = analyzer;
            Project = project;
            ProjectInstance = projectInstance;
            BuildResult = buildResult;
            BuildEnvironment = buildEnvironment;
        }

        public ProjectAnalyzer Analyzer { get; }

        public Project Project { get; }

        public ProjectInstance ProjectInstance { get; }

        public BuildResult BuildResult { get; }

        public BuildEnvironment BuildEnvironment { get; }

        public bool OverallSuccess => BuildResult.OverallResult == BuildResultCode.Success;
        
        public string TargetFramework =>
            ProjectFile.GetTargetFrameworks(
                null,  // Don't want all target frameworks since the result is just for one
                new[] { ProjectInstance?.GetProperty(ProjectFileNames.TargetFramework)?.EvaluatedValue },
                new[] { (ProjectInstance?.GetProperty(ProjectFileNames.TargetFrameworkIdentifier)?.EvaluatedValue, ProjectInstance?.GetProperty(ProjectFileNames.TargetFrameworkVersion)?.EvaluatedValue) })
            .FirstOrDefault();

        public IReadOnlyList<string> GetSourceFiles() =>
            ProjectInstance?.Items
                .Where(x => x.ItemType == "CscCommandLineArgs"
                    && !x.EvaluatedInclude.StartsWith("/")
                    && !string.Equals(Path.GetFileName(x.EvaluatedInclude), "csc.dll", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(Path.GetFileName(x.EvaluatedInclude), "csc.exe", StringComparison.OrdinalIgnoreCase))
                .Select(x => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Analyzer.ProjectFile.Path), x.EvaluatedInclude)))
                .ToList();

        public IReadOnlyList<string> GetReferences() =>
            ProjectInstance?.Items
                .Where(x => x.ItemType == "CscCommandLineArgs" && x.EvaluatedInclude.StartsWith("/reference:"))
                .Select(x => x.EvaluatedInclude.Substring(11).Trim('"'))
                .ToList();

        public IReadOnlyList<string> GetProjectReferences() =>
            ProjectInstance ?.Items
                .Where(x => x.ItemType == "ProjectReference")
                .Select(x => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Analyzer.ProjectFile.Path), x.EvaluatedInclude)))
                .ToList();
    }
}