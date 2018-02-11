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
using System.Xml.Linq;
using Buildalyzer.Logging;

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
            : this(null, null, loggerFactory, loggerVerbosity)
        {
        }

        public AnalyzerManager(TextWriter logWriter, LoggerVerbosity loggerVerbosity = LoggerVerbosity.Normal)
            : this(null, null, logWriter, loggerVerbosity)
        {
        }
        
        public AnalyzerManager(string solutionFilePath, TextWriter logWriter = null, LoggerVerbosity loggerVerbosity = LoggerVerbosity.Normal)
            : this(solutionFilePath, null, logWriter, loggerVerbosity)
        {
        }

        public AnalyzerManager(string solutionFilePath, string[] projects, ILoggerFactory loggerFactory = null, LoggerVerbosity loggerVerbosity = LoggerVerbosity.Normal)
        {
            LoggerVerbosity = loggerVerbosity;
            ProjectLogger = loggerFactory?.CreateLogger<ProjectAnalyzer>();

            if (solutionFilePath != null)
            {
                solutionFilePath = ValidatePath(solutionFilePath, true);
                SolutionDirectory = Path.GetDirectoryName(solutionFilePath);
                GetProjectsInSolution(solutionFilePath, projects);
            }
        }

        public AnalyzerManager(string solutionFilePath, string[] projects, TextWriter logWriter = null, LoggerVerbosity loggerVerbosity = LoggerVerbosity.Normal)
        {
            LoggerVerbosity = loggerVerbosity;
            if (logWriter != null)
            {
                LoggerFactory loggerFactory = new LoggerFactory();
                loggerFactory.AddProvider(new TextWriterLoggerProvider(logWriter));
                ProjectLogger = loggerFactory.CreateLogger<ProjectAnalyzer>();
            }

            if (solutionFilePath != null)
            {
                solutionFilePath = ValidatePath(solutionFilePath, true);
                SolutionDirectory = Path.GetDirectoryName(solutionFilePath);
                GetProjectsInSolution(solutionFilePath, projects);
            }
        }

        private void GetProjectsInSolution(string solutionFilePath, string[] projects = null)
        {
            var supportedType = new[]
            {
                SolutionProjectType.KnownToBeMSBuildFormat,
                SolutionProjectType.WebProject
            };

            SolutionFile solution = SolutionFile.Parse(solutionFilePath);
            foreach(ProjectInSolution project in solution.ProjectsInOrder)
            {
                if (!supportedType.Contains(project.ProjectType)) continue;
                if (projects != null && !projects.Contains(project.ProjectName)) continue;
                GetProject(project.AbsolutePath);
            }
        }
        
        public ProjectAnalyzer GetProject(string projectFilePath)
        {
            if (projectFilePath == null)
            {
                throw new ArgumentNullException(nameof(projectFilePath));
            }

            // Normalize as .sln uses backslash regardless of OS the sln is created on
            projectFilePath = projectFilePath.Replace('\\', Path.DirectorySeparatorChar);
            projectFilePath = ValidatePath(projectFilePath, true);
            if (_projects.TryGetValue(projectFilePath, out ProjectAnalyzer project))
            {
                return project;
            }
            project = new ProjectAnalyzer(this, projectFilePath);
            _projects.Add(projectFilePath, project);
            return project;
        }

        public ProjectAnalyzer GetProject(string projectFilePath, XDocument projectDocument)
        {
            if (projectFilePath == null)
            {
                throw new ArgumentNullException(nameof(projectFilePath));
            }
            if (projectDocument == null)
            {
                throw new ArgumentNullException(nameof(projectDocument));
            }

            // Normalize as .sln uses backslash regardless of OS the sln is created on
            projectFilePath = projectFilePath.Replace('\\', Path.DirectorySeparatorChar);
            projectFilePath = ValidatePath(projectFilePath, false);
            if (_projects.TryGetValue(projectFilePath, out ProjectAnalyzer project))
            {
                return project;
            }
            project = new ProjectAnalyzer(this, projectFilePath, projectDocument);
            _projects.Add(projectFilePath, project);
            return project;
        }

        private static string ValidatePath(string path, bool checkExists)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }
            path = Path.GetFullPath(path); // Normalize the path
            if (checkExists && !File.Exists(path))
            {
                throw new ArgumentException($"The path {path} could not be found.");
            }
            return path;
        }
    }
}
