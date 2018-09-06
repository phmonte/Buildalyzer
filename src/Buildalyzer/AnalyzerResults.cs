using System.Collections;
using System.Collections.Generic;

namespace Buildalyzer
{
    public class AnalyzerResults : IEnumerable<AnalyzerResult>
    {
        private readonly Dictionary<string, AnalyzerResult> _results = new Dictionary<string, AnalyzerResult>();

        internal AnalyzerResults()
        {
        }

        internal AnalyzerResults(IEnumerable<AnalyzerResult> results)
        {
            Add(results);
        }

        internal void Add(AnalyzerResult result) => _results.Add(result.TargetFramework ?? string.Empty, result);

        internal void Add(IEnumerable<AnalyzerResult> results)
        {
            foreach (AnalyzerResult result in results)
            {
                Add(result);
            }
        }

        public AnalyzerResult this[string targetFramework] => _results[targetFramework];

        public IEnumerable<string> TargetFrameworks => _results.Keys;

        public IEnumerable<AnalyzerResult> Results => _results.Values;

        public int Count => _results.Count;

        public bool ContainsTargetFramework(string targetFramework) => _results.ContainsKey(targetFramework);

        public bool TryGetTargetFramework(string targetFramework, out AnalyzerResult result) => _results.TryGetValue(targetFramework, out result);

        public IEnumerator<AnalyzerResult> GetEnumerator() => _results.Values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}