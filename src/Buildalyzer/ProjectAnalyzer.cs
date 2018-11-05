using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Buildalyzer.Construction;
using Buildalyzer.Environment;
using Buildalyzer.Logger;
using Buildalyzer.Logging;
using Microsoft.Build.Construction;
using Microsoft.Build.Logging;
using Microsoft.Extensions.Logging;
using MsBuildPipeLogger;
using ILogger = Microsoft.Build.Framework.ILogger;

namespace Buildalyzer
{
    public class ProjectAnalyzer
    {
        private readonly List<ILogger> _buildLoggers = new List<ILogger>();
        
        // Project-specific global properties and environment variables
        private readonly ConcurrentDictionary<string, string> _globalProperties = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, string> _environmentVariables = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public AnalyzerManager Manager { get; }

        public ProjectFile ProjectFile { get; }

        public EnvironmentFactory EnvironmentFactory { get; }

        public string SolutionDirectory { get; }

        public ProjectInSolution ProjectInSolution { get; }

        /// <summary>
        /// Gets a GUID for the project. This checks for a GUID from the
        /// solution (if originally provided). If this isn't available, it
        /// will generate a UUID GUID by hashing the project path relative to the solution path (so it's repeatable).
        public Guid ProjectGuid { get; }

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
        
        public IEnumerable<ILogger> BuildLoggers => _buildLoggers;

        public ILogger<ProjectAnalyzer> Logger { get; set; }

        /// <summary>
        /// Controls whether empty, invalid, and missing targets should be ignored during project load.
        /// </summary>
        public bool IgnoreFaultyImports { get; set; } = true;

