using System;
using System.Collections.Generic;

namespace Buildalyzer.Environment
{
    public class EnvironmentVariableSetter : IDisposable
    {
        private readonly Dictionary<string, string> _originalVariables = new Dictionary<string, string>();

        public EnvironmentVariableSetter(IDictionary<string, string> newVariables)
        {
            foreach (KeyValuePair<string, string> newVariable in newVariables)
            {
                _originalVariables.Add(newVariable.Key, System.Environment.GetEnvironmentVariable(newVariable.Key));
                System.Environment.SetEnvironmentVariable(newVariable.Key, newVariable.Value);
            }
        }

        public void Dispose()
        {
            foreach (KeyValuePair<string, string> originalVariable in _originalVariables)
            {
                System.Environment.SetEnvironmentVariable(originalVariable.Key, originalVariable.Value);
            }
        }
    }
}