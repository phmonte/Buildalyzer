using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;

namespace Buildalyzer.Logging
{
    internal class EventProcessor : IDisposable
    {
        private readonly ConcurrentDictionary<string, AnalyzerResult> _results = new ConcurrentDictionary<string, AnalyzerResult>();
        private readonly ConcurrentDictionary<string, string> _targets = new ConcurrentDictionary<string, string>();
        private readonly Stack<AnalyzerResult> _currentResult = new Stack<AnalyzerResult>();
        private readonly ConcurrentDictionary<string, Stack<TargetStartedEventArgs>> _targetStacks = new ConcurrentDictionary<string, Stack<TargetStartedEventArgs>>();
        private readonly AnalyzerManager _manager;
        private readonly ProjectAnalyzer _analyzer;
        private readonly ILogger<EventProcessor> _logger;
        private readonly IEnumerable<Microsoft.Build.Framework.ILogger> _buildLoggers;
        private readonly IEventSource _eventSource;
        private readonly bool _analyze;

        private readonly HashSet<string> _projectFilePaths;

        public bool OverallSuccess { get; private set; }

        public IEnumerable<AnalyzerResult> Results => _results.Values;

        public EventProcessor(AnalyzerManager manager, ProjectAnalyzer analyzer, IEnumerable<Microsoft.Build.Framework.ILogger> buildLoggers, IEventSource eventSource, bool analyze)
        {
            _manager = manager;
            _analyzer = analyzer;
            _logger = manager.LoggerFactory?.CreateLogger<EventProcessor>();
            _buildLoggers = buildLoggers ?? Array.Empty<Microsoft.Build.Framework.ILogger>();
            _eventSource = eventSource;
            _analyze = analyze;

            _projectFilePaths = new HashSet<string>();

            if (!string.IsNullOrWhiteSpace(analyzer?.ProjectFile.Path))
            {
                 _projectFilePaths.Add(analyzer?.ProjectFile.Path);
            }

            // Initialize the loggers
            foreach (Microsoft.Build.Framework.ILogger buildLogger in _buildLoggers)
            {
                buildLogger.Initialize(eventSource);
            }

            // Send events to the tree constructor
            if (analyze)
            {
                eventSource.ProjectStarted += ProjectStarted;
                eventSource.ProjectFinished += ProjectFinished;
                eventSource.TargetStarted += TargetStarted;
                eventSource.TargetFinished += TargetFinished;
                eventSource.MessageRaised += MessageRaised;
                eventSource.BuildFinished += BuildFinished;
                if (_logger != null)
                {
                    eventSource.ErrorRaised += ErrorRaised;
                }
            }
        }

        private void ProjectStarted(object sender, ProjectStartedEventArgs e)
        {
            string normalizedProjectFilePath = AnalyzerManager.NormalizePath(e.ProjectFile);

            // If we're not using an analyzer (I.e., from a binary log) and this is the first project file path we've seen, then it's the primary
            if (_projectFilePaths.Count == 0)
            {
                // check if hte project is targeting a solution
                if (normalizedProjectFilePath.EndsWith(".sln"))
                {
                    foreach (dynamic item in e.Items)
                    {
                        string key = item.Key ?? string.Empty;
                        if (key.Equals("ProjectReference"))
                        {
                            string projectName = item.Value?.ItemSpec;
                            if (!string.IsNullOrWhiteSpace(projectName))
                            {
                                _projectFilePaths.Add(AnalyzerManager.NormalizePath(projectName));
                            }
                        }
                    }
                    _currentResult.Push(null);
                }
                _projectFilePaths.Add(normalizedProjectFilePath);
            }

            // Make sure this is the same project, nested MSBuild tasks may have spawned additional builds of other projects
            if (_projectFilePaths.Contains(normalizedProjectFilePath))
            {
                // Get the TFM for this project
                string tfm = GetTargetFramework(e);
                if (!string.IsNullOrWhiteSpace(tfm))
                {
                    string key = $"{normalizedProjectFilePath}:{tfm}";
                    if (!_results.TryGetValue(key, out AnalyzerResult result))
                    {
                        result = new AnalyzerResult(normalizedProjectFilePath, _manager, _analyzer);
                        _results[key] = result;
                    }
                    result.ProcessProject(e);
                    _currentResult.Push(result);
                    return;
                }
            }

            // Push a null result so the stack is balanced on project finish
            _currentResult.Push(null);
        }

