using System.IO;
using Buildalyzer.Caching;
using Microsoft.Extensions.Logging;

namespace Buildalyzer.Environment;

internal class DotnetPathResolver
{
    private const int DefaultDotNetInfoWaitTime = 10000; // 10 seconds

    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<DotnetPathResolver> _logger;

    public DotnetPathResolver(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory?.CreateLogger<DotnetPathResolver>();
    }

    public string ResolvePath(string projectPath, string dotnetExePath)
    {
        dotnetExePath ??= "dotnet";
        IReadOnlyCollection<string> output = GetInfo(projectPath, dotnetExePath);

        var info = DotNetInfo.Parse(output);
        var basePath = info.BasePath ?? info.Runtimes.Values.FirstOrDefault();

        if (basePath is null)
        {
            _logger?.LogWarning($"Could not locate SDK path in `{dotnetExePath} --info` results");
            return null;
        }

        return basePath;
    }

    private IReadOnlyCollection<string> GetInfo(string projectPath, string dotnetExePath)
    {
        // Ensure that we set the DOTNET_CLI_UI_LANGUAGE environment variable to "en-US" before
        // running 'dotnet --info'. Otherwise, we may get localized results
        // Also unset some MSBuild variables, see https://github.com/OmniSharp/omnisharp-roslyn/blob/df160f86ce906bc566fe3e04e38a4077bd6023b4/src/OmniSharp.Abstractions/Services/DotNetCliService.cs#L36
        Dictionary<string, string> environmentVariables = new Dictionary<string, string>
        {
            { EnvironmentVariables.DOTNET_CLI_UI_LANGUAGE, "en-US" },
            { EnvironmentVariables.MSBUILD_EXE_PATH, null },
            { EnvironmentVariables.COREHOST_TRACE, "0" },
            { MsBuildProperties.MSBuildExtensionsPath, null }
        };

        IReadOnlyCollection<string> dotnetInfoCache = DotnetInfoCache.GetCache(projectPath);

        if (dotnetInfoCache == null)
        {
            // global.json may change the version, so need to set working directory
            using (ProcessRunner processRunner = new ProcessRunner(dotnetExePath, "--info", Path.GetDirectoryName(projectPath), environmentVariables, _loggerFactory))
            {
                int dotnetInfoWaitTime = int.TryParse(System.Environment.GetEnvironmentVariable(EnvironmentVariables.DOTNET_INFO_WAIT_TIME), out int dotnetInfoWaitTimeParsed)
                    ? dotnetInfoWaitTimeParsed
                    : DefaultDotNetInfoWaitTime;
                _logger?.LogInformation($"dotnet --info wait time is {dotnetInfoWaitTime}ms");
                processRunner.Start();
                processRunner.WaitForExit(dotnetInfoWaitTime);
                IReadOnlyCollection<string> dotnetInfoOutput = processRunner.Output;
                DotnetInfoCache.AddCache(projectPath, dotnetInfoOutput);
                return dotnetInfoOutput;
            }
        }
        return dotnetInfoCache;
    }
}