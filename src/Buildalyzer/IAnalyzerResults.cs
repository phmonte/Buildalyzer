using System.Collections.Generic;

namespace Buildalyzer
{
    public interface IAnalyzerResults : IEnumerable<IAnalyzerResult>
    {
        IAnalyzerResult this[string targetFramework] { get; }

        int Count { get; }

        bool OverallSuccess { get; }

        IEnumerable<IAnalyzerResult> Results { get; }

        IEnumerable<string> TargetFrameworks { get; }

        bool ContainsTargetFramework(string targetFramework);

        bool TryGetTargetFramework(string targetFramework, out IAnalyzerResult result);
    }
}