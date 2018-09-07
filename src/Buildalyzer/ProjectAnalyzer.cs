using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Buildalyzer.Construction;
using Buildalyzer.Environment;
using Buildalyzer.Logging;
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

        private readonly ProcessRunner _processRunner;
        
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
            _processRunner = new ProcessRunner(manager.ProjectLogger, null);
            
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
                string[] targetsToBuild = buildEnvironment.TargetsToBuild;
                Restore(buildEnvironment, ref targetsToBuild);
                results.Add(BuildTargets(buildEnvironment, targetFramework, targetsToBuild, true));
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
            string[] targetsToBuild = buildEnvironment.TargetsToBuild;
            Restore(buildEnvironment, ref targetsToBuild);
            foreach (string targetFramework in targetFrameworks)
            {
                results.Add(BuildTargets(buildEnvironment, targetFramework, targetsToBuild, true));
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
        public AnalyzerResults Build(string targetFramework, EnvironmentOptions environmentOptions)
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
        public AnalyzerResults Build(string targetFramework, BuildEnvironment buildEnvironment)
        {
            if (buildEnvironment == null)
            {
                throw new ArgumentNullException(nameof(buildEnvironment));
            }

            string[] targetsToBuild = buildEnvironment.TargetsToBuild;
            Restore(buildEnvironment, ref targetsToBuild);
            return new AnalyzerResults(BuildTargets(buildEnvironment, targetFramework, targetsToBuild, true));
        }

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

        private void Restore(BuildEnvironment buildEnvironment, ref string[] targetsToBuild)
        {
            // Run the Restore target before any other targets in a seperate submission
            if (targetsToBuild != null && targetsToBuild.Length > 0 && targetsToBuild[0].Equals("Restore", StringComparison.OrdinalIgnoreCase))
            {
                targetsToBuild = targetsToBuild.Skip(1).ToArray();
                BuildTargets(buildEnvironment, null, new[] { "Restore" }, false);                
            }
        }

        // This is where the magic happens - returns one result per result target framework
        private IEnumerable<AnalyzerResult> BuildTargets(BuildEnvironment buildEnvironment, string targetFramework, string[] targetsToBuild, bool analyzeResult)
        {
            string logFile = analyzeResult ? Path.ChangeExtension(Path.GetTempFileName(), ".binlog") : null;
            try
            {
                using (AnonymousPipeServerStream pipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable))
                {
                    // Get the filename
                    string fileName = buildEnvironment.MsBuildExePath;
                    string initialArguments = string.Empty;
                    if (Path.GetExtension(buildEnvironment.MsBuildExePath).Equals(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        // .NET Core MSBuild .dll needs to be run with dotnet
                        fileName = "dotnet";
                        initialArguments = $"\"{buildEnvironment.MsBuildExePath}\"";
                    }

                    // Get the arguments to use
                    //string loggerArgument = logFile == null ? string.Empty : $"/bl:{FormatArgument(logFile)};ProjectImports=None";
                    string loggerPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Buildalyzer.BuildLogger.dll");
                    string loggerArgument = $"/l:BuildLogger,{FormatArgument(loggerPath)};{pipe.GetClientHandleAsString()}";
                    Dictionary<string, string> effectiveGlobalProperties = GetEffectiveGlobalProperties(buildEnvironment);
                    if (!string.IsNullOrEmpty(targetFramework))
                    {
                        // Setting the TargetFramework MSBuild property tells MSBuild which target framework to use for the outer build
                        effectiveGlobalProperties[MsBuildProperties.TargetFramework] = targetFramework;
                    }
                    string propertyArgument = effectiveGlobalProperties.Count == 0 ? string.Empty : $"/property:{(string.Join(";", effectiveGlobalProperties.Select(x => $"{x.Key}={FormatArgument(x.Value)}")))}";
                    string targetArgument = targetsToBuild == null || targetsToBuild.Length == 0 ? string.Empty : $"/target:{string.Join(";", targetsToBuild)}";
                    string arguments = $"{initialArguments} /nodeReuse:False {targetArgument} {propertyArgument} {loggerArgument} {FormatArgument(ProjectFile.Path)}";

                    // Read and queue pipe messages
                    ConcurrentQueue<string> pipeQueue = new ConcurrentQueue<string>();
                    StreamReader reader = new StreamReader(pipe);
                    Thread inputThread = new Thread(() =>
                    {
                        string message;
                        while((message = reader.ReadLine()) != null)
                        {
                            pipeQueue.Enqueue(message);
                        }
                    })
                    {
                        IsBackground = true
                    };
                    inputThread.Start();

                    // Run MsBuild
                    try
                    {
                        if (_processRunner.Run(fileName, arguments, Path.GetDirectoryName(ProjectFile.Path), GetEffectiveEnvironmentVariables(buildEnvironment), () => ProcessPipeMessages(pipeQueue)) != 0)
                        {
                            // Failure
                            return Array.Empty<AnalyzerResult>();
                        }
                    }
                    finally
                    {
                        inputThread.Abort();
                        reader.Dispose();
                    }
                }

                // Success
                if (analyzeResult)
                {
                    //BinaryLogReader logReader = new BinaryLogReader();
                    //logReader.Read(logFile);
                    //return logReader.Results;
                }
                return Array.Empty<AnalyzerResult>();
            }
            finally
            {
                if (logFile != null)
                {
                    try
                    {
                        File.Delete(logFile);
                    }
                    catch { }
                }
            }
        }

        private void ProcessPipeMessages(ConcurrentQueue<string> pipeQueue)
        {
            string message;
            while(pipeQueue.TryDequeue(out message))
            {
                Manager.ProjectLogger.LogInformation($"PIPE! {message}");
            }
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

        // TODO: Remove and rewrite binary logger (does it need to be deleted on every build?)

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