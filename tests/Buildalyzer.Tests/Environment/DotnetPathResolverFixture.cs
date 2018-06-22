using Buildalyzer.Environment;
using NUnit.Framework;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Buildalyzer.Tests.Environment
{
    [TestFixture]
    public class DotnetPathResolverFixture
    {
        [Test]
        public void CanParseBasePath()
        {
            // Given
            List<string> lines = @".NET Core SDK (reflecting any global.json):
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

.NET Core runtimes installed:
  Microsoft.AspNetCore.All 2.1.0 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.All]
  Microsoft.AspNetCore.App 2.1.0 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.NETCore.App 1.0.0 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 1.0.1 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 2.0.7 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 2.1.0 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]

To install additional .NET Core runtimes or SDKs:
  https://aka.ms/dotnet-download
".Split(System.Environment.NewLine).ToList();

            // When
            string result = DotnetPathResolver.ParseBasePath(lines);

            // Then
            result.ShouldBe(@"C:\Program Files\dotnet\sdk\2.1.300\");
        }

        [Test]
        public void CanParseInstalledSdksPath()
        {
            // Given
            List<string> lines = @"Host (useful for support):
  Version: 2.1.0
  Commit:  caa7b7e2ba

.NET Core SDKs installed:
  2.1.200 [C:\Program Files\dotnet\sdk]
  2.1.300 [C:\Program Files\dotnet\sdk]

.NET Core runtimes installed:
  Microsoft.AspNetCore.All 2.1.0 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.All]
  Microsoft.AspNetCore.App 2.1.0 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.NETCore.App 1.0.0 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 1.0.1 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 2.0.7 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 2.1.0 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]

To install additional .NET Core runtimes or SDKs:
  https://aka.ms/dotnet-download
".Split(System.Environment.NewLine).ToList();

            // When
            string result = DotnetPathResolver.ParseInstalledSdksPath(lines);

            // Then
            result.ShouldBe(@"C:\Program Files\dotnet\sdk\2.1.300\");
        }
    }
}
