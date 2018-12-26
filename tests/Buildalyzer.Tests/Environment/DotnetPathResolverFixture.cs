using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Buildalyzer.Environment;
using NUnit.Framework;
using Shouldly;

namespace Buildalyzer.Tests.Environment
{
    [TestFixture]
    public class DotnetPathResolverFixture
    {
        public const string LinuxOutput = @".NET Core SDK (reflecting any global.json):
Version:   2.1.401
Commit:    91b1c13032

Runtime Environment:
  OS Name:     ubuntu
  OS Version:  16.04
  OS Platform: Linux
  RID:         ubuntu.16.04-x64
  Base Path:   /usr/share/dotnet/sdk/2.1.401/

Host (useful for support):
  Version: 2.1.3
  Commit:  124038c13e

.NET Core SDKs installed:
  1.1.5 [/usr/share/dotnet/sdk]
  1.1.6 [/usr/share/dotnet/sdk]
  2.1.201 [/usr/share/dotnet/sdk]

.NET Core runtimes installed:
  Microsoft.AspNetCore.All 2.1.3 [/usr/share/dotnet/shared/Microsoft.AspNetCore.All]
  Microsoft.NETCore.App 2.1.3 [/usr/share/dotnet/shared/Microsoft.NETCore.App]

To install additional .NET Core runtimes or SDKs:
  https://aka.ms/dotnet-download";

        public const string WindowsOutput = @".NET Core SDK (reflecting any global.json):
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

        [TestCase(WindowsOutput, @"C:\Program Files\dotnet\sdk\2.1.300\")]
        [TestCase(LinuxOutput, "/usr/share/dotnet/sdk/2.1.401/")]
        public void CanParseBasePath(string output, string basePath)
        {
            // Given
            List<string> lines = output.Split("\n").Select(x => x.Trim('\r')).ToList();

            // When
            string result = DotnetPathResolver.ParseBasePath(lines);

            // Then
            result.ShouldBe(basePath);
        }

        [TestCase(WindowsOutput, @"C:\Program Files\dotnet\sdk\2.1.201\")]
        [TestCase(LinuxOutput, "/usr/share/dotnet/sdk/2.1.201/")]
        public void CanParseInstalledSdksPath(string output, string sdksPath)
        {
            // Given
            List<string> lines = output.Split("\n").Select(x => x.Trim('\r')).ToList();

            // When
            string result = DotnetPathResolver.ParseInstalledSdksPath(lines);

            // Then
            AnalyzerManager.NormalizePath(result).ShouldBe(AnalyzerManager.NormalizePath(sdksPath));
        }
    }
}
