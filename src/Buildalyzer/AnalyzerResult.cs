using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Buildalyzer.Construction;
using Buildalyzer.Logging;
using Microsoft.Build.Framework;

namespace Buildalyzer
{
    public class AnalyzerResult : IAnalyzerResult
    {
        private readonly Dictionary<string, string> _properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IProjectItem[]> _items = new Dictionary<string, IProjectItem[]>(StringComparer.OrdinalIgnoreCase);
        private readonly Guid _projectGuid;
        private List<(string, string)> _cscCommandLineArguments;
        private List<(string, string)> _fscCommandLineArguments;

        internal AnalyzerResult(string projectFilePath, AnalyzerManager manager, ProjectAnalyzer analyzer)
        {
            ProjectFilePath = projectFilePath;
            Manager = manager;
            Analyzer = analyzer;

            string projectGuid = GetProperty(nameof(ProjectGuid));
            if (string.IsNullOrEmpty(projectGuid) || !Guid.TryParse(projectGuid, out _projectGuid))
            {
                _projectGuid = analyzer == null
                    ? GuidUtility.Create(GuidUtility.UrlNamespace, ProjectFilePath)
                    : analyzer.ProjectGuid;
            }
        }

        /// <inheritdoc/>
        public string ProjectFilePath { get; }

        public AnalyzerManager Manager { get; }

        /// <inheritdoc/>
        public ProjectAnalyzer Analyzer { get; }

        public bool Succeeded { get; internal set; }

        public IReadOnlyDictionary<string, string> Properties => _properties;

        public IReadOnlyDictionary<string, IProjectItem[]> Items => _items;

        /// <inheritdoc/>
        public Guid ProjectGuid => _projectGuid;

        /// <inheritdoc/>
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
                .Select(x => AnalyzerManager.NormalizePath(Path.Combine(Path.GetDirectoryName(ProjectFilePath), x.Item2)))
                .ToArray() ?? _fscCommandLineArguments
                ?.Where(x => x.Item1 == null
                    && !x.Item2.Contains("fsc.dll")
                    && !x.Item2.Contains("fsc.exe"))
                .Select(x => AnalyzerManager.NormalizePath(Path.Combine(Path.GetDirectoryName(ProjectFilePath), x.Item2)))
                .ToArray() ?? Array.Empty<string>();