        private string GetTargetFramework(ProjectStartedEventArgs e)
        {
            string normalizedProjectFilePath = AnalyzerManager.NormalizePath(e.ProjectFile);
            string tfm = null;
            if (!_targets.TryGetValue(normalizedProjectFilePath, out tfm))
            {
                tfm = e.Properties?.Cast<DictionaryEntry>()
                    .FirstOrDefault(x =>
                        string.Equals(x.Key.ToString(), "TargetFrameworkMoniker", StringComparison.OrdinalIgnoreCase))
                    .Value as string;
                if (!string.IsNullOrWhiteSpace(tfm))
                {
                    tfm = tfm.ToLowerInvariant();
                    _targets.TryAdd(normalizedProjectFilePath, tfm);
                }
            }

            return tfm;
        }

        private void ProjectFinished(object sender, ProjectFinishedEventArgs e)
        {
            AnalyzerResult result = _currentResult.Count > 0 ? _currentResult.Pop() : null;
            if (result != null)
            {
                result.Succeeded = e.Succeeded;
            }
        }

        private void TargetStarted(object sender, TargetStartedEventArgs e)
        {
            Stack<TargetStartedEventArgs> targetStack = GetTargetStack(e.ProjectFile);
            targetStack.Push(e);
        }

        private void TargetFinished(object sender, TargetFinishedEventArgs e)
        {
            if (e.ProjectFile.EndsWith(".sln") && e.TargetName == "Build")
            {
                Console.WriteLine("Aa");
                foreach (ITaskItem targetOutput in e.TargetOutputs)
                {
                    string normalizedProjectFilePath = AnalyzerManager.NormalizePath(targetOutput.GetMetadata("OriginalItemSpec"));
                    string targetFrameworkIdentifier = targetOutput.GetMetadata("TargetFrameworkIdentifier");
                    string targetFrameworkVersion = targetOutput.GetMetadata("TargetFrameworkVersion");

                    string tfm = $"{targetFrameworkIdentifier},Version=v{targetFrameworkVersion}".ToLowerInvariant();

                    _targets.AddOrUpdate(
                        normalizedProjectFilePath,
                        key => tfm,
                        (key, oldValue) => tfm);
                }
            }

            Stack<TargetStartedEventArgs> targetStack = GetTargetStack(e.ProjectFile);
            TargetStartedEventArgs top = targetStack.Pop();
            if (top.TargetName != e.TargetName)
            {
                // Sanity check
                throw new InvalidOperationException("Mismatched target events");
            }
        }

        private void MessageRaised(object sender, BuildMessageEventArgs e)
        {
            // Process the command line arguments for the Csc task
            AnalyzerResult result = _currentResult.Count == 0 ? null : _currentResult.Peek();
            if (result != null
                && e is TaskCommandLineEventArgs cmd
                && string.Equals(cmd.TaskName, "Csc", StringComparison.OrdinalIgnoreCase))
            {
                Stack<TargetStartedEventArgs> targetStack = GetTargetStack(e.ProjectFile);
                result.ProcessCscCommandLine(cmd.CommandLine, targetStack.Any(x => x.TargetName == "CoreCompile"));
            }
        }

        private void BuildFinished(object sender, BuildFinishedEventArgs e)
        {
            OverallSuccess = e.Succeeded;
        }

        private Stack<TargetStartedEventArgs> GetTargetStack(string projectFile)
        {
            return _targetStacks.GetOrAdd(projectFile, _ => new Stack<TargetStartedEventArgs>());
        }

        private void ErrorRaised(object sender, BuildErrorEventArgs e) => _logger.LogError(e.Message);

        public void Dispose()
        {
            if (_analyze)
            {
                _eventSource.ProjectStarted -= ProjectStarted;
                _eventSource.ProjectFinished -= ProjectFinished;
                _eventSource.TargetStarted -= TargetStarted;
                _eventSource.TargetFinished -= TargetFinished;
                _eventSource.MessageRaised -= MessageRaised;
                _eventSource.BuildFinished -= BuildFinished;
                if (_logger != null)
                {
                    _eventSource.ErrorRaised -= ErrorRaised;
                }
            }

            // Need to release the loggers in case they get used again (I.e., Restore followed by Clean;Build)
            foreach (Microsoft.Build.Framework.ILogger buildLogger in _buildLoggers)
            {
                buildLogger.Shutdown();
            }
        }
    }
}
