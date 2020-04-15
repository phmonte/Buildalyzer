using System;
using System.Collections.Generic;

namespace Buildalyzer
{
    public interface IAnalyzerResult
    {
        ProjectAnalyzer Analyzer { get; }
        IReadOnlyDictionary<string, ProjectItem[]> Items { get; }
        AnalyzerManager Manager { get; }
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> PackageReferences { get; }
        string ProjectFilePath { get; }
        Guid ProjectGuid { get; }
        IEnumerable<string> ProjectReferences { get; }
        IReadOnlyDictionary<string, string> Properties { get; }
        string[] References { get; }
        string[] SourceFiles { get; }
        bool Succeeded { get; }
        string TargetFramework { get; }

        string GetProperty(string name);
    }
}