using System.Diagnostics;
using System.IO;

namespace Buildalyzer.TestTools;

/// <summary>Creates a test context for testing <see cref=IProjectAnalyzer"/>s.</summary>
/// <remarks>
/// The context ensures an fresh build (deletes previous artifacts in advance).
/// The context logs to the console in DEBUG mode.
/// </remarks>
public sealed class BuildalyzerTestContext : IDisposable
{
    public TextWriter Log => IsDisposed ? throw new ObjectDisposedException(GetType().FullName) : log;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly TextWriter log = new StringWriter();

    public BuildalyzerTestContext(FileInfo projectFile)
    {
        ProjectFile = projectFile;
        Manager = new AnalyzerManager(
            new AnalyzerManagerOptions
            {
                LogWriter = Log,
            });

        Analyzer = Manager.GetProject(projectFile.FullName);

        DebugMode(ref InDebugMode);
        AddBinaryLogger();
        DeleteSubDirectory("bin");
        DeleteSubDirectory("obj");
    }

    public FileInfo ProjectFile { get; }

    public AnalyzerManager Manager { get; }

    public IProjectAnalyzer Analyzer { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!IsDisposed)
        {
            if (InDebugMode)
            {
                Console.WriteLine(Log.ToString());
            }
            Log.Dispose();

            IsDisposed = true;
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool IsDisposed;

    /// <summary>Ensures that the analysis is done ignoring previous results.</summary>
    private void DeleteSubDirectory(string path)
    {
        var directory = new DirectoryInfo(Path.Combine(ProjectFile.Directory!.FullName, path));

        if (directory.Exists)
        {
            try
            {
                directory.Delete(true);
                Log.WriteLine($"Deleted all files at {directory}");
            }
            catch (Exception x)
            {
                Log.WriteLine(x);
            }
        }
    }

    [Conditional("BinaryLog")]
    private void AddBinaryLogger()
    {
        Analyzer.AddBinaryLogger(Path.Combine(@"C:\Temp\", Path.ChangeExtension(ProjectFile.Name, ".core.binlog")));
    }

    /// <summary>Sets <paramref name="inDebugMode"/> to true when run in DEBUG mode.</summary>
    [Conditional("DEBUG")]
    private void DebugMode(ref bool inDebugMode) => inDebugMode = true;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly bool InDebugMode;
}
