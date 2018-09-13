using Buildalyzer.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Logging.StructuredLogger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Buildalyzer.Logging
{
    internal class EventProcessor : IDisposable
    {
        private readonly string _projectFilePath;
        private readonly IEnumerable<ILogger> _loggers;
        private readonly Microsoft.Build.Logging.StructuredLogger.Construction _construction;

        public EventProcessor(string projectFilePath, IEnumerable<ILogger> loggers, IEventSource eventSource, bool analyze)
        {
            _projectFilePath = projectFilePath;
            _loggers = loggers;

            // Initialize the loggers
            foreach(ILogger logger in loggers)
            {
                logger.Initialize(eventSource);
            }

            // Send events to the tree constructor
            if(analyze)
            {
                _construction = new Microsoft.Build.Logging.StructuredLogger.Construction();
                eventSource.BuildStarted += _construction.BuildStarted;
                eventSource.BuildFinished += _construction.BuildFinished;
                eventSource.ProjectStarted += _construction.ProjectStarted;
                eventSource.ProjectFinished += _construction.ProjectFinished;
                eventSource.TargetStarted += _construction.TargetStarted;
                eventSource.TargetFinished += _construction.TargetFinished;
                eventSource.TaskStarted += _construction.TaskStarted;
                eventSource.TaskFinished += _construction.TaskFinished;
                eventSource.MessageRaised += _construction.MessageRaised;
                eventSource.WarningRaised += _construction.WarningRaised;
                eventSource.ErrorRaised += _construction.ErrorRaised;
                eventSource.CustomEventRaised += _construction.CustomEventRaised;
                eventSource.StatusEventRaised += _construction.StatusEventRaised;
            }
        }

        public void Dispose()
        {
            // Need to release the loggers in case they get used again (I.e., Restore followed by Clean;Build)
            foreach (ILogger logger in _loggers)
            {
                logger.Shutdown();
            }
        }

        public IEnumerable<AnalyzerResult> GetResults(ProjectAnalyzer analyzer)
        {
            if (_construction != null)
            {
                // Group all nested projects by TFM under a single tree
                // We want all project nodes since we don't know exactly which targets are being built
                Dictionary<string, TreeNode> projects = new Dictionary<string, TreeNode>();
                _construction.Build.VisitAllChildren<Project>(x => ProjectVisitor(x, projects));
                return projects.Values.Select(x => new AnalyzerResult(analyzer, _construction, x));
            }

            return Array.Empty<AnalyzerResult>();
        }

        private void ProjectVisitor(Project project, Dictionary<string, TreeNode> projects)
        {
            // Make sure this is the same project, nested MSBuild tasks may have spawned additional builds of other projects
            if (AnalyzerManager.NormalizePath(project.ProjectFile) != _projectFilePath)
            {
                return;
            }

            // Get the TFM for this project
            string tfm = project.GetProperty("TargetFrameworkMoniker");
            if (!string.IsNullOrWhiteSpace(tfm))
            {
                // Add this project to the tree for this TFM
                TreeNode tree = null;
                if (!projects.TryGetValue(tfm, out tree))
                {
                    tree = new NamedNode();
                    projects.Add(tfm, tree);
                }
                tree.AddChild(project);
            }
        }
    }
}
