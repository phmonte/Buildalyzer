using System.Collections.Generic;
using Microsoft.Build.Construction;
using Microsoft.Extensions.Logging;

namespace Buildalyzer
{
    public interface IAnalyzerManager
    {
        ILoggerFactory LoggerFactory { get; set; }
        IReadOnlyDictionary<string, IProjectAnalyzer> Projects { get; }
        SolutionFile SolutionFile { get; }
        string SolutionFilePath { get; }

        IAnalyzerResults Analyze(string binLogPath, IEnumerable<Microsoft.Build.Framework.ILogger> buildLoggers = null);
        IProjectAnalyzer GetProject(string projectFilePath);
        void RemoveGlobalProperty(string key);
        void SetEnvironmentVariable(string key, string value);
        void SetGlobalProperty(string key, string value);
    }
}