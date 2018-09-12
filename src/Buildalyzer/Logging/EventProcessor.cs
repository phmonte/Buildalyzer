using Microsoft.Build.Framework;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Buildalyzer.Logging
{
    internal class EventProcessor
    {
        private readonly Microsoft.Build.Logging.StructuredLogger.Construction _construction
            = new Microsoft.Build.Logging.StructuredLogger.Construction();

        private readonly Microsoft.Extensions.Logging.ILogger _logger;

        public EventProcessor(Microsoft.Extensions.Logging.ILogger logger)
        {
            _logger = logger;
        }

        public void Attach(IEventSource eventSource)
        {
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
}
