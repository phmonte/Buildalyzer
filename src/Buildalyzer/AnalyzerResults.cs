using System.Collections;
using System.Collections.Generic;

namespace Buildalyzer
{
    public class AnalyzerResults : IEnumerable<IAnalyzerResult>, IAnalyzerResults
    {
        private readonly Dictionary<string, IAnalyzerResult> _results = new Dictionary<string, IAnalyzerResult>();

        private bool? _overallSuccess = null;

        public bool OverallSuccess => _overallSuccess == true;

        internal void Add(IEnumerable<IAnalyzerResult> results, bool overallSuccess)
        {
            foreach (IAnalyzerResult result in results)
            {
                _results[result.TargetFramework ?? string.Empty] = result;
            }
            _overallSuccess = _overallSuccess.HasValue ? _overallSuccess.Value && overallSuccess : overallSuccess;
        }

        public IAnalyzerResult this[string targetFramework] => _results[targetFramework];

        public IEnumerable<string> TargetFrameworks => _results.Keys;

        public IEnumerable<IAnalyzerResult> Results => _results.Values;

        public int Count => _results.Count;

        public bool ContainsTargetFramework(string targetFramework) => _results.ContainsKey(targetFramework);

        public bool TryGetTargetFramework(string targetFramework, out IAnalyzerResult result) => _results.TryGetValue(targetFramework, out result);

        public IEnumerator<IAnalyzerResult> GetEnumerator() => _results.Values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}