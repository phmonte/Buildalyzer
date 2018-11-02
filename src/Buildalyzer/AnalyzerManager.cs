using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using Buildalyzer.Construction;
using Buildalyzer.Environment;
using Buildalyzer.Logging;
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Logging.StructuredLogger;
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

        /// <summary>
        /// This maps Roslyn project IDs to full normalized project file paths of references (since the Roslyn Project doesn't provide access to this data)
        /// which allows us to match references with Roslyn projects that already exist in the Workspace/Solution (instead of rebuilding them).
        /// This cache exists in <see cref="AnalyzerManager"/> so that it's lifetime can be controlled and it can be collected when <see cref="AnalyzerManager"/> goes out of scope.
        /// </summary>
        internal ConcurrentDictionary<Guid, string[]> WorkspaceProjectReferences = new ConcurrentDictionary<Guid, string[]>();

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

        /// <summary>
        /// Analyzes an MSBuild binary log file.
        /// </summary>
        /// <param name="binLogPath">The path to the binary log file.</param>
        /// <param name="buildLoggers">MSBuild loggers to replay events from the log to.</param>
        /// <returns>A dictionary of target frameworks to <see cref="AnalyzerResult"/>.</returns>
        public AnalyzerResults Analyze(string binLogPath, IEnumerable<Microsoft.Build.Framework.ILogger> buildLoggers = null)
        {
            binLogPath = NormalizePath(binLogPath);
            if(!File.Exists(binLogPath))
            {
                throw new ArgumentException($"The path {binLogPath} could not be found.");
            }

            BinLogReader reader = new BinLogReader();
            using (EventProcessor eventProcessor = new EventProcessor(this, null, buildLoggers, reader, true))
            {
                reader.Replay(binLogPath);
                return new AnalyzerResults
                {
                    { eventProcessor.Results, eventProcessor.OverallSuccess }
                };
            }
        }

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
            path == null ? null : Path.GetFullPath(path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar));
    }
}
