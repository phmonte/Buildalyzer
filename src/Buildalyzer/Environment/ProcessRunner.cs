using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Buildalyzer.Environment;

internal sealed class ProcessRunner : IDisposable
{
    private readonly ILogger Logger;
    private readonly ProcessDataCollector Collector;

    public int ExitCode => Process.ExitCode;

    public ProcessData Data => Collector.Data;

    private Process Process { get; }

    public Action Exited { get; set; }

    public ProcessRunner(
        string fileName,
        string arguments,
        string workingDirectory,
        Dictionary<string, string?> environmentVariables,
        ILoggerFactory? loggerFactory)
    {
        Logger = loggerFactory?.CreateLogger<ProcessRunner>() ?? NullLogger<ProcessRunner>.Instance;
        Process = new Process
        {
            StartInfo =
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            },

            // Raises Process.Exited immediately instead of when checked via .WaitForExit() or .HasExited
            EnableRaisingEvents = true,
        };

        // Copy over environment variables
        if (environmentVariables != null)
        {
            foreach (KeyValuePair<string, string> variable in environmentVariables)
            {
                Process.StartInfo.Environment[variable.Key] = variable.Value;
                Process.StartInfo.EnvironmentVariables[variable.Key] = variable.Value;
            }
        }

        Process.OutputDataReceived += OutputDataReceived;
        Process.ErrorDataReceived += ErrorDataReceived;
        Process.Exited += OnExit;

        Collector = new(Process);
    }

    public ProcessRunner Start()
    {
        Process.Start();
        Process.BeginOutputReadLine();
        Process.BeginErrorReadLine();
        Logger.LogDebug(
            "Started process {ProcessId}: \"{FileName}\" {Arguments}{NewLine}",
            Process.Id,
            Process.StartInfo.FileName,
            Process.StartInfo.Arguments,
            System.Environment.NewLine);
        return this;
    }

    public void WaitForExit() => Process.WaitForExit();

    public bool WaitForExit(int timeout)
    {
        bool exited = Process.WaitForExit(timeout);
        if (exited)
        {
            // To ensure that asynchronous event handling has been completed, call the WaitForExit() overload that takes no parameter after receiving a true from this overload.
            // From https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.process.waitforexit?redirectedfrom=MSDN&view=netcore-3.1#System_Diagnostics_Process_WaitForExit_System_Int32_
            // See also https://github.com/dotnet/runtime/issues/27128
            Process.WaitForExit();
        }
        return exited;
    }

    private void OutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            Logger.LogDebug("{Data}{NewLine}", e.Data, NewLine);
        }
    }

    private void ErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            Logger.LogError("{Data}{NewLine}", e.Data, NewLine);
        }
    }

    private void OnExit(object? sender, EventArgs e)
    {
        Exited?.Invoke();
        Logger.LogDebug(
            "Process {Id} exited with code {ExitCode}{NewLine}",
            Process.Id,
            Process.ExitCode,
            NewLine);
    }

    public void Dispose()
    {
        Process.OutputDataReceived -= OutputDataReceived;
        Process.ErrorDataReceived -= ErrorDataReceived;
        Process.Exited -= OnExit;
        Process.Close();
        Collector.Dispose();
    }

    private static string NewLine => System.Environment.NewLine;
}
