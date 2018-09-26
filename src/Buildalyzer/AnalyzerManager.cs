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
        internal readonly static SolutionProjectType[] SupportedProjectTypes = new SolutionProjectType[]
        {
            SolutionProjectType.KnownToBeMSBuildFormat,
            SolutionProjectType.WebProject
        };

        private readonly ConcurrentDictionary<string, ProjectAnalyzer> _projects = new ConcurrentDictionary<string, ProjectAnalyzer>();

        public IReadOnlyDictionary<string, ProjectAnalyzer> Projects => _projects;

        public ILoggerFactory LoggerFactory { get; set; }

        internal IProjectTransformer ProjectTransformer { get; }
        
        internal ConcurrentDictionary<string, string> GlobalProperties { get; } = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        internal ConcurrentDictionary<string, string> EnvironmentVariables { get; } = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string SolutionFilePath { get; }

        public SolutionFile SolutionFile { get; }
        
        public AnalyzerManager(AnalyzerManagerOptions options = null)
            : this(null, options)
        {
        }

        public AnalyzerManager(string solutionFilePath, AnalyzerManagerOptions options = null)
        {
            options = options ?? new AnalyzerManagerOptions();
            LoggerFactory = options.LoggerFactory;
            ProjectTransformer = options.ProjectTransformer;

            if (!string.IsNullOrEmpty(solutionFilePath))
            {
                SolutionFilePath = NormalizePath(solutionFilePath);
                SolutionFile = SolutionFile.Parse(SolutionFilePath);

                // Initialize all the projects in the solution
                foreach (ProjectInSolution projectInSolution in SolutionFile.ProjectsInOrder)
                {
                    if (!SupportedProjectTypes.Contains(projectInSolution.ProjectType))
                    {
                        continue;
                    }
                    GetProject(projectInSolution.AbsolutePath, projectInSolution);
                }
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

        public ProjectAnalyzer GetProject(string projectFilePath) => GetProject(projectFilePath, null);

        private ProjectAnalyzer GetProject(string projectFilePath, ProjectInSolution projectInSolution)
        {
            if (projectFilePath == null)
            {
                throw new ArgumentNullException(nameof(projectFilePath));
            }

            projectFilePath = NormalizePath(projectFilePath);
            if(!File.Exists(projectFilePath))
            {
                if(projectInSolution == null)
                {
                    throw new ArgumentException($"The path {projectFilePath} could not be found.");
                }
                return null;
            }
            return _projects.GetOrAdd(projectFilePath, new ProjectAnalyzer(this, projectFilePath, projectInSolution));
        }

        internal static string NormalizePath(string path) =>
            path == null ? null : Path.GetFullPath(new Uri(path.Replace('\\', Path.DirectorySeparatorChar)).LocalPath);
    }
}
