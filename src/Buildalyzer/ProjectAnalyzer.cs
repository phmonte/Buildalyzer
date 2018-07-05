using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Buildalyzer.Construction;
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

        public BuildEnvironment BuildEnvironment { get; private set; }

        public IEnumerable<ILogger> Loggers => _loggers;

        internal ProjectAnalyzer(AnalyzerManager manager, string projectFilePath, XDocument projectDocument, BuildEnvironment buildEnvironment, EnvironmentOptions environmentOptions)
        {
            Manager = manager;
            ProjectFile = new ProjectFile(projectFilePath, projectDocument, manager.ProjectTransformer);
            if (buildEnvironment != null)
            {
                SetBuildEnvironment(buildEnvironment);
            }
            else if (environmentOptions != null)
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
        /// Sets the build environment that should be used.
        /// This will invalidate all cached build result data and result in new builds.
        /// </summary>
        /// <param name="buildEnvironment">
        /// The new build environment.
        /// </param>
        public void SetBuildEnvironment(BuildEnvironment buildEnvironment)
        {
            BuildEnvironment = buildEnvironment ?? throw new ArgumentNullException(nameof(buildEnvironment));
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

            BuildEnvironment = new EnvironmentFactory(Manager, ProjectFile, options).GetBuildEnvironment();
        }

        /// <summary>
        /// Resets the build environment to default values.
        /// This will invalidate all cached build result data and result in new builds.
        /// </summary>
        public void ResetBuildEnvironment()
        {
            BuildEnvironment = new EnvironmentFactory(Manager, ProjectFile, null).GetBuildEnvironment();
        }

        /// <summary>
        /// Creates a new <see cref="BuildEnvironment"/> and modifies the build targets
        /// to the specified targets.
        /// This will invalidate all cached build result data and result in new builds.
        /// </summary>
        /// <param name="targetsToBuild">The targets to build.</param>
        public void SetTargetsToBuild(params string[] targetsToBuild) =>
            SetBuildEnvironment(BuildEnvironment.WithTargetsToBuild(targetsToBuild));

        public Project Load() => Load(null);

        public Project Load(string targetFramework)
        {
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
            Dictionary<string, string> effectiveGlobalProperties = GetEffectiveGlobalProperties();
            if(!string.IsNullOrEmpty(targetFramework))
            {
                effectiveGlobalProperties[MsBuildProperties.TargetFramework] = targetFramework;
            }
            ProjectCollection projectCollection = CreateProjectCollection(effectiveGlobalProperties);

            // Load the project
            using (new TemporaryEnvironment(GetEffectiveEnvironmentVariables()))
            {
                using (XmlReader projectReader = ProjectFile.CreateReader())
                {
                    ProjectRootElement root = ProjectRootElement.Create(projectReader, projectCollection);

                    // When constructing a project from an XmlReader, MSBuild cannot determine the project file path.  Setting the
                    // path explicitly is necessary so that the reserved properties like $(MSBuildProjectDirectory) will work.
                    root.FullPath = ProjectFile.Path;

                    return new Project(
                        root,
                        effectiveGlobalProperties,
                        ToolLocationHelper.CurrentToolsVersion,
                        projectCollection);
                }
            }
        }
        
        /// <summary>
        /// Builds all target framework(s).
        /// </summary>
        /// <returns>A dictionary of target frameworks to <see cref="AnalyzerResult"/>.</returns>
        public AnalyzerResults Build()
        {
            // Load the project to get the evaluated target frameworks
            Project project = Load();

            // Get all evaluated target frameworks from the Project and build them
            // but don't worry about getting a single target framework, it'll build the default
            string[] targetFrameworks = ProjectFile.GetTargetFrameworks(
                project.GetPropertyValue(ProjectFileNames.TargetFrameworks), null, null);

            return Build(targetFrameworks);
        }

        /// <summary>
        /// Builds the requested target framework(s).
        /// </summary>
        /// <param name="targetFrameworks">The set of target frameworks to build.</param>
        /// <returns>A dictionary of target frameworks to <see cref="AnalyzerResult"/>.</returns>
        public AnalyzerResults Build(string[] targetFrameworks)
        {
            // If the set of target frameworks is empty, just build the default
            if(targetFrameworks == null || targetFrameworks.Length == 0)
            {
                targetFrameworks = new string[] { null };
            }

            string[] targetsToBuild = BuildEnvironment.TargetsToBuild;
            AnalyzerResult result = PrepareForBuild(ref targetsToBuild);
            AnalyzerResults results = new AnalyzerResults();
            foreach (string targetFramework in targetFrameworks)
            {
                results.Add(BuildTargets(targetFramework, targetsToBuild));
            }

            return results;
        }

        /// <summary>
        /// Builds a specific target framework.
        /// </summary>
        /// <param name="targetFramework">The target framework to build.</param>
        /// <returns>The results of the build process.</returns>
        public AnalyzerResult Build(string targetFramework)
        {
            string[] targetsToBuild = BuildEnvironment.TargetsToBuild;
            AnalyzerResult result = PrepareForBuild(ref targetsToBuild);    
            if (targetsToBuild.Length > 0)
            {
                result = BuildTargets(targetFramework, targetsToBuild);
            }
            return result;
        }

        private AnalyzerResult PrepareForBuild(ref string[] targetsToBuild)
        {
            if (targetsToBuild.Length == 0)
            {
                throw new InvalidOperationException("No targets are specified to build.");
            }

            // Reset the cache before every build in case MSBuild cached something from a project reference build
            Manager.BuildManager.ResetCaches();

            // Run the Restore target before any other targets in a seperate submission
            if (string.Compare(targetsToBuild[0], "Restore", StringComparison.OrdinalIgnoreCase) == 0)
            {
                targetsToBuild = targetsToBuild.Skip(1).ToArray();
                return BuildTargets(null, new[] { "Restore" });
            }

            return null;
        }

        private AnalyzerResult BuildTargets(string targetFramework, string[] targetsToBuild)
        {
            // Get a fresh project, otherwise the MSBuild cache will mess up builds
            // See https://github.com/Microsoft/msbuild/issues/3469
            Project project = Load(targetFramework);

            using (new TemporaryEnvironment(GetEffectiveEnvironmentVariables()))
            {
                //ProjectInstance projectInstance = Manager.BuildManager.GetProjectInstanceForBuild(project);
                ProjectInstance projectInstance = project.CreateProjectInstance();

                BuildResult buildResult = Manager.BuildManager.Build(
                    new BuildParameters(project.ProjectCollection)
                    {
                        Loggers = Loggers,
                        ProjectLoadSettings = ProjectLoadSettings.RecordEvaluatedItemElements
                    },
                    new BuildRequestData(projectInstance, targetsToBuild));

                return new AnalyzerResult(this, projectInstance, buildResult);
            }
        }

        public void SetGlobalProperty(string key, string value)
        {
            _globalProperties[key] = value;
        }

        public void RemoveGlobalProperty(string key)
        {
            // Nulls are removed before passing to MSBuild and can be used to ignore values in lower-precedence collections
            _globalProperties[key] = null;
        }

        public void SetEnvironmentVariable(string key, string value)
        {
            _environmentVariables[key] = value;
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