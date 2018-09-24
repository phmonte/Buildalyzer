using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Buildalyzer.Construction;
using Buildalyzer.Environment;
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;

namespace Buildalyzer
{
    public class AnalyzerManager
    {
        private readonly ConcurrentDictionary<string, ProjectAnalyzer> _projects = new ConcurrentDictionary<string, ProjectAnalyzer>();

        public IReadOnlyDictionary<string, ProjectAnalyzer> Projects => _projects;

        public ILoggerFactory LoggerFactory { get; set; }

        internal IProjectTransformer ProjectTransformer { get; }
        
        internal ConcurrentDictionary<string, string> GlobalProperties { get; } = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        internal ConcurrentDictionary<string, string> EnvironmentVariables { get; } = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string SolutionDirectory { get; }

        public AnalyzerManager(AnalyzerManagerOptions options = null)
            : this(null, null, options)
        {
        }

        public AnalyzerManager(string solutionFilePath, AnalyzerManagerOptions options = null)
            : this(solutionFilePath, null, options)
        {
        }

        public AnalyzerManager(string solutionFilePath, string[] projects, AnalyzerManagerOptions options = null)
        {
            options = options ?? new AnalyzerManagerOptions();
            LoggerFactory = options.LoggerFactory;
            ProjectTransformer = options.ProjectTransformer;

            if (solutionFilePath != null)
            {
                solutionFilePath = NormalizeAndValidatePath(solutionFilePath);
                SolutionDirectory = Path.GetDirectoryName(solutionFilePath);
                GetProjectsInSolution(solutionFilePath, projects);
            }
        }        

        public static IEnumerable<ProjectInSolution> GetProjectsInSolution(string solutionFilePath)
        {
            SolutionProjectType[] supportedType = new[]
            {
                SolutionProjectType.KnownToBeMSBuildFormat,
                SolutionProjectType.WebProject
            };

            SolutionFile solution = SolutionFile.Parse(solutionFilePath);
            foreach (ProjectInSolution project in solution.ProjectsInOrder)
            {
                if (!supportedType.Contains(project.ProjectType)) continue;
                yield return project;
            }
        }

        private void GetProjectsInSolution(string solutionFilePath, string[] projects)
        {
            foreach(ProjectInSolution project in GetProjectsInSolution(solutionFilePath))
            {
                if (projects != null && !projects.Contains(project.ProjectName)) continue;
                GetProject(project.AbsolutePath);
            }
        }

        public void SetGlobalProperty(string key, string value)
        {
            GlobalProperties[key] = value;
        }

        public void RemoveGlobalProperty(string key)
        {
            // Nulls are removed before passing to MSBuild and can be used to ignore values in lower-precedence collections
            GlobalProperties[key] = null;
        }

        public void SetEnvironmentVariable(string key, string value)
        {
            EnvironmentVariables[key] = value;
        }

        public ProjectAnalyzer GetProject(string projectFilePath)
        {
            if (projectFilePath == null)
            {
                throw new ArgumentNullException(nameof(projectFilePath));
            }

            projectFilePath = NormalizeAndValidatePath(projectFilePath);
            return _projects.GetOrAdd(projectFilePath, new ProjectAnalyzer(this, projectFilePath));
        }

        private static string NormalizeAndValidatePath(string path)
        {
            path = NormalizePath(path);
            if (!File.Exists(path))
            {
                throw new ArgumentException($"The path {path} could not be found.");
            }
            return path;
        }

        internal static string NormalizePath(string path) =>
            path == null ? null : Path.GetFullPath(new Uri(path.Replace('\\', Path.DirectorySeparatorChar)).LocalPath);
    }
}
