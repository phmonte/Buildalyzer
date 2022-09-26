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
        private readonly List<string> _assemblyObjects = new List<string>
        {
            "vbc.exe",
            "vbc.dll",
            "csc.exe",
            "csc.dll",
            "fsc.exe",
            "fsc.dll"
        };
        private List<(string, string)> _cscCommandLineArguments;
        private List<(string, string)> _fscCommandLineArguments;
        private List<(string, string)> _vbcCommandLineArguments;
        private string _command;
        private string[] _compilerArguments;
        private string _compilerFilePath;

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
        public string Command => _command;

        /// <inheritdoc/>
        public string CompilerFilePath => _compilerFilePath;

        /// <inheritdoc/>
        public string[] CompilerArguments => _compilerArguments;

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
                .ToArray() ?? _vbcCommandLineArguments
                ?.Where(x => x.Item1 == null && !_assemblyObjects.Contains(Path.GetFileName(x.Item2), StringComparer.OrdinalIgnoreCase))
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
                .ToArray()
            ?? _vbcCommandLineArguments
                ?.Where(x => x.Item1 is object && x.Item1.Equals("reference", StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Item2)
                .ToArray()
            ?? Array.Empty<string>();

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
                .ToArray()
            ?? _vbcCommandLineArguments
                ?.Where(x => x.Item1 is object && x.Item1.Equals("define", StringComparison.OrdinalIgnoreCase))
                .SelectMany(x => x.Item2.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(x => x.Trim())
                .ToArray()
            ?? Array.Empty<string>();

        public string[] AdditionalFiles =>
            _cscCommandLineArguments
                ?.Where(x => x.Item1 is object && x.Item1.Equals("additionalfile", StringComparison.OrdinalIgnoreCase))
                .SelectMany(x => x.Item2.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(x => x.Trim())
                .ToArray()
            ?? _vbcCommandLineArguments
                ?.Where(x => x.Item1 is object && x.Item1.Equals("additionalfile", StringComparison.OrdinalIgnoreCase))
                .SelectMany(x => x.Item2.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(x => x.Trim())
                .ToArray()
            ?? Array.Empty<string>();

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
            if (string.IsNullOrWhiteSpace(commandLine))
            {
                return;
            }

            ProcessedCommandLine cmd = ProcessCscCommandLine(commandLine);

            // Some projects can have multiple Csc calls (see #92) so if this is the one inside CoreCompile use it, otherwise use the first
            if (coreCompile)
            {
                _command = cmd.Command;
                _compilerFilePath = cmd.FileName;
                _compilerArguments = cmd.Arguments.ToArray();
            }

            // Azure function app projects have multiple Csc calls all of which are marked as coreCompile, so aggregate the ProcessedArguments.
            _cscCommandLineArguments ??= new List<(string, string)>();
            _cscCommandLineArguments.AddRange(cmd.ProcessedArguments);
        }

        internal static ProcessedCommandLine ProcessCscCommandLine(string commandLine)
        {
            return ProcessCommandLine(commandLine, "csc.");
        }

        internal struct ProcessedCommandLine
        {
            public string Command;
            public string FileName;
            public List<string> Arguments;
            public List<(string, string)> ProcessedArguments;
        }

        internal static ProcessedCommandLine ProcessCommandLine(string commandLine, string initialCommandEnd)
        {
            ProcessedCommandLine cmd;
            cmd.Command = commandLine;
            cmd.FileName = string.Empty;
            cmd.Arguments = new List<string>();
            cmd.ProcessedArguments = new List<(string, string)>();

            using (IEnumerator<string> enumerator = EnumerateCommandLineParts(commandLine, initialCommandEnd).GetEnumerator())
            {
                if (!enumerator.MoveNext())
                {
                    return cmd;
                }

                // Initial command (csc)
                cmd.ProcessedArguments.Add((null, enumerator.Current));
                cmd.FileName = enumerator.Current;

                // Iterate the rest of parts
                while (enumerator.MoveNext())
                {
                    string part = enumerator.Current;
                    cmd.Arguments.Add(part);

                    if (part[0] == '/')
                    {
                        int valueStart = part.IndexOf(':');
                        if (valueStart >= 0 && valueStart < part.Length - 1)
                        {
                            // Argument with a value
                            cmd.ProcessedArguments.Add((part.Substring(1, valueStart - 1), part.Substring(valueStart + 1)));
                        }
                        else
                        {
                            // Switch
                            cmd.ProcessedArguments.Add((valueStart >= 0 ? part.Substring(1, valueStart - 1) : part.Substring(1), null));
                        }
                    }
                    else
                    {
                        // Argument, not a switch
                        cmd.ProcessedArguments.Add((null, part));
                    }
                }
            }

            return cmd;
        }

        internal void ProcessVbcCommandLine(string commandLine)
        {
            ProcessedCommandLine cmd = ProcessCommandLine(commandLine, "vbc.");

            // vbc comma delimits the references, enumerate and replace
            int referencesIdx = cmd.ProcessedArguments.FindIndex(x => x.Item1 == "reference");
            if (referencesIdx >= 0)
            {
                (string, string) references = cmd.ProcessedArguments[referencesIdx];
                cmd.Arguments.RemoveAt(referencesIdx);
                cmd.ProcessedArguments.RemoveAt(referencesIdx);
                foreach (string r in references.Item2.Split(','))
                {
                    cmd.Arguments.Add("/reference:\"" + r + "\"");
                    cmd.ProcessedArguments.Add((references.Item1, r));
                }
            }

            _command = cmd.Command;
            _compilerFilePath = cmd.FileName;
            _compilerArguments = cmd.Arguments.ToArray();
            _vbcCommandLineArguments = cmd.ProcessedArguments;
        }

        public bool HasFscArguments()
        {
            return _fscCommandLineArguments?.Count > 0;
        }

        private static IEnumerable<string> EnumerateCommandLineParts(string commandLine, string initialCommandEnd)
        {
            StringBuilder part = new StringBuilder();
            bool isInQuote = false;
            bool initialCommand = true;

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

                    if (initialCommand && part.ToString().EndsWith(initialCommandEnd, StringComparison.InvariantCultureIgnoreCase))
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
            _command = commandLine;

            List<(string, string)> processedArguments = new List<(string, string)>();
            List<string> arguments = new List<string>();

            bool initialCommand = true;
            using (IEnumerator<string> enumerator = EnumerateCommandLinePartsFsc(commandLine, initialCommand).GetEnumerator())
            {
                // Initial command (fsc)
                processedArguments.Add((null, enumerator.Current));
                _compilerFilePath = enumerator.Current;
                initialCommand = false;

                // Iterate the rest of parts
                while (enumerator.MoveNext())
                {
                    string part = enumerator.Current;
                    arguments.Add(part);

                    if (part[0] == '-')
                    {
                        int valueStart = part.IndexOf(':');
                        if (valueStart >= 0 && valueStart < part.Length - 1)
                        {
                            // Argument with a value
                            processedArguments.Add((part.Substring(1, valueStart - 1), part.Substring(valueStart + 1)));
                        }
                        else
                        {
                            // Switch
                            processedArguments.Add((valueStart >= 0 ? part.Substring(1, valueStart - 1) : part.Substring(1), null));
                        }
                    }
                    else
                    {
                        // Argument, not a switch
                        processedArguments.Add((null, part));
                    }
                }
            }

            _fscCommandLineArguments = processedArguments;
            _compilerArguments = arguments.ToArray();
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
            public bool Equals(IProjectItem x, IProjectItem y) => x.ItemSpec.Equals(y.ItemSpec, StringComparison.OrdinalIgnoreCase);

            public int GetHashCode(IProjectItem obj) => obj.ItemSpec.ToLowerInvariant().GetHashCode();
        }
    }
}