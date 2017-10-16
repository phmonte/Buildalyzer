using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Loader;
using System.Text;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;

namespace Buildalyzer
{
    public class AnalyzerManager
    {
        private readonly Dictionary<string, ProjectAnalyzer> _projects = new Dictionary<string, ProjectAnalyzer>();

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

        public AnalyzerManager(string solutionDirectory, ILoggerFactory loggerFactory = null, LoggerVerbosity loggerVerbosity = LoggerVerbosity.Normal)
        {
            LoggerVerbosity = loggerVerbosity;
            SolutionDirectory = solutionDirectory == null ? null : Path.GetFullPath(solutionDirectory);
            ProjectLogger = loggerFactory?.CreateLogger<ProjectAnalyzer>();
        }

        public AnalyzerManager(string solutionDirectory, StringBuilder logBuilder, LoggerVerbosity loggerVerbosity = LoggerVerbosity.Normal)
        {
            LoggerVerbosity = loggerVerbosity;
            SolutionDirectory = solutionDirectory == null ? null : Path.GetFullPath(solutionDirectory);
            if (logBuilder != null)
            {
                LoggerFactory loggerFactory = new LoggerFactory();
                loggerFactory.AddProvider(new StringBuilderLoggerProvider(logBuilder));
                ProjectLogger = loggerFactory.CreateLogger<ProjectAnalyzer>();
            }
        }

        public IReadOnlyDictionary<string, ProjectAnalyzer> Projects => _projects;
        
        public ProjectAnalyzer GetProject(string projectPath)
        {
            projectPath = ValidateProjectPath(projectPath);
            if (_projects.TryGetValue(projectPath, out ProjectAnalyzer project))
            {
                return project;
            }
            project = new ProjectAnalyzer(this, projectPath);
            _projects.Add(projectPath, project);
            return project;
        }

        private static string ValidateProjectPath(string projectPath)
        {
            if (projectPath == null)
            {
                throw new ArgumentNullException(nameof(projectPath));
            }
            projectPath = Path.GetFullPath(projectPath); // Normalize the path
            if (!File.Exists(projectPath))
            {
                throw new ArgumentException($"The project file {projectPath} could not be found.");
            }
            return projectPath;
        }
    }
}
