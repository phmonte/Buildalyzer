using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Buildalyzer.Environment;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Logging;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Build.Framework.ILogger;

namespace Buildalyzer
{
    public class ProjectAnalyzer
    {
        private readonly XDocument _projectDocument;
        private readonly BuildEnvironment _buildEnvironment;
        private readonly ConsoleLogger _logger;
        private BinaryLogger _binaryLogger = null;

        private Project _project = null;
        private ProjectInstance _compiledProject = null;

        // Project-specific global properties and environment variables
        private readonly Dictionary<string, string> _globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _environmentVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public AnalyzerManager Manager { get; }

        public string ProjectFilePath { get; }

        /// <summary>
        /// The global properties for MSBuild. By default, each project
        /// is configured with properties that use a design-time build without calling the compiler.
        /// </summary>
        public IReadOnlyDictionary<string, string> GlobalProperties => new ReadOnlyDictionary<string, string>(GetEffectiveGlobalProperties());

        /// <summary>
        /// The environment variables used for MSBuild. By default, each project
        /// is configured with variables which help point to configured MSBuild paths.
        /// </summary>
        public IReadOnlyDictionary<string, string> EnvironmentVariables => new ReadOnlyDictionary<string, string>(GetEffectiveEnvironmentVariables());

        public Project Project => Load();

        public ProjectInstance CompiledProject => Compile();

        internal ProjectAnalyzer(AnalyzerManager manager, string projectFilePath, XDocument projectDocument, BuildEnvironment buildEnvironment)
        {
            Manager = manager;
            ProjectFilePath = projectFilePath;
            _projectDocument = projectDocument ?? XDocument.Load(projectFilePath);
            manager.ProjectTransformer.Apply(_projectDocument);

            // Get the paths
            _buildEnvironment = buildEnvironment ?? EnvironmentFactory.GetBuildEnvironment(projectFilePath, _projectDocument);

            // Preload/enforce referencing some required asemblies
            Copy copy = new Copy();

            // Set the solution directory global property
            string solutionDir = manager.SolutionDirectory ?? Path.GetDirectoryName(projectFilePath);
            SetGlobalProperty(MsBuildProperties.SolutionDir, solutionDir);

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

            var effectiveGlobalProperties = GetEffectiveGlobalProperties();
            var effectiveEnvironmentVariables = GetEffectiveEnvironmentVariables();

            // Create a project collection for each project since the toolset might change depending on the type of project
            ProjectCollection projectCollection = CreateProjectCollection(effectiveGlobalProperties);

            // Load the project
            using (new TemporaryEnvironment(effectiveEnvironmentVariables))
            {               
                using (XmlReader projectReader = _projectDocument.CreateReader())
                {
                    var xml = ProjectRootElement.Create(projectReader, projectCollection);

                    // When constructing a project from an XmlReader, MSBuild cannot determine the project file path.  Setting the
                    // path explicitly is necessary so that the reserved properties like $(MSBuildProjectDirectory) will work.
                    xml.FullPath = ProjectFilePath;

                    _project = new Project(xml, effectiveGlobalProperties, null, projectCollection);
                }
                return _project;
            }
        }

        private ProjectCollection CreateProjectCollection(IDictionary<string, string> globalProperties)
        {
            ProjectCollection projectCollection = new ProjectCollection(globalProperties);
            projectCollection.RemoveAllToolsets();  // Make sure we're only using the latest tools
            projectCollection.AddToolset(new Toolset(ToolLocationHelper.CurrentToolsVersion, _buildEnvironment.ToolsPath, projectCollection, string.Empty));
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

            var effectiveEnvironmentVariables = GetEffectiveEnvironmentVariables();

            // Compile the project
            using (new TemporaryEnvironment(effectiveEnvironmentVariables))
            {
                ProjectInstance projectInstance = Manager.BuildManager.GetProjectInstanceForBuild(project);
                List<string> targets = new List<string>();
                if(Manager.CleanBeforeCompile)
                {
                    targets.Add("Clean");
                }
                targets.Add("Compile");

                // This is essentialy what ProjectInstance.Build() does, but it copies the BuildParameters
                // from the ProjectCollection which is necessary for nested builds since we replaced the toolset
                BuildResult buildResult = Manager.BuildManager.Build(
                    new BuildParameters(project.ProjectCollection)
                    {
                        Loggers = GetLoggers()
                    },
                    new BuildRequestData(projectInstance, targets.ToArray()));
                if (buildResult.OverallResult != BuildResultCode.Success)
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

        public void RemoveGlobalProperty(string key)
        {
            if (_project != null)
            {
                throw new InvalidOperationException("Can not change global properties once project has been loaded");
            }

            // Nulls are removed before passing to MSBuild and can be used to ignore values in lower-precedence collections
            _globalProperties[key] = null;
        }

        public void SetEnvironmentVariable(string key, string value)
        {
            if (_project != null)
            {
                throw new InvalidOperationException("Can not change environment variables once project has been loaded");
            }
            _environmentVariables[key] = value;
        }

        // Note the order of precedence (from least to most)
        private Dictionary<string, string> GetEffectiveGlobalProperties()
            => GetEffectiveDictionary(
                true,  // Remove nulls to avoid passing null global properties. But null can be used in higher-precident dictionaries to ignore a lower-precident dictionary's value.
                _buildEnvironment.GlobalProperties,
                Manager.GlobalProperties,
                _globalProperties);

        // Note the order of precedence (from least to most)
        private Dictionary<string, string> GetEffectiveEnvironmentVariables()
            => GetEffectiveDictionary(
                false, // Don't remove nulls as a null value will unset the env var which may be set by a calling process.
                _buildEnvironment.EnvironmentVariables,
                Manager.EnvironmentVariables,
                _environmentVariables);

        private static Dictionary<string, string> GetEffectiveDictionary(
            bool removeNulls,
            params IDictionary<string, string>[] innerDictionaries)
        {
            var effectiveDictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var innerDictionary in innerDictionaries)
            {
                foreach (var pair in innerDictionary)
                {
                    if (removeNulls && pair.Value == null)
                    {
                        effectiveDictionary.Remove(pair.Key);
                    }
                    else
                    {
                        effectiveDictionary[pair.Key] = pair.Value;
                    }
                }
            }

            return effectiveDictionary;
        }
    }
}