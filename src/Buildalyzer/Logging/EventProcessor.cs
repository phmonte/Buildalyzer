using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Buildalyzer.Logging
{
    internal class EventProcessor : IDisposable
    {
        private readonly Dictionary<string, AnalyzerResult> _results = new Dictionary<string, AnalyzerResult>();
        private readonly Stack<AnalyzerResult> _currentResult = new Stack<AnalyzerResult>();
        private readonly Stack<TargetStartedEventArgs> _targetStack = new Stack<TargetStartedEventArgs>();
        private readonly AnalyzerManager _manager;
        private readonly ProjectAnalyzer _analyzer;
        private readonly ILogger<EventProcessor> _logger;
        private readonly IEnumerable<Microsoft.Build.Framework.ILogger> _buildLoggers;
        private readonly IEventSource _eventSource;
        private readonly bool _analyze;

        private string _projectFilePath;

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

            _projectFilePath = _analyzer?.ProjectFile.Path;

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
            // If we're not using an analyzer (I.e., from a binary log) and this is the first project file path we've seen, then it's the primary
            if (_projectFilePath == null)
            {
                _projectFilePath = AnalyzerManager.NormalizePath(e.ProjectFile);
            }

            // Make sure this is the same project, nested MSBuild tasks may have spawned additional builds of other projects
            if (AnalyzerManager.NormalizePath(e.ProjectFile) == _projectFilePath)
            {
                // Get the TFM for this project
                string tfm = e.Properties?.Cast<DictionaryEntry>()
                    .FirstOrDefault(x => string.Equals(x.Key.ToString(), "TargetFrameworkMoniker", StringComparison.OrdinalIgnoreCase)).Value as string;
                if (!string.IsNullOrWhiteSpace(tfm))
                {
                    if (!_results.TryGetValue(tfm, out AnalyzerResult result))
                    {
                        result = new AnalyzerResult(_projectFilePath, _manager, _analyzer);
                        _results[tfm] = result;
                    }
                    result.ProcessProject(e);
                    _currentResult.Push(result);
                    return;
                }
            }

            // Push a null result so the stack is balanced on project finish
            _currentResult.Push(null);
        }

        private void ProjectFinished(object sender, ProjectFinishedEventArgs e)
        {
            AnalyzerResult result = _currentResult.Pop();
            if (result != null)
            {
                result.Succeeded = e.Succeeded;
            }
        }

        private void TargetStarted(object sender, TargetStartedEventArgs e)
        {
            _targetStack.Push(e);
        }

        private void TargetFinished(object sender, TargetFinishedEventArgs e)
        {
            if (_targetStack.Pop().TargetName != e.TargetName)
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
                result.ProcessCscCommandLine(cmd.CommandLine, _targetStack.Any(x => x.TargetName == "CoreCompile"));
            }
        }

        private void BuildFinished(object sender, BuildFinishedEventArgs e)
        {
            OverallSuccess = e.Succeeded;
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
