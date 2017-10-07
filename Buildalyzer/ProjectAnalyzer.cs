using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Logging;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using ILogger = Microsoft.Build.Framework.ILogger;
using LoggerExtensions = Microsoft.Extensions.Logging.LoggerExtensions;

namespace Buildalyzer
{
    public class ProjectAnalyzer
    {
        private readonly Dictionary<string, string> _globalProperties;
        private readonly IPathHelper _pathHelper;
        private readonly ConsoleLogger _logger;

        private Project _project = null;
        private ProjectInstance _compiledProject = null;
        
        public string ProjectPath { get; }

        /// <summary>
        /// The global properties for MSBuild. By default, each project
        /// is configured with properties that use a design-time build without calling the compiler.
        /// </summary>
        public IReadOnlyDictionary<string, string> GlobalProperties => _globalProperties;

        public Project Project => Load();

        public ProjectInstance CompiledProject => Compile();

        internal ProjectAnalyzer(Analyzer analyzer, string projectPath)
        {
            ProjectPath = projectPath;

            // Get the paths
            _pathHelper = PathHelperFactory.GetPathHelper(projectPath);

            // Preload/enforce referencing some required asemblies
            Copy copy = new Copy();
            Assembly.LoadFrom(Path.GetFullPath(Path.Combine(_pathHelper.RoslynTargetsPath, "Microsoft.Build.Tasks.CodeAnalysis.dll")));

            // Set global properties
            _globalProperties = new Dictionary<string, string>
            {
                { MsBuildProperties.SolutionDir, analyzer.SolutionDirectory ?? Path.GetDirectoryName(projectPath) },
                { MsBuildProperties.MSBuildExtensionsPath, _pathHelper.ExtensionsPath },
                { MsBuildProperties.MSBuildSDKsPath, _pathHelper.SDKsPath },
                { MsBuildProperties.RoslynTargetsPath, _pathHelper.RoslynTargetsPath },
                { MsBuildProperties.DesignTimeBuild, "true" },
                { MsBuildProperties.BuildProjectReferences, "false" },
                { MsBuildProperties.SkipCompilerExecution, "true" },
                { MsBuildProperties.ProvideCommandLineArgs, "true" }
            };
            
            // Create the logger
            if(analyzer.ProjectLogger != null)
            {
                _logger = new ConsoleLogger(analyzer.LoggerVerbosity, x => LoggerExtensions.LogInformation(analyzer.ProjectLogger, x), null, null);
            }
        }

        public Project Load()
        {
            if (_project != null)
            {
                return _project;
            }

            // Create a project collection for each project since the toolset might change depending on the type of project
            ProjectCollection projectCollection = CreateProjectCollection();

            // Load the project
            using (new BuildEnvironment(GlobalProperties))
            {
                _project = projectCollection.LoadProject(ProjectPath);
                return Project;
            }
        }

        private ProjectCollection CreateProjectCollection()
        {            
            ProjectCollection projectCollection = new ProjectCollection(_globalProperties);
            projectCollection.AddToolset(new Toolset(ToolLocationHelper.CurrentToolsVersion, _pathHelper.ToolsPath, projectCollection, string.Empty));
            projectCollection.DefaultToolsVersion = ToolLocationHelper.CurrentToolsVersion;
            if (_logger != null)
            {
                projectCollection.RegisterLogger(_logger);
            }
            return projectCollection;
        }

        public ProjectInstance Compile()
        {
            if (_compiledProject != null)
            {
                return _compiledProject;
            }
            Project project = Load();
            if (project == null)
            {
                return null;
            }
            
            // Compile the project
            using (new BuildEnvironment(GlobalProperties))
            {
                ProjectInstance projectInstance = project.CreateProjectInstance();
                if (!projectInstance.Build("Clean", _logger == null ? null : new ILogger[] { _logger }))
                {
                    return null;
                }
                if (!projectInstance.Build("Compile", _logger == null ? null : new ILogger[] { _logger }))
                {
                    return null;
                }
                _compiledProject = projectInstance;
                return _compiledProject;
            }
        }

        public IReadOnlyList<string> GetSourceFiles() => 
            Compile()?.Items
                .Where(x => x.ItemType == "CscCommandLineArgs" && !x.EvaluatedInclude.StartsWith("/"))
                .Select(x => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ProjectPath), x.EvaluatedInclude)))
                .ToList();

        public IReadOnlyList<string> GetReferences() =>
            Compile()?.Items
                .Where(x => x.ItemType == "CscCommandLineArgs" && x.EvaluatedInclude.StartsWith("/reference:"))
                .Select(x => x.EvaluatedInclude.Substring(11))
                .ToList();

        public void SetGlobalProperty(string key, string value)
        {
            if (_project != null)
            {
                throw new InvalidOperationException("Can not change global properties once project has been loaded");
            }
            _globalProperties[key] = value;
        }

        public bool RemoveGlobalProperty(string key)
        {
            if (_project != null)
            {
                throw new InvalidOperationException("Can not change global properties once project has been loaded");
            }
            return _globalProperties.Remove(key);
        }
    }
}