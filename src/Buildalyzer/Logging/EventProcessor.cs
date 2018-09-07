using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Buildalyzer.Logging
{
    internal class EventProcessor
    {
        private readonly Microsoft.Extensions.Logging.ILogger _logger;

        public EventProcessor(Microsoft.Extensions.Logging.ILogger logger)
        {
            _logger = logger;
        }

        public void Attach(IEventSource eventSource)
        {
            eventSource.WarningRaised += (s, e) => _logger.LogWarning($"{e.Message}{System.Environment.NewLine}");
            eventSource.ErrorRaised += (s, e) => _logger.LogError($"{e.Message}{System.Environment.NewLine}");
            eventSource.ProjectStarted += (s, e) => _logger.LogInformation($"{e.Message}{System.Environment.NewLine}");
            eventSource.ProjectFinished += (s, e) => _logger.LogInformation($"{e.Message}{System.Environment.NewLine}");
        }
    }
}
