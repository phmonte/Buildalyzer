using Buildalyzer.IO;
using Microsoft.Build.Framework;

namespace Buildalyzer.Processors;

public sealed class FSharpCommandLineProcessor : CommandLineProcessor
{
    [Pure]
    public override bool IsApplicable(BuildMessageEventArgs e)
        => e.Message is { Length: > 0 }
        && e.SenderName?.ToUpperInvariant() == "FSC";

    [Pure]
    public override string[]? SplitCommandLineIntoArguments(string commandLine)
     => commandLine.Split(Splitters, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } args
     && First(args[0]).ToArray() is { Length: >= 1 } first
         ? first.Concat(args[1..]).ToArray()
         : null;

    [Pure]
    protected override CompilerCommand Parse(string? baseDir, string? root, string[] args)
    {
        var sourceFiles = args.Where(a => a[0] != '-').Select(IOPath.Parse);
        var preprocessorSymbolNames = args.Where(a => a.StartsWith("--define:")).Select(a => a[9..]);
        var metadataReferences = args.Where(a => a.StartsWith("-r:")).Select(a => a[3..]);

        return new FSharpCompilerCommand()
        {
            MetadataReferences = metadataReferences.ToImmutableArray(),
            PreprocessorSymbolNames = preprocessorSymbolNames.ToImmutableArray(),
            SourceFiles = sourceFiles.ToImmutableArray(),
        };
    }

    [Pure]
    private static IEnumerable<string> First(string arg)
        => Tokenize(arg)
        .SkipWhile(NotCompilerLocation)
        .Select(a => a.Trim())
        .Where(a => a.Length > 0);

    [Pure]
    private static IEnumerable<string> Tokenize(string arg)
    {
        var first = 0;
        var cursor = 0;
        var quote = false;

        foreach (var ch in arg)
        {
            if (ch == '"')
            {
                if (quote)
                {
                    quote = false;
                    yield return arg[first..cursor];
                }
                else
                {
                    quote = true;
                }
                first = cursor + 1;
            }
            else if (ch == ' ' && cursor >= first && !quote)
            {
                yield return arg[first..cursor];
                first = cursor + 1;
            }
            cursor++;
        }
        yield return arg[first..];
    }

    [Pure]
    private static bool NotCompilerLocation(string s)
        => !s.EndsWith("fsc.dll", StringComparison.OrdinalIgnoreCase)
        && !s.EndsWith("fsc.exe", StringComparison.OrdinalIgnoreCase);

    private static readonly char[] Splitters = ['\r', '\n'];
}
