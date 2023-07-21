using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Buildalyzer.Environment;
using NUnit.Framework;
using Shouldly;

namespace Buildalyzer.Tests.Environment
{
    [TestFixture]
    public class DotnetPathResolverFixture
    {
        [TestCase(WindowsNetOutput, @"C:\Program Files\dotnet\sdk\8.0.100-preview.4.23260.5\")]
        [TestCase(WindowsNetCoreOutput, @"C:\Program Files\dotnet\sdk\2.1.300\")]
        [TestCase(LinuxOutput, "/usr/lib/dotnet/sdk/7.0.109/")]
        public void CanParseBasePath([NotNull] string output, string basePath)
        {
            // Given
            List<string> lines = output.Split("\n").Select(x => x.Trim('\r')).ToList();

            // When
            string result = DotnetPathResolver.ParseBasePath(lines);

            // Then
            result.ShouldBe(basePath);
        }

        [TestCase(WindowsNetOutput, @"C:\Program Files\dotnet\sdk\8.0.100-preview.4.23260.5\")]
        [TestCase(WindowsNetCoreOutput, @"C:\Program Files\dotnet\sdk\2.1.201\")]
        [TestCase(LinuxOutput, "/usr/lib/dotnet/sdk/7.0.109/")]
        public void CanParseInstalledSdksPath([NotNull] string output, string sdksPath)
        {
            // Given
            List<string> lines = output.Split("\n").Select(x => x.Trim('\r')).ToList();

            // When
            string result = DotnetPathResolver.ParseInstalledSdksPath(lines);

            // Then
            AnalyzerManager.NormalizePath(result).ShouldBe(AnalyzerManager.NormalizePath(sdksPath));
        }

        public const string LinuxOutput = @".NET Core SDK (reflecting any global.json):

.NET SDK:
 Version:   7.0.109
 Commit:    3e9283a8e9

Runtime Environment:
 OS Name:     ubuntu
 OS Version:  22.04
 OS Platform: Linux
 RID:         ubuntu.22.04-x64
 Base Path:   /usr/lib/dotnet/sdk/7.0.109/

Host:
  Version:      7.0.9
  Architecture: x64
  Commit:       8e9a17b221

.NET SDKs installed:
  6.0.120 [/usr/lib/dotnet/sdk]
  7.0.109 [/usr/lib/dotnet/sdk]

.NET runtimes installed:
  Microsoft.AspNetCore.App 6.0.20 [/usr/lib/dotnet/shared/Microsoft.AspNetCore.App]
  Microsoft.AspNetCore.App 7.0.9 [/usr/lib/dotnet/shared/Microsoft.AspNetCore.App]
  Microsoft.NETCore.App 6.0.20 [/usr/lib/dotnet/shared/Microsoft.NETCore.App]
  Microsoft.NETCore.App 7.0.9 [/usr/lib/dotnet/shared/Microsoft.NETCore.App]

Other architectures found:
  None

Environment variables:
  DOTNET_ROOT       [/usr/lib/dotnet]

global.json file:
  Not found

Learn more:
  https://aka.ms/dotnet/info

Download .NET:
  https://aka.ms/dotnet/download";

        public const string WindowsNetCoreOutput = @".NET Core SDK (reflecting any global.json):

 Version:   2.1.300
 Commit:    adab45bf0c

Runtime Environment:
 OS Name:     Windows
 OS Version:  6.1.7601
 OS Platform: Windows
 RID:         win7-x64
 Base Path:   C:\Program Files\dotnet\sdk\2.1.300\

Host (useful for support):
  Version: 2.1.0
  Commit:  caa7b7e2ba

.NET Core SDKs installed:
  2.1.200 [C:\Program Files\dotnet\sdk]
  2.1.300 [C:\Program Files\dotnet\sdk]
  2.1.201 [C:\Program Files\dotnet\sdk]

.NET Core runtimes installed:
  Microsoft.AspNetCore.All 2.1.0 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.All]
  Microsoft.AspNetCore.App 2.1.0 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.NETCore.App 1.0.0 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 1.0.1 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 2.0.7 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 2.1.0 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]

To install additional .NET Core runtimes or SDKs:
  https://aka.ms/dotnet-download";

        public const string WindowsNetOutput = @".NET SDK (reflecting any global.json):
 Version:   8.0.100-preview.4.23260.5
 Commit:    2268e7b15c

Runtime Environment:
 OS Name:     Windows
 OS Version:  10.0.22621
 OS Platform: Windows
 RID:         win10-x64
 Base Path:   C:\Program Files\dotnet\sdk\8.0.100-preview.4.23260.5\

Host (useful for support):
  Version: 6.0.0
  Commit:  4822e3c3aa

.NET SDKs installed:
  6.0.412 [C:\Program Files\dotnet\sdk]
  7.0.306 [C:\Program Files\dotnet\sdk]
  7.0.400-preview.23330.10 [C:\Program Files\dotnet\sdk]
  8.0.100-preview.4.23260.5 [C:\Program Files\dotnet\sdk]

.NET runtimes installed:
  Microsoft.AspNetCore.App 6.0.19 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.AspNetCore.App 6.0.20 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.AspNetCore.App 7.0.8 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.AspNetCore.App 7.0.9 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.AspNetCore.App 8.0.0-preview.4.23260.4 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.NETCore.App 6.0.15 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 6.0.19 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 6.0.20 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 7.0.8 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 7.0.9 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 8.0.0-preview.4.23259.5 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.WindowsDesktop.App 6.0.15 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]
  Microsoft.WindowsDesktop.App 6.0.19 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]
  Microsoft.WindowsDesktop.App 6.0.20 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]
  Microsoft.WindowsDesktop.App 7.0.8 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]
  Microsoft.WindowsDesktop.App 7.0.9 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]
  Microsoft.WindowsDesktop.App 8.0.0-preview.4.23260.1 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]

To install additional .NET runtimes or SDKs:
  https://aka.ms/dotnet-download";
    }
}