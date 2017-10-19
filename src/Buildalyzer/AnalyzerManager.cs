using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Loader;
using System.Text;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;
using Microsoft.Build.Construction;
using System.Linq;

namespace Buildalyzer
{
    public class AnalyzerManager
    {
        private readonly Dictionary<string, ProjectAnalyzer> _projects = new Dictionary<string, ProjectAnalyzer>();

        public IReadOnlyDictionary<string, ProjectAnalyzer> Projects => _projects;

        internal ILogger<ProjectAnalyzer> ProjectLogger { get; }

        internal LoggerVerbosity LoggerVerbosity { get; }

        public string SolutionDirectory { get; }
        
        public AnalyzerManager(ILoggerFactory loggerFactory = null, LoggerVerbosity loggerVerbosity = LoggerVerbosity.Normal)
            : this(null, loggerFactory, loggerVerbosity)
        {
        }

        public AnalyzerManager(StringBuilder logBuilder, LoggerVerbosity loggerVerbosity = LoggerVerbosity.Normal)
            : this(null, logBuilder, loggerVerbosity)
        {
        }

        public AnalyzerManager(string solutionPath, ILoggerFactory loggerFactory = null, LoggerVerbosity loggerVerbosity = LoggerVerbosity.Normal)
        {
            LoggerVerbosity = loggerVerbosity;
            ProjectLogger = loggerFactory?.CreateLogger<ProjectAnalyzer>();

            if (solutionPath != null)
            {
                solutionPath = ValidatePath(solutionPath);
                SolutionDirectory = Path.GetDirectoryName(solutionPath);
                GetProjectsInSolution(solutionPath);
            }
        }

        public AnalyzerManager(string solutionPath, StringBuilder logBuilder, LoggerVerbosity loggerVerbosity = LoggerVerbosity.Normal)
        {
            LoggerVerbosity = loggerVerbosity;
            if (logBuilder != null)
            {
                LoggerFactory loggerFactory = new LoggerFactory();
                loggerFactory.AddProvider(new StringBuilderLoggerProvider(logBuilder));
                ProjectLogger = loggerFactory.CreateLogger<ProjectAnalyzer>();
            }

            if (solutionPath != null)
            {
                solutionPath = ValidatePath(solutionPath);
                SolutionDirectory = Path.GetDirectoryName(solutionPath);
                GetProjectsInSolution(solutionPath);
            }
        }

        private void GetProjectsInSolution(string solutionPath)
        {
            var supportedType = new[]
            {
                SolutionProjectType.KnownToBeMSBuildFormat,
                SolutionProjectType.WebProject
            };

            SolutionFile solution = SolutionFile.Parse(solutionPath);
            foreach(ProjectInSolution project in solution.ProjectsInOrder)
            {
                if (!supportedType.Contains(project.ProjectType))
                    continue;
                GetProject(project.AbsolutePath);
            }
        }
        
        public ProjectAnalyzer GetProject(string projectPath)
        {
            // Normalise as .sln uses backslash regardless of OS the sln is created on
            projectPath = projectPath.Replace('\\', Path.DirectorySeparatorChar);
            projectPath = ValidatePath(projectPath);
            if (_projects.TryGetValue(projectPath, out ProjectAnalyzer project))
            {
                return project;
            }
            project = new ProjectAnalyzer(this, projectPath);
            _projects.Add(projectPath, project);
            return project;
        }


        private static string ValidatePath(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }
            path = Path.GetFullPath(path); // Normalize the path
            if (!File.Exists(path))
            {
                throw new ArgumentException($"The path {path} could not be found.");
            }
            return path;
        }
    }
}