        public string[] References =>
            _cscCommandLineArguments
                ?.Where(x => x.Item1 is object && x.Item1.Equals("reference", StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Item2)
                .ToArray()
            ?? _fscCommandLineArguments
                ?.Where(x => x.Item1 is object && x.Item1.Equals("r", StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Item2)
                .ToArray() ?? Array.Empty<string>();

        public string[] AnalyzerReferences =>
            _cscCommandLineArguments
                ?.Where(x => x.Item1 is object && x.Item1.Equals("analyzer", StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Item2)
                .ToArray() ?? Array.Empty<string>();

        public string[] PreprocessorSymbols =>
            _cscCommandLineArguments
                ?.Where(x => x.Item1 is object && x.Item1.Equals("define", StringComparison.OrdinalIgnoreCase))
                .SelectMany(x => x.Item2.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(x => x.Trim())
                .ToArray() ?? Array.Empty<string>();

        public IEnumerable<string> ProjectReferences =>
            Items.TryGetValue("ProjectReference", out IProjectItem[] items)
                ? items.Distinct(new ProjectItemItemSpecEqualityComparer())
                       .Select(x => AnalyzerManager.NormalizePath(
                            Path.Combine(Path.GetDirectoryName(ProjectFilePath), x.ItemSpec)))
                : Array.Empty<string>();

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> PackageReferences =>
            Items.TryGetValue("PackageReference", out IProjectItem[] items)
                ? items.Distinct(new ProjectItemItemSpecEqualityComparer()).ToDictionary(x => x.ItemSpec, x => x.Metadata)
                : new Dictionary<string, IReadOnlyDictionary<string, string>>();

        internal void ProcessProject(PropertiesAndItems propertiesAndItems)
        {
            // Add properties
            foreach (DictionaryEntry entry in propertiesAndItems.Properties.ToDictionaryEntries())
            {
                _properties[entry.Key.ToString()] = entry.Value.ToString();
            }

            // Add items
            foreach (IGrouping<string, DictionaryEntry> itemGroup in propertiesAndItems.Items.ToDictionaryEntries().GroupBy(x => x.Key.ToString()))
            {
                _items[itemGroup.Key] = itemGroup.Select(x => new ProjectItem((ITaskItem)x.Value)).ToArray();
            }
        }

        internal void ProcessCscCommandLine(string commandLine, bool coreCompile)
        {
            // Some projects can have multiple Csc calls (see #92) so if this is the one inside CoreCompile use it, otherwise use the first
            if (string.IsNullOrWhiteSpace(commandLine) || (_cscCommandLineArguments != null && !coreCompile))
            {
                return;
            }
            _cscCommandLineArguments = ProcessCscCommandLine(commandLine);
        }

        internal static List<(string, string)> ProcessCscCommandLine(string commandLine)
        {
            List<(string, string)> args = new List<(string, string)>();

            bool initialCommand = true;
            using (IEnumerator<string> enumerator = EnumerateCommandLineParts(commandLine, initialCommand).GetEnumerator())
            {
                if (!enumerator.MoveNext())
                {
                    return args;
                }

                // Initial command (csc)
                args.Add((null, enumerator.Current));
                initialCommand = false;

                // Iterate the rest of parts
                while (enumerator.MoveNext())
                {
                    string part = enumerator.Current;

                    if (part[0] == '/')
                    {
                        int valueStart = part.IndexOf(':');
                        if (valueStart >= 0 && valueStart < part.Length - 1)
                        {
                            // Argument with a value
                            args.Add((part.Substring(1, valueStart - 1), part.Substring(valueStart + 1)));
                        }
                        else
                        {
                            // Switch
                            args.Add((valueStart >= 0 ? part.Substring(1, valueStart - 1) : part.Substring(1), null));
                        }
                    }
                    else
                    {
                        // Argument, not a switch
                        args.Add((null, part));
                    }
                }
            }

            return args;
        }

        public bool HasFscArguments()
        {
            return _fscCommandLineArguments?.Count > 0;
        }

        private static IEnumerable<string> EnumerateCommandLineParts(string commandLine, bool initialCommand)
        {
            StringBuilder part = new StringBuilder();
            bool isInQuote = false;

            using (StringReader reader = new StringReader(commandLine))
            {
                while (reader.Read() is int c && c >= 0)
                {
                    switch (c)
                    {
                        case '\\':
                            int next = reader.Read();
                            if (next == '"')
                            {
                                // Escaped quote
                                part.Append('"');
                            }
                            else
                            {
                                // Not an escape
                                part.Append((char)c);

                                if (next >= 0)
                                {
                                    part.Append((char)next);
                                }
                            }
                            break;
                        case '\t':
                        case '\n':
                        case '\v':
                        case '\f':
                        case '\r':
                        case ' ':
                            if (isInQuote || initialCommand)
                            {
                                // Treat as a normal char
                                part.Append((char)c);
                            }
                            else if (part.Length > 0)
                            {
                                // End of the part
                                yield return part.ToString();
                                part.Clear();
                            }
                            break;
                        case '"':
                            isInQuote = !isInQuote;
                            break;
                        default:
                            part.Append((char)c);
                            break;
                    }

                    if (initialCommand && part.ToString().EndsWith("csc.", StringComparison.InvariantCultureIgnoreCase))
                    {
                        initialCommand = false;
                    }
                }
            }

            if (part.Length > 0)
            {
                yield return part.ToString();
            }
        }

        internal void ProcessFscCommandLine(string commandLine)
        {
            List<(string, string)> args = new List<(string, string)>();

            bool initialCommand = true;
            using (IEnumerator<string> enumerator = EnumerateCommandLinePartsFsc(commandLine, initialCommand).GetEnumerator())
            {
                if (!enumerator.MoveNext())
                {
                    _fscCommandLineArguments = args;
                }

                // Initial command (fsc)
                args.Add((null, enumerator.Current));
                initialCommand = false;

                // Iterate the rest of parts
                while (enumerator.MoveNext())
                {
                    string part = enumerator.Current;

                    if (part[0] == '-')
                    {
                        int valueStart = part.IndexOf(':');
                        if (valueStart >= 0 && valueStart < part.Length - 1)
                        {
                            // Argument with a value
                            args.Add((part.Substring(1, valueStart - 1), part.Substring(valueStart + 1)));
                        }
                        else
                        {
                            // Switch
                            args.Add((valueStart >= 0 ? part.Substring(1, valueStart - 1) : part.Substring(1), null));
                        }
                    }
                    else
                    {
                        // Argument, not a switch
                        args.Add((null, part));
                    }
                }
            }

            _fscCommandLineArguments = args;
        }

        private static IEnumerable<string> EnumerateCommandLinePartsFsc(string commandLine, bool initialCommand)
        {
            StringBuilder part = new StringBuilder();
            bool isInQuote = false;

            using (StringReader reader = new StringReader(commandLine))
            {
                while (reader.Read() is int c && c >= 0)
                {
                    switch (c)
                    {
                        case '\\':
                            int next = reader.Read();
                            if (next == '"')
                            {
                                // Escaped quote
                                part.Append('"');
                            }
                            else
                            {
                                // Not an escape
                                part.Append((char)c);

                                if (next >= 0)
                                {
                                    part.Append((char)next);
                                }
                            }
                            break;
                        case '\t':
                        case '\n':
                        case '\v':
                        case '\f':
                        case '\r':
                            if (isInQuote || initialCommand)
                            {
                                // Treat as a normal char
                                part.Append((char)c);
                            }
                            else if (reader.Read() == '\n')
                            {
                                // End of the part
                                yield return part.ToString();
                                part.Clear();
                            }
                            break;

                        // case ' ':
                        case '"':
                            isInQuote = !isInQuote;
                            break;
                        default:
                            part.Append((char)c);
                            break;
                    }

                    if (initialCommand && part.ToString().EndsWith("fsc.", StringComparison.InvariantCultureIgnoreCase))
                    {
                        initialCommand = false;
                    }
                }
            }

            if (part.Length > 0)
            {
                yield return part.ToString();
            }
        }

        private class ProjectItemItemSpecEqualityComparer : IEqualityComparer<IProjectItem>
        {
            public bool Equals(IProjectItem x, IProjectItem y) => x.ItemSpec.Equals(y.ItemSpec, StringComparison.CurrentCultureIgnoreCase);
            public int GetHashCode(IProjectItem obj) => obj.ItemSpec.ToLower().GetHashCode();
        }
    }
}