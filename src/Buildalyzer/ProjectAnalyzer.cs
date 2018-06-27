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
        private readonly ConsoleLogger _logger;
        private BinaryLogger _binaryLogger = null;
        
        private Project _project = null;
        private ProjectInstance _projectInstance = null;

        // Project-specific global properties and environment variables
        private readonly Dictionary<string, string> _globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _environmentVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public AnalyzerManager Manager { get; }

        public ProjectFile ProjectFile { get; }

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

        public ProjectInstance ProjectInstance => Build();

        public BuildEnvironment BuildEnvironment { get; private set; }

        public string TargetFramework { get; private set; }

        internal ProjectAnalyzer(AnalyzerManager manager, string projectFilePath, XDocument projectDocument, BuildEnvironment buildEnvironment)
        {
            Manager = manager;
            ProjectFile = new ProjectFile(projectFilePath, projectDocument, manager.ProjectTransformer);
            SetTargetFramework(null, false);
            SetBuildEnvironment(buildEnvironment);

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

        /// <summary>
        /// Invalidates the cached build data and will result in new builds.
        /// </summary>
        public void InvalidateCache()
        {
            _project = null;
            _projectInstance = null;
            Manager.BuildManager.ResetCaches();
        }

        /// <summary>
        /// Sets the build environment that should be used.
        /// </summary>
        /// <param name="buildEnvironment">
        /// The new build environment.
        /// Passing in <c>null</c> indicates that the build environment should be reset to the default value.
        /// This will invalidate all cached build result data and result in new builds.
        /// </param>
        public void SetBuildEnvironment(BuildEnvironment buildEnvironment)
        {
            BuildEnvironment = buildEnvironment ?? new EnvironmentFactory(Manager, ProjectFile).GetBuildEnvironment(TargetFramework);
            InvalidateCache();
        }

        /// <summary>
        /// Sets the target framework to be used for builds.
        /// </summary>
        /// <param name="targetFramework">
        /// The target framework to use.
        /// Passing in <c>null</c> indicates that the target framework should be reset to the default value
        /// (the first target framework in the project file).
        /// This will invalidate all cached build result data and result in new builds.
        /// </param>
        /// <param name="recalculateBuildEnvironment">
        /// Indicates if the build environment should be recalculated when changing the target framework.
        /// </param>
        public void SetTargetFramework(string targetFramework, bool recalculateBuildEnvironment = true)
        {
            TargetFramework = string.IsNullOrWhiteSpace(targetFramework)
                ? ProjectFile.TargetFrameworks.FirstOrDefault()
                : targetFramework;
            if (recalculateBuildEnvironment)
            {
                SetBuildEnvironment(null);
            }
            InvalidateCache();
        }
        
        public ProjectAnalyzer WithBinaryLog(string binaryLogFilePath = null)
        {
            _binaryLogger = new BinaryLogger
            {
                Parameters = binaryLogFilePath ?? Path.ChangeExtension(ProjectFile.Path, "binlog"),
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
            var effectiveGlobalProperties = GetEffectiveGlobalProperties();
            ProjectCollection projectCollection = CreateProjectCollection(effectiveGlobalProperties);

            // Load the project
            using (new TemporaryEnvironment(GetEffectiveEnvironmentVariables()))
            {               
                using (XmlReader projectReader = ProjectFile.CreateReader(TargetFramework))
                {
                    ProjectRootElement xml = ProjectRootElement.Create(projectReader, projectCollection);

                    // When constructing a project from an XmlReader, MSBuild cannot determine the project file path.  Setting the
                    // path explicitly is necessary so that the reserved properties like $(MSBuildProjectDirectory) will work.
                    xml.FullPath = ProjectFile.Path;

                    _project = new Project(xml, effectiveGlobalProperties, null, projectCollection);
                }
                return _project;
            }
        }

        public ProjectInstance Build()
        {
            if (_projectInstance != null)
            {
                return _projectInstance;
            }
            Project project = Load();
            if (project == null)
            {
                return null;
            }

            // Some project types can't be built from .NET Core
            if (BuildEnvironment.IsRunningOnCore)
            {
                // Portable projects
                if (ProjectFile.IsPortable)
                {
                    throw new Exception("Can't build portable class library projects from a .NET Core host");
                }

                // Legacy framework projects with PackageReference
                if (!ProjectFile.UsesSdk && ProjectFile.ContainsPackageReferences)
                {
                    throw new Exception("Can't build legacy projects that contain PackageReference from a .NET Core host");
                }
            }

            // Build the project
            using (new TemporaryEnvironment(GetEffectiveEnvironmentVariables()))
            {
                ProjectInstance projectInstance = Manager.BuildManager.GetProjectInstanceForBuild(project);

                // This is essentialy what ProjectInstance.Build() does, but it copies the BuildParameters
                // from the ProjectCollection which is necessary for nested builds since we replaced the toolset
                if (BuildEnvironment.Targets.Length > 0)
                {
                    BuildResult buildResult = Manager.BuildManager.Build(
                        new BuildParameters(project.ProjectCollection)
                        {
                            Loggers = GetLoggers()
                        },
                        new BuildRequestData(projectInstance, BuildEnvironment.Targets));
                    if (buildResult.OverallResult != BuildResultCode.Success)
                    {
                        return null;
                    }
                }
                _projectInstance = projectInstance;
                return _projectInstance;
            }
        }

        public IReadOnlyList<string> GetSourceFiles() =>
            Build()?.Items
                .Where(x => x.ItemType == "CscCommandLineArgs" && !x.EvaluatedInclude.StartsWith("/"))
                .Select(x => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ProjectFile.Path), x.EvaluatedInclude)))
                .ToList();

        public IReadOnlyList<string> GetReferences() =>
            Build()?.Items
                .Where(x => x.ItemType == "CscCommandLineArgs" && x.EvaluatedInclude.StartsWith("/reference:"))
                .Select(x => x.EvaluatedInclude.Substring(11).Trim('"'))
                .ToList();

        public IReadOnlyList<string> GetProjectReferences() =>
            Build()?.Items
                .Where(x => x.ItemType == "ProjectReference")
                .Select(x => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ProjectFile.Path), x.EvaluatedInclude)))
                .ToList();

        public void SetGlobalProperty(string key, string value)
        {
            _globalProperties[key] = value;
            InvalidateCache();
        }

        public void RemoveGlobalProperty(string key)
        {
            // Nulls are removed before passing to MSBuild and can be used to ignore values in lower-precedence collections
            _globalProperties[key] = null;
            InvalidateCache();
        }

        public void SetEnvironmentVariable(string key, string value)
        {
            _environmentVariables[key] = value;
            InvalidateCache();
        }

        // Note the order of precedence (from least to most)
        private Dictionary<string, string> GetEffectiveGlobalProperties()
            => GetEffectiveDictionary(
                true,  // Remove nulls to avoid passing null global properties. But null can be used in higher-precident dictionaries to ignore a lower-precident dictionary's value.
                BuildEnvironment.GlobalProperties,
                Manager.GlobalProperties,
                _globalProperties);

        // Note the order of precedence (from least to most)
        private Dictionary<string, string> GetEffectiveEnvironmentVariables()
            => GetEffectiveDictionary(
                false, // Don't remove nulls as a null value will unset the env var which may be set by a calling process.
                BuildEnvironment.EnvironmentVariables,
                Manager.EnvironmentVariables,
                _environmentVariables);

        private static Dictionary<string, string> GetEffectiveDictionary(
            bool removeNulls,
            params IReadOnlyDictionary<string, string>[] innerDictionaries)
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

        private ProjectCollection CreateProjectCollection(IDictionary<string, string> globalProperties)
        {
            ProjectCollection projectCollection = new ProjectCollection(globalProperties);
            projectCollection.RemoveAllToolsets();  // Make sure we're only using the latest tools
            projectCollection.AddToolset(new Toolset(ToolLocationHelper.CurrentToolsVersion, BuildEnvironment.ToolsPath, projectCollection, string.Empty));
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
    }
}