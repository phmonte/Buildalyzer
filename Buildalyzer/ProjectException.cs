using System;

namespace Buildalyzer
{
    public class ProjectException : Exception
    {
        public ProjectException(string message, string logOutput) : base(message)
        {
            LogOutput = logOutput;
        }

        public string LogOutput { get; }
    }
}