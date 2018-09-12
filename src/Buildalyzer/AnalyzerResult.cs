using Buildalyzer.Construction;
using Buildalyzer.Environment;
using Microsoft.Build.Execution;
using Microsoft.Build.Logging.StructuredLogger;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Buildalyzer
{
    public class AnalyzerResult
    {
        private readonly Microsoft.Build.Logging.StructuredLogger.Construction _construction;
        private readonly TreeNode _tree;

        internal AnalyzerResult(
            ProjectAnalyzer analyzer,
            Microsoft.Build.Logging.StructuredLogger.Construction construction,
            TreeNode tree)
        {
            Analyzer = analyzer;
            _construction = construction;
            _tree = tree;
        }

        public ProjectAnalyzer Analyzer { get; }

        public bool OverallSuccess => _construction.Build.Succeeded;

        // Get the last property to try and get the final value
        public string GetProperty(string name) =>
            _tree.Children.OfType<Project>().Reverse()
            .Select(x => x.GetProperty(name))
            .FirstOrDefault(x => x != null);

        public string TargetFramework =>
            ProjectFile.GetTargetFrameworks(
                null,  // Don't want all target frameworks since the result is just for one
                new[] { GetProperty(ProjectFileNames.TargetFramework) },
                new[] { (GetProperty(ProjectFileNames.TargetFrameworkIdentifier), GetProperty(ProjectFileNames.TargetFrameworkVersion)) })
            .FirstOrDefault();
        
        public IReadOnlyList<string> GetSourceFiles() =>
            GetCscCommandLineArgs()
                .Where(x => x.Item1 == null
                    && !string.Equals(Path.GetFileName(x.Item2), "csc.dll", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(Path.GetFileName(x.Item2), "csc.exe", StringComparison.OrdinalIgnoreCase))
                .Select(x => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Analyzer.ProjectFile.Path), x.Item2)))
                .ToList();

        public IReadOnlyList<string> GetReferences() =>
            GetCscCommandLineArgs()
                .Where(x => x.Item1 == "reference")
                .Select(x => x.Item2)
                .ToList();

        public IReadOnlyList<string> GetProjectReferences() => Array.Empty<string>();

        //public IReadOnlyList<string> GetProjectReferences() =>
        //    ProjectInstance ?.Items
        //        .Where(x => x.ItemType == "ProjectReference")
        //        .Select(x => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Analyzer.ProjectFile.Path), x.EvaluatedInclude)))
        //        .ToList();

        private (string, string)[] _cscCommandLineArgs;

        private (string, string)[] GetCscCommandLineArgs()
        {
            if(_cscCommandLineArgs == null)
            {
                _cscCommandLineArgs =
                    ParseCommandLineArguments(
                        _tree
                            .FindLastDescendant<Task>(x => string.Equals(x.Name, "Csc", StringComparison.OrdinalIgnoreCase))
                            ?.GetValue("CommandLineArguments")).ToArray();
            }
            return _cscCommandLineArgs;
        }

        private static IEnumerable<(string, string)> ParseCommandLineArguments(string str)
        {
            if(string.IsNullOrWhiteSpace(str))
            {
                yield break;
            }

            string[] parts = str.Split(new[] { ' ' });

            // Combine the initial command
            int start = Array.FindIndex(parts, x => x.Length > 0 && x[0] == '/');
            yield return (null, string.Join(" ", parts.Take(start)).Trim('"'));

            // Iterate the rest of them
            for (int c = start; c < parts.Length; c++)
            {
                if (parts[c].Length > 0)
                {
                    int valueStart = 0;
                    if (parts[c][0] == '/')
                    {
                        valueStart = parts[c].IndexOf(':');
                        if (valueStart == -1 || valueStart >= parts[c].Length - 1)
                        {
                            // Argument without a value
                            yield return (valueStart == -1 ? parts[c].Substring(1) : parts[c].Substring(1, valueStart - 1), null);
                            continue;
                        }
                        valueStart++;  // Move to the value
                    }

                    if (parts[c][valueStart] == '"')
                    {
                        // The value is quoted, find the end quote
                        int first = c;
                        while (c < parts.Length
                            && parts[c][parts[c].Length - 1] != '"'
                            && (parts[c].Length > 1 || parts[c][parts[c].Length - 2] != '\\'))
                        {
                            c++;
                        }

                        if (first == c)
                        {
                            // The end quote was in the same part
                            yield return (
                                valueStart == 0 ? null : parts[c].Substring(1, valueStart - 2),
                                parts[c].Substring(valueStart).Trim('"'));
                            continue;
                        }

                        // The end quote is in another part, join them
                        yield return (
                            valueStart == 0 ? null : parts[first].Substring(1, valueStart - 2),
                            string.Join(" ", parts.Skip(first).Take(c - first + 1)).Substring(valueStart).Trim('"'));
                        continue;
                    }

                    // Not quoted, return the value
                    yield return (
                        valueStart == 0 ? null : parts[c].Substring(1, valueStart - 2),
                        parts[c].Substring(valueStart));
                }
            }
        }
    }
}