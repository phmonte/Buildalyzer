using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Logging;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using ILogger = Microsoft.Build.Framework.ILogger;
using LoggerExtensions = Microsoft.Extensions.Logging.LoggerExtensions;
using Buildalyzer.Environment;
using Microsoft.Extensions.Logging;

namespace Buildalyzer
{
    public class ProjectAnalyzer
    {
        private readonly XDocument _projectDocument;
        private readonly Dictionary<string, string> _globalProperties;
        private readonly BuildEnvironment _buildEnvironment;
        private readonly ConsoleLogger _logger;
        private BinaryLogger _binaryLogger = null;

        private Project _project = null;
        private ProjectInstance _compiledProject = null;

        public AnalyzerManager Manager { get; }
        
        public string ProjectFilePath { get; }

        /// <summary>
        /// The global properties for MSBuild. By default, each project
        /// is configured with properties that use a design-time build without calling the compiler.
        /// </summary>
        public IReadOnlyDictionary<string, string> GlobalProperties => _globalProperties;

        public Project Project => Load();

        public ProjectInstance CompiledProject => Compile();

        internal ProjectAnalyzer(AnalyzerManager manager, string projectFilePath)
            : this(manager, projectFilePath, XDocument.Load(projectFilePath))
        {
        }

        internal ProjectAnalyzer(AnalyzerManager manager, string projectFilePath, XDocument projectDocument)
        {
            Manager = manager;
            ProjectFilePath = projectFilePath;
            _projectDocument = TweakProjectDocument(manager, projectDocument);

            // Get the paths
            _buildEnvironment = EnvironmentFactory.GetBuildEnvironment(projectFilePath, _projectDocument);

            // Preload/enforce referencing some required asemblies
            Copy copy = new Copy();

            // Set global properties
            string solutionDir = manager.SolutionDirectory ?? Path.GetDirectoryName(projectFilePath);
            _globalProperties = _buildEnvironment.GetGlobalProperties(solutionDir);
            
            // Create the logger
            if(manager.ProjectLogger != null)
            {
                _logger = new ConsoleLogger(manager.LoggerVerbosity, x => manager.ProjectLogger.LogInformation(x), null, null);
            }
        }

        public ProjectAnalyzer WithBinaryLog(string binaryLogFilePath = null)
        {
            _binaryLogger = new BinaryLogger
            {
                Parameters = binaryLogFilePath ?? Path.ChangeExtension(ProjectFilePath, "binlog"),
                CollectProjectImports = BinaryLogger.ProjectImportsCollectionMode.Embed
            };
            return this;
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
            _buildEnvironment.SetEnvironmentVars(GlobalProperties);
            try
            { 
                using (XmlReader projectReader = _projectDocument.CreateReader())
                {
                    _project = projectCollection.LoadProject(projectReader);
                    _project.FullPath = ProjectFilePath;
                }
                return _project;
            }
            finally
            {
                _buildEnvironment.UnsetEnvironmentVars();
            }
        }

        // Tweaks the project file a bit to ensure a succesfull build
        private static XDocument TweakProjectDocument(AnalyzerManager manager, XDocument projectDocument)
        {
            // Add SkipGetTargetFrameworkProperties to every ProjectReference
            foreach (XElement projectReference in projectDocument.GetDescendants("ProjectReference").ToArray())
            {
                projectReference.AddChildElement("SkipGetTargetFrameworkProperties", "true");
            }

            // Removes all EnsureNuGetPackageBuildImports
            foreach (XElement ensureNuGetPackageBuildImports in
                projectDocument.GetDescendants("Target").Where(x => x.GetAttributeValue("Name") == "EnsureNuGetPackageBuildImports").ToArray())
            {
                ensureNuGetPackageBuildImports.Remove();
            }

            manager.ProjectTweaker?.Invoke(projectDocument);
            

            return projectDocument;
        }

        private ProjectCollection CreateProjectCollection()
        {            
            ProjectCollection projectCollection = new ProjectCollection(_globalProperties);
            projectCollection.RemoveAllToolsets();  // Make sure we're only using the latest tools
            projectCollection.AddToolset(new Toolset(ToolLocationHelper.CurrentToolsVersion, _buildEnvironment.GetToolsPath(), projectCollection, string.Empty));
            projectCollection.DefaultToolsVersion = ToolLocationHelper.CurrentToolsVersion;
            return projectCollection;
        }

        private IEnumerable<ILogger> GetLoggers()
        {
            if (_logger != null)
            {
                yield return _logger;
            }
            if (_binaryLogger != null)
            {
                yield return _binaryLogger;
            }
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
            _buildEnvironment.SetEnvironmentVars(GlobalProperties);
            try
            {
                ProjectInstance projectInstance = project.CreateProjectInstance();
                if (Manager.CleanBeforeCompile && !projectInstance.Build("Clean", GetLoggers()))
                {
                    return null;
                }
                if (!projectInstance.Build("Compile", GetLoggers()))
                {
                    return null;
                }
                _compiledProject = projectInstance;
                return _compiledProject;
            }
            finally
            {
                _buildEnvironment.UnsetEnvironmentVars();
            }
        }

        public IReadOnlyList<string> GetSourceFiles() => 
            Compile()?.Items
                .Where(x => x.ItemType == "CscCommandLineArgs" && !x.EvaluatedInclude.StartsWith("/"))
                .Select(x => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ProjectFilePath), x.EvaluatedInclude)))
                .ToList();

        public IReadOnlyList<string> GetReferences() =>
            Compile()?.Items
                .Where(x => x.ItemType == "CscCommandLineArgs" && x.EvaluatedInclude.StartsWith("/reference:"))
                .Select(x => x.EvaluatedInclude.Substring(11).Trim('"'))
                .ToList();

        public IReadOnlyList<string> GetProjectReferences() =>
            Compile()?.Items
                .Where(x => x.ItemType == "ProjectReference")
                .Select(x => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ProjectFilePath), x.EvaluatedInclude)))
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