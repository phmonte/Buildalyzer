using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Buildalyzer.Environment;
using Buildalyzer.Logging;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;

namespace Buildalyzer
{
    public class AnalyzerManager
    {
        private readonly Dictionary<string, ProjectAnalyzer> _projects = new Dictionary<string, ProjectAnalyzer>();

        public IReadOnlyDictionary<string, ProjectAnalyzer> Projects => _projects;

        internal ILogger<ProjectAnalyzer> ProjectLogger { get; }

        internal LoggerVerbosity LoggerVerbosity { get; }
        
        internal Action<XDocument> ProjectTweaker { get; }

        public string SolutionDirectory { get; }
        
        public AnalyzerManager(ILoggerFactory loggerFactory = null, LoggerVerbosity loggerVerbosity = LoggerVerbosity.Normal, Action<XDocument> projectTweaker = null)
            : this(null, null, loggerFactory, loggerVerbosity, projectTweaker)
        {
        }

        public AnalyzerManager(TextWriter logWriter, LoggerVerbosity loggerVerbosity = LoggerVerbosity.Normal, Action<XDocument> projectTweaker = null)
            : this(null, null, logWriter, loggerVerbosity, projectTweaker)
        {
        }
        
        public AnalyzerManager(string solutionFilePath, TextWriter logWriter = null, LoggerVerbosity loggerVerbosity = LoggerVerbosity.Normal, Action<XDocument> projectTweaker = null)
            : this(solutionFilePath, null, logWriter, loggerVerbosity, projectTweaker)
        {
        }

        public AnalyzerManager(string solutionFilePath, string[] projects, ILoggerFactory loggerFactory = null, LoggerVerbosity loggerVerbosity = LoggerVerbosity.Normal, Action<XDocument> projectTweaker = null)
        {
            LoggerVerbosity = loggerVerbosity;
            ProjectLogger = loggerFactory?.CreateLogger<ProjectAnalyzer>();
            ProjectTweaker = projectTweaker;

            if (solutionFilePath != null)
            {
                solutionFilePath = ValidatePath(solutionFilePath, true);
                SolutionDirectory = Path.GetDirectoryName(solutionFilePath);
                GetProjectsInSolution(solutionFilePath, projects);
            }
        }

        public AnalyzerManager(string solutionFilePath, string[] projects, TextWriter logWriter = null, LoggerVerbosity loggerVerbosity = LoggerVerbosity.Normal, Action<XDocument> projectTweaker = null)
        {
            LoggerVerbosity = loggerVerbosity;
            ProjectTweaker = projectTweaker;
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

            return GetProjectInternal(projectFilePath, null, true, null);
        }

        public ProjectAnalyzer GetProject(string projectFilePath, BuildEnvironment buildEnvironment)
        {
            if (projectFilePath == null)
            {
                throw new ArgumentNullException(nameof(projectFilePath));
            }
            if (buildEnvironment == null)
            {
                throw new ArgumentNullException(nameof(buildEnvironment));
            }

            return GetProjectInternal(projectFilePath, null, true, buildEnvironment);
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

            return GetProjectInternal(projectFilePath, projectDocument, false, null);
        }

        public ProjectAnalyzer GetProject(string projectFilePath, XDocument projectDocument, BuildEnvironment buildEnvironment)
        {
            if (projectFilePath == null)
            {
                throw new ArgumentNullException(nameof(projectFilePath));
            }
            if (projectDocument == null)
            {
                throw new ArgumentNullException(nameof(projectDocument));
            }
            if (buildEnvironment == null)
            {
                throw new ArgumentNullException(nameof(buildEnvironment));
            }

            return GetProjectInternal(projectFilePath, projectDocument, false, buildEnvironment);
        }

        private ProjectAnalyzer GetProjectInternal(string projectFilePath, XDocument projectDocument, bool checkExists, BuildEnvironment buildEnvironment)
        {
            ValidateBuildEnvironment(buildEnvironment);

            // Normalize as .sln uses backslash regardless of OS the sln is created on
            projectFilePath = projectFilePath.Replace('\\', Path.DirectorySeparatorChar);
            projectFilePath = ValidatePath(projectFilePath, checkExists);
            if (_projects.TryGetValue(projectFilePath, out ProjectAnalyzer project))
            {
                return project;
            }
            project = new ProjectAnalyzer(this, projectFilePath, projectDocument, buildEnvironment);
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

        private static void ValidateBuildEnvironment(BuildEnvironment buildEnvironment)
        {
            if (buildEnvironment == null)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(buildEnvironment.ToolsPath))
            {
                throw new ArgumentException($"The value for {nameof(BuildEnvironment)}.{nameof(buildEnvironment.ToolsPath)} must be provided.");
            }
            if (string.IsNullOrWhiteSpace(buildEnvironment.ExtensionsPath))
            {
                throw new ArgumentException($"The value for {nameof(BuildEnvironment)}.{nameof(buildEnvironment.ExtensionsPath)} must be provided.");
            }
            if (string.IsNullOrWhiteSpace(buildEnvironment.SDKsPath))
            {
                throw new ArgumentException($"The value for {nameof(BuildEnvironment)}.{nameof(buildEnvironment.SDKsPath)} must be provided.");
            }
            if (string.IsNullOrWhiteSpace(buildEnvironment.RoslynTargetsPath))
            {
                throw new ArgumentException($"The value for {nameof(BuildEnvironment)}.{nameof(buildEnvironment.RoslynTargetsPath)} must be provided.");
            }
        }
    }
}
