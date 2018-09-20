using Buildalyzer.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Buildalyzer
{
    public class AnalyzerResult
    {
        private readonly Dictionary<string, string> _properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ProjectItem[]> _items = new Dictionary<string, ProjectItem[]>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _projectReferences = new HashSet<string>();
        private List<(string, string)> _cscCommandLineArguments;

        internal AnalyzerResult(ProjectAnalyzer analyzer)
        {
            Analyzer = analyzer;
        }

        public ProjectAnalyzer Analyzer { get; }

        public bool Succeeded { get; internal set; }

        public IReadOnlyDictionary<string, string> Properties => _properties;

        public IReadOnlyDictionary<string, ProjectItem[]> Items => _items;
        
        /// <summary>
        /// Gets the value of the specified property and returns <c>null</c>
        /// if the property could not be found.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <returns>The value of the property or <c>null</c>.</returns>
        public string GetProperty(string name) =>
            Properties.TryGetValue(name, out string value) ? value : null;

        public string TargetFramework =>
            ProjectFile.GetTargetFrameworks(
                null,  // Don't want all target frameworks since the result is just for one
                new[] { GetProperty(ProjectFileNames.TargetFramework) },
                new[] { (GetProperty(ProjectFileNames.TargetFrameworkIdentifier), GetProperty(ProjectFileNames.TargetFrameworkVersion)) })
            .FirstOrDefault();

        public string[] SourceFiles =>
            _cscCommandLineArguments
                ?.Where(x => x.Item1 == null
                    && !string.Equals(Path.GetFileName(x.Item2), "csc.dll", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(Path.GetFileName(x.Item2), "csc.exe", StringComparison.OrdinalIgnoreCase))
                .Select(x => AnalyzerManager.NormalizePath(Path.Combine(Path.GetDirectoryName(Analyzer.ProjectFile.Path), x.Item2)))
                .ToArray() ?? Array.Empty<string>();

        public string[] References =>
            _cscCommandLineArguments
                ?.Where(x => x.Item1 == "reference")
                .Select(x => x.Item2)
                .ToArray() ?? Array.Empty<string>();

        public IEnumerable<string> ProjectReferences =>
            Items.TryGetValue("ProjectReference", out ProjectItem[] items)
                ? items.Select(x => AnalyzerManager.NormalizePath(
                    Path.Combine(Path.GetDirectoryName(Analyzer.ProjectFile.Path), x.ItemSpec)))
                : Array.Empty<string>();

        internal void ProcessProject(ProjectStartedEventArgs e)
        {
            // Add properties
            foreach(DictionaryEntry entry in e.Properties.Cast<DictionaryEntry>())
            {
                _properties[entry.Key.ToString()] = entry.Value.ToString();
            }

            // Add items
            foreach(IGrouping<string, DictionaryEntry> itemGroup in e.Items.Cast<DictionaryEntry>().GroupBy(x => x.Key.ToString()))
            {
                _items[itemGroup.Key] = itemGroup.Select(x => new ProjectItem((ITaskItem)x.Value)).ToArray();
            }
        }

        internal void ProcessCscCommandLine(string commandLine)
        {
            if (string.IsNullOrWhiteSpace(commandLine))
            {
                return;
            }
            _cscCommandLineArguments = new List<(string, string)>();

            string[] parts = commandLine.Split(new[] { ' ' });

            // Combine the initial command
            int start = Array.FindIndex(parts, x => x.Length > 0 && x[0] == '/');
            _cscCommandLineArguments.Add((null, string.Join(" ", parts.Take(start)).Trim('"')));

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
                            _cscCommandLineArguments.Add(
                                (valueStart == -1 ? parts[c].Substring(1) : parts[c].Substring(1, valueStart - 1), null));
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
                            _cscCommandLineArguments.Add((
                                valueStart == 0 ? null : parts[c].Substring(1, valueStart - 2),
                                parts[c].Substring(valueStart).Trim('"')));
                            continue;
                        }

                        // The end quote is in another part, join them
                        _cscCommandLineArguments.Add((
                            valueStart == 0 ? null : parts[first].Substring(1, valueStart - 2),
                            string.Join(" ", parts.Skip(first).Take(c - first + 1)).Substring(valueStart).Trim('"')));
                        continue;
                    }

                    // Not quoted, return the value
                    _cscCommandLineArguments.Add((
                        valueStart == 0 ? null : parts[c].Substring(1, valueStart - 2),
                        parts[c].Substring(valueStart)));
                }
            }
        }
    }
}