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
        private readonly List<ILogger> _loggers = new List<ILogger>();
        
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

        public IEnumerable<ILogger> Loggers => _loggers;

        internal ProjectAnalyzer(AnalyzerManager manager, string projectFilePath, XDocument projectDocument, BuildEnvironment buildEnvironment, EnvironmentOptions environmentOptions)
        {
            Manager = manager;
            ProjectFile = new ProjectFile(projectFilePath, projectDocument, manager.ProjectTransformer);
            ResetTargetFramework(false);
            if (buildEnvironment != null)
            {
                SetBuildEnvironment(buildEnvironment);
            }
            else if(environmentOptions != null)
            {
                SetBuildEnvironment(environmentOptions);
            }
            else
            {
                ResetBuildEnvironment();
            }

            // Preload/enforce referencing some required asemblies
            Copy copy = new Copy();

            // Set the solution directory global property
            string solutionDir = manager.SolutionDirectory ?? Path.GetDirectoryName(projectFilePath);
            SetGlobalProperty(MsBuildProperties.SolutionDir, solutionDir);

            // Create the logger
            if(manager.ProjectLogger != null)
            {
                AddLogger(new ConsoleLogger(manager.LoggerVerbosity, x => manager.ProjectLogger.LogInformation(x), null, null));
            }
        }

        /// <summary>
        /// Invalidates the cached build data and will result in new builds.
        /// </summary>
        public void InvalidateCache()
        {
            _project = null;
            _projectInstance = null;
        }

        /// <summary>
        /// Sets the build environment that should be used.
        /// This will invalidate all cached build result data and result in new builds.
        /// </summary>
        /// <param name="buildEnvironment">
        /// The new build environment.
        /// </param>
        public void SetBuildEnvironment(BuildEnvironment buildEnvironment)
        {
            BuildEnvironment = buildEnvironment ?? throw new ArgumentNullException(nameof(buildEnvironment));
            InvalidateCache();
        }

        /// <summary>
        /// Sets the build environment that should be used by specifiying options.
        /// This will invalidate all cached build result data and result in new builds.
        /// </summary>
        /// <param name="options">
        /// The new build environment options.
        /// </param>
        public void SetBuildEnvironment(EnvironmentOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            BuildEnvironment = new EnvironmentFactory(Manager, ProjectFile, options).GetBuildEnvironment(TargetFramework);
            InvalidateCache();
        }

        /// <summary>
        /// Resets the build environment to default values.
        /// This will invalidate all cached build result data and result in new builds.
        /// </summary>
        public void ResetBuildEnvironment()
        {
            BuildEnvironment = new EnvironmentFactory(Manager, ProjectFile, null).GetBuildEnvironment(TargetFramework);
            InvalidateCache();
        }

        /// <summary>
        /// Creates a new <see cref="BuildEnvironment"/> and modifies the build targets
        /// to the specified targets.
        /// This will invalidate all cached build result data and result in new builds.
        /// </summary>
        /// <param name="targetsToBuild">The targets to build.</param>
        public void SetTargetsToBuild(params string[] targetsToBuild) =>
            SetBuildEnvironment(BuildEnvironment.WithTargetsToBuild(targetsToBuild));

        /// <summary>
        /// Sets the target framework to be used for builds.
        /// This will invalidate all cached build result data and result in new builds.
        /// </summary>
        /// <param name="targetFramework">
        /// The target framework to use.
        /// </param>
        /// <param name="resetBuildEnvironment">
        /// Indicates if the build environment should be recalculated when changing the target framework.
        /// If this is <c>true</c> it will also reset the targets to build.
        /// </param>
        public void SetTargetFramework(string targetFramework, bool resetBuildEnvironment = true)
        {
            TargetFramework = targetFramework ?? throw new ArgumentNullException(nameof(targetFramework));
            if (resetBuildEnvironment)
            {
                ResetBuildEnvironment();
            }
            InvalidateCache();
        }

        /// <summary>
        /// Resets the target framework to be used for builds to the first target framework in the project file.
        /// This will invalidate all cached build result data and result in new builds.
        /// </summary>
        /// <param name="recalculateBuildEnvironment">
        /// Indicates if the build environment should be recalculated when changing the target framework.
        /// If this is <c>true</c> it will also reset the targets to build.
        /// </param>
        public void ResetTargetFramework(bool recalculateBuildEnvironment = true)
        {
            TargetFramework = ProjectFile.TargetFrameworks.FirstOrDefault();
            if (recalculateBuildEnvironment)
            {
                ResetBuildEnvironment();
            }
            InvalidateCache();
        }

        public Project Load()
        {
            if (_project != null)
            {
                return _project;
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

                    _project = new Project(
                        xml,
                        effectiveGlobalProperties,
                        null,
                        projectCollection);
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

            // Reset the cache before every build in case MSBuild cached something from a project reference build
            Manager.BuildManager.ResetCaches();

            Project project = Load();
            if (project == null)
            {
                return null;
            }
            
            // Build the project
            using (new TemporaryEnvironment(GetEffectiveEnvironmentVariables()))
            {
                ProjectInstance projectInstance = Manager.BuildManager.GetProjectInstanceForBuild(project);

                // This is essentialy what ProjectInstance.Build() does, but it copies the BuildParameters
                // from the ProjectCollection which is necessary for nested builds since we replaced the toolset
                if (BuildEnvironment.TargetsToBuild.Length > 0)
                {
                    BuildResult buildResult = Manager.BuildManager.Build(
                        new BuildParameters(project.ProjectCollection)
                        {
                            Loggers = Loggers,
                            ProjectLoadSettings = ProjectLoadSettings.RecordEvaluatedItemElements
                        },
                        new BuildRequestData(projectInstance, BuildEnvironment.TargetsToBuild));
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

        public void AddBinaryLogger(string binaryLogFilePath = null) =>
            AddLogger(new BinaryLogger
            {
                Parameters = binaryLogFilePath ?? Path.ChangeExtension(ProjectFile.Path, "binlog"),
                CollectProjectImports = BinaryLogger.ProjectImportsCollectionMode.Embed,
                Verbosity = Microsoft.Build.Framework.LoggerVerbosity.Diagnostic
            });

        public void AddLogger(ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            _loggers.Add(logger);
        }

        public void RemoveLogger(ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            _loggers.Remove(logger);
        }
    }
}