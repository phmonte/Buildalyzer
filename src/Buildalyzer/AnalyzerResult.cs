using System.Collections;
using System.IO;
using Buildalyzer.Construction;
using Buildalyzer.Logging;

namespace Buildalyzer;

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
    private string _compilerFilePath;

    public CompilerCommand CompilerCommand { get; private set; }

    internal AnalyzerResult(string projectFilePath, AnalyzerManager manager, ProjectAnalyzer analyzer)
    {
        ProjectFilePath = projectFilePath;
        Manager = manager;
        Analyzer = analyzer;

        string projectGuid = GetProperty(nameof(ProjectGuid));
        if (string.IsNullOrEmpty(projectGuid) || !Guid.TryParse(projectGuid, out _projectGuid))
        {
            _projectGuid = analyzer == null
                ? Buildalyzer.ProjectGuid.Create(ProjectFilePath)
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
    public string Command => CompilerCommand?.Text ?? _command;

    /// <inheritdoc/>
    public string CompilerFilePath => CompilerCommand?.CompilerLocation?.ToString() ?? _compilerFilePath;

    /// <inheritdoc/>
    public string[] CompilerArguments => CompilerCommand?.Arguments.ToArray() ?? [];

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
        CompilerCommand?.SourceFiles
            .Select(file => AnalyzerManager.NormalizePath(Path.Combine(Path.GetDirectoryName(ProjectFilePath), file.Path)))
            .ToArray() ?? [];

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

    public string[] PreprocessorSymbols => CompilerCommand?.PreprocessorSymbolNames.ToArray() ?? [];

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
        foreach (var entry in propertiesAndItems.Properties)
        {
            _properties[entry.Key] = entry.StringValue;
        }

        // Add items
        foreach (var items in propertiesAndItems.Items)
        {
            _items[items.Key] = items.Values.Select(task => new ProjectItem(task)).ToArray();
        }
    }

    internal void ProcessCscCommandLine(string commandLine, bool coreCompile)
    {
        // Some projects can have multiple Csc calls (see #92) so if this is the one inside CoreCompile use it, otherwise use the first
        if (string.IsNullOrWhiteSpace(commandLine) || (_cscCommandLineArguments != null && !coreCompile))
        {
            return;
        }
        ProcessedCommandLine cmd = ProcessCscCommandLine(commandLine);
        CompilerCommand = Compiler.CommandLine.Parse(new FileInfo(ProjectFilePath).Directory, commandLine, CompilerLanguage.CSharp);
        _command = cmd.Command;
        _compilerFilePath = cmd.FileName;
        _cscCommandLineArguments = cmd.ProcessedArguments;
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

        CompilerCommand = Compiler.CommandLine.Parse(new FileInfo(ProjectFilePath).Directory, commandLine, CompilerLanguage.VisualBasic);
        _command = cmd.Command;
        _compilerFilePath = cmd.FileName;
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

        CompilerCommand = Compiler.CommandLine.Parse(new FileInfo(ProjectFilePath).Directory, commandLine, CompilerLanguage.FSharp);
        _fscCommandLineArguments = processedArguments;
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