using System;
using System.IO;
using System.Xml.Linq;
using Buildalyzer.Logging;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;

namespace Buildalyzer
{
    public class AnalyzerManagerOptions
    {
        public ILoggerFactory LoggerFactory { get; set; }
        public LoggerVerbosity LoggerVerbosity { get; set; } = LoggerVerbosity.Normal;
        public ProjectTransformer ProjectTransformer { get; set; }
        public bool CleanBeforeCompile { get; set; } = true;

        public TextWriter LogWriter
        {
            set
            {
                if(value == null)
                {
                    LoggerFactory = null;
                    return;
                }

                LoggerFactory = new LoggerFactory();
                LoggerFactory.AddProvider(new TextWriterLoggerProvider(value));
            }
        }

        private static ILoggerFactory CreateLoggerFactory(TextWriter logWriter)
        {
            if (logWriter != null)
            {
                LoggerFactory loggerFactory = new LoggerFactory();
                loggerFactory.AddProvider(new TextWriterLoggerProvider(logWriter));
                return loggerFactory;
            }

            return null;
        }
    }
}
