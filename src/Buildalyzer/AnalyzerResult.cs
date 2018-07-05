using Buildalyzer.Construction;
using Microsoft.Build.Execution;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Buildalyzer
{
    public class AnalyzerResult
    {
        private readonly ProjectAnalyzer _analyzer;

        internal AnalyzerResult(ProjectAnalyzer analyzer, ProjectInstance projectInstance, BuildResult buildResult)
        {
            _analyzer = analyzer;
            ProjectInstance = projectInstance;
            BuildResult = buildResult;
        }
        
        public ProjectInstance ProjectInstance { get; }

        public BuildResult BuildResult { get; }

        public bool OverallSuccess => BuildResult.OverallResult == BuildResultCode.Success;
        
        public string TargetFramework =>
            ProjectFile.GetTargetFrameworks(
                null,  // Don't want all target frameworks since the result is just for one
                ProjectInstance?.GetProperty(ProjectFileNames.TargetFramework)?.EvaluatedValue,
                ProjectInstance?.GetProperty(ProjectFileNames.TargetFrameworkVersion)?.EvaluatedValue)
            .FirstOrDefault();

        public IReadOnlyList<string> GetSourceFiles() =>
            ProjectInstance?.Items
                .Where(x => x.ItemType == "CscCommandLineArgs" && !x.EvaluatedInclude.StartsWith("/"))
                .Select(x => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(_analyzer.ProjectFile.Path), x.EvaluatedInclude)))
                .ToList();

        public IReadOnlyList<string> GetReferences() =>
            ProjectInstance?.Items
                .Where(x => x.ItemType == "CscCommandLineArgs" && x.EvaluatedInclude.StartsWith("/reference:"))
                .Select(x => x.EvaluatedInclude.Substring(11).Trim('"'))
                .ToList();

        public IReadOnlyList<string> GetProjectReferences() =>
            ProjectInstance ?.Items
                .Where(x => x.ItemType == "ProjectReference")
                .Select(x => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(_analyzer.ProjectFile.Path), x.EvaluatedInclude)))
                .ToList();
    }
}