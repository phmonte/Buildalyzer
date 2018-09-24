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
        private readonly ProjectAnalyzer _analyzer;
        private readonly IEnumerable<Microsoft.Build.Framework.ILogger> _loggers;
        private readonly IEventSource _eventSource;
        private readonly bool _analyze;

        public bool OverallSuccess { get; private set; }

        public IEnumerable<AnalyzerResult> Results => _results.Values;
        
        public EventProcessor(ProjectAnalyzer analyzer, IEnumerable<Microsoft.Build.Framework.ILogger> loggers, IEventSource eventSource, bool analyze)
        {
            _analyzer = analyzer;
            _loggers = loggers;
            _eventSource = eventSource;
            _analyze = analyze;

            // Initialize the loggers
            // TODO: Figure out what to do with loggers: don't filter if using loggers, what about console (use stdout?)
            foreach (Microsoft.Build.Framework.ILogger logger in loggers)
            {
                logger.Initialize(eventSource);
            }

            // Send events to the tree constructor
            if(analyze)
            {
                eventSource.ProjectStarted += ProjectStarted;
                eventSource.ProjectFinished += ProjectFinished;
                eventSource.MessageRaised += MessageRaised;
                eventSource.BuildFinished += BuildFinished;
                eventSource.ErrorRaised += ErrorRaised;
            }
        }
        
        private void ProjectStarted(object sender, ProjectStartedEventArgs e)
        {
            // Make sure this is the same project, nested MSBuild tasks may have spawned additional builds of other projects
            if (AnalyzerManager.NormalizePath(e.ProjectFile) == _analyzer.ProjectFile.Path)
            {
                // Get the TFM for this project
                string tfm = e.Properties.Cast<DictionaryEntry>()
                    .FirstOrDefault(x => string.Equals(x.Key.ToString(), "TargetFrameworkMoniker", StringComparison.OrdinalIgnoreCase)).Value as string;
                if (!string.IsNullOrWhiteSpace(tfm))
                {
                    if (!_results.TryGetValue(tfm, out AnalyzerResult result))
                    {
                        result = new AnalyzerResult(_analyzer);
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
            if(result != null)
            {
                result.Succeeded = e.Succeeded;
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
                result.ProcessCscCommandLine(cmd.CommandLine);
            }
        }

        private void BuildFinished(object sender, BuildFinishedEventArgs e)
        {
            OverallSuccess = e.Succeeded;
        }

        private void ErrorRaised(object sender, BuildErrorEventArgs e) => _analyzer.Manager.ProjectLogger.LogError(e.Message);

        public void Dispose()
        {
            if (_analyze)
            {
                _eventSource.ProjectStarted -= ProjectStarted;
                _eventSource.ProjectFinished -= ProjectFinished;
                _eventSource.MessageRaised -= MessageRaised;
                _eventSource.BuildFinished -= BuildFinished;
            }

            // Need to release the loggers in case they get used again (I.e., Restore followed by Clean;Build)
            foreach (Microsoft.Build.Framework.ILogger logger in _loggers)
            {
                logger.Shutdown();
            }
        }
    }
}