        // The project file path should already be normalized
        internal ProjectAnalyzer(AnalyzerManager manager, string projectFilePath, ProjectInSolution projectInSolution)
        {            
            Manager = manager;
            Logger = Manager.LoggerFactory?.CreateLogger<ProjectAnalyzer>();
            ProjectFile = new ProjectFile(projectFilePath, manager.ProjectTransformer);
            EnvironmentFactory = new EnvironmentFactory(Manager, ProjectFile);
            ProjectInSolution = projectInSolution;
            SolutionDirectory = string.IsNullOrEmpty(manager.SolutionFilePath)
                ? Path.GetDirectoryName(projectFilePath) : Path.GetDirectoryName(manager.SolutionFilePath);

            // Get (or create) a project GUID
            ProjectGuid = projectInSolution == null
                ? GuidUtility.Create(GuidUtility.UrlNamespace, ProjectFile.Path.Substring(SolutionDirectory.Length))
                : Guid.Parse(projectInSolution.ProjectGuid);

            // Set the solution directory global property
            SetGlobalProperty(MsBuildProperties.SolutionDir, SolutionDirectory);
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

            // Create a new build envionment for each target
            AnalyzerResults results = new AnalyzerResults();
            foreach (string targetFramework in targetFrameworks)
            {
                BuildEnvironment buildEnvironment = EnvironmentFactory.GetBuildEnvironment(targetFramework, environmentOptions);
                BuildTargets(buildEnvironment, targetFramework, buildEnvironment.TargetsToBuild, results);
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
            
            AnalyzerResults results = new AnalyzerResults();
            foreach (string targetFramework in targetFrameworks)
            {
                BuildTargets(buildEnvironment, targetFramework, buildEnvironment.TargetsToBuild, results);
            }

            return results;
        }

        /// <summary>
        /// Builds a specific target framework.
        /// </summary>
        /// <param name="targetFramework">The target framework to build.</param>
        /// <returns>The result of the build process.</returns>
        public AnalyzerResults Build(string targetFramework) =>
            Build(targetFramework, EnvironmentFactory.GetBuildEnvironment(targetFramework));

        /// <summary>
        /// Builds a specific target framework.
        /// </summary>
        /// <param name="targetFramework">The target framework to build.</param>
        /// <param name="environmentOptions">The environment options to use for the build.</param>
        /// <returns>The result of the build process.</returns>
        public AnalyzerResults Build(string targetFramework, EnvironmentOptions environmentOptions) => 
            Build(
                targetFramework,
                EnvironmentFactory.GetBuildEnvironment(
                    targetFramework,
                    environmentOptions ?? throw new ArgumentNullException(nameof(environmentOptions))));

        /// <summary>
        /// Builds a specific target framework.
        /// </summary>
        /// <param name="targetFramework">The target framework to build.</param>
        /// <param name="buildEnvironment">The build environment to use for the build.</param>
        /// <returns>The result of the build process.</returns>
        public AnalyzerResults Build(string targetFramework, BuildEnvironment buildEnvironment) => 
            BuildTargets(
                buildEnvironment ?? throw new ArgumentNullException(nameof(buildEnvironment)),
                targetFramework,
                buildEnvironment.TargetsToBuild,
                new AnalyzerResults());

        /// <summary>
        /// Builds the project without specifying a target framework. In a multi-targeted project this will return a <see cref="AnalyzerResult"/> for each target framework.
        /// </summary>
        /// <returns>The result of the build process.</returns>
        public AnalyzerResults Build() => Build((string)null);

        /// <summary>
        /// Builds the project without specifying a target framework. In a multi-targeted project this will return a <see cref="AnalyzerResult"/> for each target framework.
        /// </summary>
        /// <param name="environmentOptions">The environment options to use for the build.</param>
        /// <returns>The result of the build process.</returns>
        public AnalyzerResults Build(EnvironmentOptions environmentOptions) => Build((string)null, environmentOptions);

        /// <summary>
        /// Builds the project without specifying a target framework. In a multi-targeted project this will return a <see cref="AnalyzerResult"/> for each target framework.
        /// </summary>
        /// <param name="buildEnvironment">The build environment to use for the build.</param>
        /// <returns>The result of the build process.</returns>
        public AnalyzerResults Build(BuildEnvironment buildEnvironment) => Build((string)null, buildEnvironment);
        
        // This is where the magic happens - returns one result per result target framework
        private AnalyzerResults BuildTargets(BuildEnvironment buildEnvironment, string targetFramework, string[] targetsToBuild, AnalyzerResults results)
        {
            using (CancellationTokenSource cancellation = new CancellationTokenSource())
            {
                using (AnonymousPipeLoggerServer pipeLogger = new AnonymousPipeLoggerServer(cancellation.Token))
                {
                    using (EventProcessor eventProcessor = new EventProcessor(Manager, this, BuildLoggers, pipeLogger, results != null))
                    {
                        // Run MSBuild
                        int exitCode;
                        string fileName = GetCommand(buildEnvironment, targetFramework, targetsToBuild, pipeLogger.GetClientHandle(), out string arguments);
                        using (ProcessRunner processRunner = new ProcessRunner(fileName, arguments, Path.GetDirectoryName(ProjectFile.Path), GetEffectiveEnvironmentVariables(buildEnvironment), Manager.LoggerFactory))
                        {
                            processRunner.Exited = () => cancellation.Cancel();
                            processRunner.Start();
                            pipeLogger.ReadAll();
                            processRunner.WaitForExit();
                            exitCode = processRunner.ExitCode;
                        }

                        // Collect the results
                        results?.Add(eventProcessor.Results, exitCode == 0 && eventProcessor.OverallSuccess);
                    }
                }
            }
            return results;
        }

        private string GetCommand(BuildEnvironment buildEnvironment, string targetFramework, string[] targetsToBuild, string pipeLoggerClientHandle, out string arguments)
        {
            // Get the executable and the initial set of arguments
            string fileName = buildEnvironment.MsBuildExePath;
            string initialArguments = string.Empty;
            if (Path.GetExtension(buildEnvironment.MsBuildExePath).Equals(".dll", StringComparison.OrdinalIgnoreCase))
            {
                // .NET Core MSBuild .dll needs to be run with dotnet
                fileName = buildEnvironment.DotnetExePath;
                initialArguments = $"\"{buildEnvironment.MsBuildExePath}\"";
            }

            // Get the logger arguments (/l)
            string loggerPath = typeof(BuildalyzerLogger).Assembly.Location;
            bool logEverything = _buildLoggers.Count > 0;
            string loggerArgument = $"/l:{nameof(BuildalyzerLogger)},{FormatArgument(loggerPath)};{pipeLoggerClientHandle};{logEverything}";

            // Get the properties arguments (/property)
            Dictionary<string, string> effectiveGlobalProperties = GetEffectiveGlobalProperties(buildEnvironment);
            if (!string.IsNullOrEmpty(targetFramework))
            {
                // Setting the TargetFramework MSBuild property tells MSBuild which target framework to use for the outer build
                effectiveGlobalProperties[MsBuildProperties.TargetFramework] = targetFramework;
            }
            string propertyArgument = effectiveGlobalProperties.Count == 0 ? string.Empty : $"/property:{(string.Join(";", effectiveGlobalProperties.Select(x => $"{x.Key}={FormatArgument(x.Value)}")))}";

            // Get the target argument (/target)
            string targetArgument = targetsToBuild == null || targetsToBuild.Length == 0 ? string.Empty : $"/target:{string.Join(";", targetsToBuild)}";

            // Get the restore argument (/restore)
            string restoreArgument = buildEnvironment.Restore ? "/restore" : string.Empty;

            arguments = $"{initialArguments} /noconsolelogger {restoreArgument} {targetArgument} {propertyArgument} {loggerArgument} {FormatArgument(ProjectFile.Path)}";
            return fileName;
        }

        private static string FormatArgument(string argument)
        {
            // Escape inner quotes
            argument = argument.Replace("\"", "\\\"");

            // Also escape trailing slashes so they don't escape the closing quote
            if (argument.EndsWith("\\"))
            {
                argument = $"{argument}\\";
            }

            // Surround with quotes
            return $"\"{argument}\"";
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
                        
        public void AddBinaryLogger(
            string binaryLogFilePath = null,
            BinaryLogger.ProjectImportsCollectionMode collectProjectImports = BinaryLogger.ProjectImportsCollectionMode.Embed) =>
            AddBuildLogger(new BinaryLogger
            {
                Parameters = binaryLogFilePath ?? Path.ChangeExtension(ProjectFile.Path, "binlog"),
                CollectProjectImports = BinaryLogger.ProjectImportsCollectionMode.Embed,
                Verbosity = Microsoft.Build.Framework.LoggerVerbosity.Diagnostic
            });

        /// <summary>
        /// Adds an MSBuild logger to the build. Note that this may have a large penalty on build performance.
        /// </summary>
        /// <remarks>
        /// Normally, the minimum required amount of log events are forwarded from the MSBuild process to Buildalyzer.
        /// By attaching arbitrary loggers, MSBuild must forward every log event so the logger has a chance to handle it.
        /// </remarks>
        /// <param name="logger">The logger to add.</param>
        public void AddBuildLogger(ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            _buildLoggers.Add(logger);
        }

        /// <summary>
        /// Removes an MSBuild logger from the build.
        /// </summary>
        /// <param name="logger">The logger to remove.</param>
        public void RemoveBuildLogger(ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            _buildLoggers.Remove(logger);
        }
    }    
}