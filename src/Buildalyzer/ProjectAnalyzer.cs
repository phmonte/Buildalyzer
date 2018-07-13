using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
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

        public EnvironmentFactory EnvironmentFactory { get; }

        /// <summary>
        /// The global properties for MSBuild to be used for every build from this analyzer.
        /// </summary>
        /// <remarks>
        /// Additional global properties may be added or changed by individual build environment.
        /// </remarks>
        public IReadOnlyDictionary<string, string> GlobalProperties => GetEffectiveGlobalProperties(null);

        /// <summary>
        /// The environment variables for MSBuild to be used for every build from this analyzer.
        /// </summary>
        /// <remarks>
        /// Additional environment variables may be added or changed by individual build environment.
        /// </remarks>
        public IReadOnlyDictionary<string, string> EnvironmentVariables => GetEffectiveEnvironmentVariables(null);
        
        public IEnumerable<ILogger> Loggers => _loggers;

        /// <summary>
        /// Controls whether empty, invalid, and missing targets should be ignored during project load.
        /// </summary>
        public bool IgnoreFaultyImports { get; set; } = true;

        internal ProjectAnalyzer(AnalyzerManager manager, string projectFilePath, XDocument projectDocument)
        {
            // Preload/enforce referencing some required asemblies
            Copy copy = new Copy();

            Manager = manager;
            ProjectFile = new ProjectFile(projectFilePath, projectDocument, manager.ProjectTransformer);
            EnvironmentFactory = new EnvironmentFactory(Manager, ProjectFile);

            // Set the solution directory global property
            string solutionDir = manager.SolutionDirectory ?? Path.GetDirectoryName(projectFilePath);
            SetGlobalProperty(MsBuildProperties.SolutionDir, solutionDir);

            // Create the logger
            if(manager.ProjectLogger != null)
            {
                AddLogger(new ConsoleLogger(manager.LoggerVerbosity, x => manager.ProjectLogger.LogInformation(x), null, null));
            }
        }
        
        public Project Load() =>
            Load(null, EnvironmentFactory.GetBuildEnvironment());

        public Project Load(EnvironmentOptions environmentOptions)
        {
            if (environmentOptions == null)
            {
                throw new ArgumentNullException(nameof(environmentOptions));
            }

            return Load(null, EnvironmentFactory.GetBuildEnvironment(environmentOptions));
        }

        public Project Load(BuildEnvironment buildEnvironment) =>
            Load(null, buildEnvironment);

        public Project Load(string targetFramwork) =>
            Load(targetFramwork, EnvironmentFactory.GetBuildEnvironment(targetFramwork));

        public Project Load(string targetFramework, EnvironmentOptions environmentOptions)
        {
            if (environmentOptions == null)
            {
                throw new ArgumentNullException(nameof(environmentOptions));
            }

            return Load(targetFramework, EnvironmentFactory.GetBuildEnvironment(targetFramework, environmentOptions));
        }

        public Project Load(string targetFramework, BuildEnvironment buildEnvironment)
        {
            if (buildEnvironment == null)
            {
                throw new ArgumentNullException(nameof(buildEnvironment));
            }

            // Need a fresh BuildEnvironmentHelper for every load/build
            ResetBuildEnvironmentHelper(buildEnvironment);

            // Some project types can't be built from .NET Core
            if (BuildEnvironment.IsRunningOnCore)
            {
                // Portable projects
                if (ProjectFile.RequiresNetFramework)
                {
                    throw new Exception("This project requires the .NET Framework and can't be built from a .NET Core host");
                }

                // Legacy framework projects with PackageReference
                if (!ProjectFile.UsesSdk && ProjectFile.ContainsPackageReferences)
                {
                    throw new Exception("Can't build legacy projects that contain PackageReference from a .NET Core host");
                }
            }

            // Create a project collection for each project since the toolset might change depending on the type of project
            Dictionary<string, string> effectiveGlobalProperties = GetEffectiveGlobalProperties(buildEnvironment);
            if(!string.IsNullOrEmpty(targetFramework))
            {
                // Setting the TargetFramework MSBuild property tells MSBuild which target framework to use for the outer build
                effectiveGlobalProperties[MsBuildProperties.TargetFramework] = targetFramework;
            }
            ProjectCollection projectCollection = CreateProjectCollection(buildEnvironment, effectiveGlobalProperties);

            // Load the project
            using (new TemporaryEnvironment(GetEffectiveEnvironmentVariables(buildEnvironment)))
            {
                using (XmlReader projectReader = ProjectFile.CreateReader())
                {
                    ProjectRootElement root = ProjectRootElement.Create(projectReader, projectCollection);

                    // When constructing a project from an XmlReader, MSBuild cannot determine the project file path.  Setting the
                    // path explicitly is necessary so that the reserved properties like $(MSBuildProjectDirectory) will work.
                    root.FullPath = ProjectFile.Path;

                    using (new AssemblyResolver(buildEnvironment))
                    {
                        return new Project(
                            root,
                            effectiveGlobalProperties,
                            ToolLocationHelper.CurrentToolsVersion,
                            projectCollection,
                            IgnoreFaultyImports
                                ? ProjectLoadSettings.IgnoreEmptyImports | ProjectLoadSettings.IgnoreInvalidImports | ProjectLoadSettings.IgnoreMissingImports
                                : ProjectLoadSettings.Default);
                    }
                }
            }
        }

        /// <summary>
        /// Builds all target framework(s).
        /// </summary>
        /// <returns>A dictionary of target frameworks to <see cref="AnalyzerResult"/>.</returns>
        public AnalyzerResults BuildAllTargetFrameworks() => BuildAllTargetFrameworks(new EnvironmentOptions());

        /// <summary>
        /// Builds all target framework(s) with the specified build environment options.
        /// </summary>
        /// <param name="environmentOptions">The environment options to use for the build.</param>
        /// <returns>A dictionary of target frameworks to <see cref="AnalyzerResult"/>.</returns>
        public AnalyzerResults BuildAllTargetFrameworks(EnvironmentOptions environmentOptions)
        {
            if (environmentOptions == null)
            {
                throw new ArgumentNullException(nameof(environmentOptions));
            }

            // Load the project with the default build environment to get the evaluated target frameworks
            Project project = Load(EnvironmentFactory.GetBuildEnvironment(environmentOptions));

            // Get all evaluated target frameworks from the Project and build them
            // but don't worry about getting a single target framework, it'll build the default
            string[] targetFrameworks = ProjectFile.GetTargetFrameworks(
                new[] { project.GetPropertyValue(ProjectFileNames.TargetFrameworks) }, null, null);

            return Build(targetFrameworks, environmentOptions);
        }
        
        /// <summary>
        /// Builds all target framework(s) with the specified build environment.
        /// </summary>
        /// <param name="buildEnvironment">The build environment to use for the build.</param>
        /// <returns>A dictionary of target frameworks to <see cref="AnalyzerResult"/>.</returns>
        public AnalyzerResults BuildAllTargetFrameworks(BuildEnvironment buildEnvironment)
        {
            if (buildEnvironment == null)
            {
                throw new ArgumentNullException(nameof(buildEnvironment));
            }

            // Load the project with the specified to get the evaluated target frameworks
            Project project = Load(buildEnvironment);

            // Get all evaluated target frameworks from the Project and build them
            // but don't worry about getting a single target framework, it'll build the default
            string[] targetFrameworks = ProjectFile.GetTargetFrameworks(
                new[] { project.GetPropertyValue(ProjectFileNames.TargetFrameworks) }, null, null);

            return Build(targetFrameworks, buildEnvironment);
        }

        /// <summary>
        /// Builds the requested target framework(s).
        /// </summary>
        /// <param name="targetFrameworks">The set of target frameworks to build.</param>
        /// <returns>A dictionary of target frameworks to <see cref="AnalyzerResult"/>.</returns>
        public AnalyzerResults Build(string[] targetFrameworks) =>
            Build(targetFrameworks, new EnvironmentOptions());

        /// <summary>
        /// Builds the requested target framework(s).
        /// </summary>
        /// <param name="targetFrameworks">The set of target frameworks to build.</param>
        /// <param name="environmentOptions">The environment options to use for the build.</param>
        /// <returns>A dictionary of target frameworks to <see cref="AnalyzerResult"/>.</returns>
        public AnalyzerResults Build(string[] targetFrameworks, EnvironmentOptions environmentOptions)
        {
            if (environmentOptions == null)
            {
                throw new ArgumentNullException(nameof(environmentOptions));
            }

            // If the set of target frameworks is empty, just build the default
            if (targetFrameworks == null || targetFrameworks.Length == 0)
            {
                targetFrameworks = new string[] { null };
            }

            // Reset the cache before every build in case MSBuild cached something from a project reference build
            Manager.BuildManager.ResetCaches();

            // Create a new build envionment for each target
            AnalyzerResults results = new AnalyzerResults();
            foreach (string targetFramework in targetFrameworks)
            {
                BuildEnvironment buildEnvironment = EnvironmentFactory.GetBuildEnvironment(targetFramework, environmentOptions);
                string[] targetsToBuild = buildEnvironment.TargetsToBuild;
                Restore(buildEnvironment, ref targetsToBuild);
                results.Add(BuildTargets(buildEnvironment, targetFramework, targetsToBuild));
            }

            return results;
        }

        /// <summary>
        /// Builds the requested target framework(s).
        /// </summary>
        /// <param name="targetFrameworks">The set of target frameworks to build.</param>
        /// <param name="buildEnvironment">The build environment to use for the build.</param>
        /// <returns>A dictionary of target frameworks to <see cref="AnalyzerResult"/>.</returns>
        public AnalyzerResults Build(string[] targetFrameworks, BuildEnvironment buildEnvironment)
        {
            if (buildEnvironment == null)
            {
                throw new ArgumentNullException(nameof(buildEnvironment));
            }

            // If the set of target frameworks is empty, just build the default
            if (targetFrameworks == null || targetFrameworks.Length == 0)
            {
                targetFrameworks = new string[] { null };
            }

            // Reset the cache before every build in case MSBuild cached something from a project reference build
            Manager.BuildManager.ResetCaches();

            string[] targetsToBuild = buildEnvironment.TargetsToBuild;
            Restore(buildEnvironment, ref targetsToBuild);
            AnalyzerResults results = new AnalyzerResults();
            foreach (string targetFramework in targetFrameworks)
            {
                results.Add(BuildTargets(buildEnvironment, targetFramework, targetsToBuild));
            }

            return results;
        }

        /// <summary>
        /// Builds a specific target framework.
        /// </summary>
        /// <param name="targetFramework">The target framework to build.</param>
        /// <returns>The result of the build process.</returns>
        public AnalyzerResult Build(string targetFramework) =>
            Build(targetFramework, EnvironmentFactory.GetBuildEnvironment(targetFramework));

        /// <summary>
        /// Builds a specific target framework.
        /// </summary>
        /// <param name="targetFramework">The target framework to build.</param>
        /// <param name="environmentOptions">The environment options to use for the build.</param>
        /// <returns>The result of the build process.</returns>
        public AnalyzerResult Build(string targetFramework, EnvironmentOptions environmentOptions)
        {
            if (environmentOptions == null)
            {
                throw new ArgumentNullException(nameof(environmentOptions));
            }

            return Build(targetFramework, EnvironmentFactory.GetBuildEnvironment(targetFramework, environmentOptions));
        }

        /// <summary>
        /// Builds a specific target framework.
        /// </summary>
        /// <param name="targetFramework">The target framework to build.</param>
        /// <param name="buildEnvironment">The build environment to use for the build.</param>
        /// <returns>The result of the build process.</returns>
        public AnalyzerResult Build(string targetFramework, BuildEnvironment buildEnvironment)
        {
            if (buildEnvironment == null)
            {
                throw new ArgumentNullException(nameof(buildEnvironment));
            }

            // Reset the cache before every build in case MSBuild cached something from a project reference build
            Manager.BuildManager.ResetCaches();

            string[] targetsToBuild = buildEnvironment.TargetsToBuild;
            AnalyzerResult result = Restore(buildEnvironment, ref targetsToBuild);    
            if (targetsToBuild.Length > 0)
            {
                result = BuildTargets(buildEnvironment, targetFramework, targetsToBuild);
            }
            return result;
        }

        /// <summary>
        /// Builds the project without specifying a target framework. This may have undesirable behavior if the project is multi-targeted.
        /// </summary>
        /// <returns>The result of the build process.</returns>
        public AnalyzerResult Build() => Build((string)null);

        /// <summary>
        /// Builds the project without specifying a target framework. This may have undesirable behavior if the project is multi-targeted.
        /// </summary>
        /// <param name="environmentOptions">The environment options to use for the build.</param>
        /// <returns>The result of the build process.</returns>
        public AnalyzerResult Build(EnvironmentOptions environmentOptions) => Build((string)null, environmentOptions);

        /// <summary>
        /// Builds the project without specifying a target framework. This may have undesirable behavior if the project is multi-targeted.
        /// </summary>
        /// <param name="buildEnvironment">The build environment to use for the build.</param>
        /// <returns>The result of the build process.</returns>
        public AnalyzerResult Build(BuildEnvironment buildEnvironment) => Build((string)null, buildEnvironment);

        private AnalyzerResult Restore(BuildEnvironment buildEnvironment, ref string[] targetsToBuild)
        {
            if (targetsToBuild.Length == 0)
            {
                throw new InvalidOperationException("No targets are specified to build.");
            }
            
            // Run the Restore target before any other targets in a seperate submission
            if (string.Compare(targetsToBuild[0], "Restore", StringComparison.OrdinalIgnoreCase) == 0)
            {
                targetsToBuild = targetsToBuild.Skip(1).ToArray();
                return BuildTargets(buildEnvironment, null, new[] { "Restore" });
            }

            return null;
        }

        private AnalyzerResult BuildTargets(BuildEnvironment buildEnvironment, string targetFramework, string[] targetsToBuild)
        {
            // Get a fresh project, otherwise the MSBuild cache will mess up builds
            // See https://github.com/Microsoft/msbuild/issues/3469
            Project project = Load(targetFramework, buildEnvironment);

            using (new TemporaryEnvironment(GetEffectiveEnvironmentVariables(buildEnvironment)))
            {
                //ProjectInstance projectInstance = Manager.BuildManager.GetProjectInstanceForBuild(project);
                ProjectInstance projectInstance = project.CreateProjectInstance();

                using (new AssemblyResolver(buildEnvironment))
                {
                    BuildResult buildResult = Manager.BuildManager.Build(
                        new BuildParameters(project.ProjectCollection)
                        {
                            Loggers = Loggers,
                            ProjectLoadSettings = ProjectLoadSettings.RecordEvaluatedItemElements
                        },
                        new BuildRequestData(projectInstance, targetsToBuild));
                    return new AnalyzerResult(this, project, projectInstance, buildResult, buildEnvironment);
                }
            }
        }

        /// <summary>
        /// A glorious hack to work around the fact that BuildEnvironmentHelper is a static singleton
        /// that won't change for different build environments in the same process
        /// (see https://github.com/Microsoft/msbuild/blob/ffa7f408e8d00bda677cfb0ec15d547acf2aea06/src/Shared/BuildEnvironmentHelper.cs#L410-L426).
        /// It also won't calculate BuildEnvironmentHelper.MSBuildToolsDirectory32 correctly for API-based
        /// builds uding defaults, which screws up things like SDK resolver resolution
        /// (see https://github.com/Microsoft/msbuild/blob/ffa7f408e8d00bda677cfb0ec15d547acf2aea06/src/Build/BackEnd/Components/SdkResolution/SdkResolverLoader.cs#L29-L30).
        /// </summary>
        private void ResetBuildEnvironmentHelper(BuildEnvironment buildEnvironment)
        {
            Type type = Assembly.GetAssembly(typeof(Project)).GetType("Microsoft.Build.Shared.BuildEnvironmentHelper");
            MethodInfo resetMethod = type.GetMethod("ResetInstance_ForUnitTestsOnly", BindingFlags.Static | BindingFlags.NonPublic);
            resetMethod.Invoke(null, new object[] { (Func<string>)(() => buildEnvironment.MsBuildExePath), null, null, null, null, null });
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
        private Dictionary<string, string> GetEffectiveGlobalProperties(BuildEnvironment buildEnvironment)
            => GetEffectiveDictionary(
                true,  // Remove nulls to avoid passing null global properties. But null can be used in higher-precident dictionaries to ignore a lower-precident dictionary's value.
               buildEnvironment?.GlobalProperties,
                Manager.GlobalProperties,
                _globalProperties);

        // Note the order of precedence (from least to most)
        private Dictionary<string, string> GetEffectiveEnvironmentVariables(BuildEnvironment buildEnvironment)
            => GetEffectiveDictionary(
                false, // Don't remove nulls as a null value will unset the env var which may be set by a calling process.
                buildEnvironment?.EnvironmentVariables,
                Manager.EnvironmentVariables,
                _environmentVariables);

        private static Dictionary<string, string> GetEffectiveDictionary(
            bool removeNulls,
            params IReadOnlyDictionary<string, string>[] innerDictionaries)
        {
            Dictionary<string, string> effectiveDictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (IReadOnlyDictionary<string, string> innerDictionary in innerDictionaries.Where(x => x != null))
            {
                foreach (KeyValuePair<string, string> pair in innerDictionary)
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

        private ProjectCollection CreateProjectCollection(BuildEnvironment buildEnvironment, IDictionary<string, string> globalProperties)
        {
            ProjectCollection projectCollection = new ProjectCollection(globalProperties);
            projectCollection.RemoveAllToolsets();  // Make sure we're only using the latest tools
            projectCollection.AddToolset(new Toolset(ToolLocationHelper.CurrentToolsVersion, buildEnvironment.ToolsPath, projectCollection, string.Empty));
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