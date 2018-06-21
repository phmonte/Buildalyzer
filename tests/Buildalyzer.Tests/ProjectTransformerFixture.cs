using Buildalyzer;
using NUnit.Framework;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace BuildalyzerTests
{
    [TestFixture]
    public class ProjectTransformerFixture
    {
        [Test]
        public void AddsSkipGetTargetFrameworkProperties()
        {
            // Given
            XDocument projectDocument = XDocument.Parse(@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>netcoreapp2.1</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Microsoft.Extensions.Logging"" Version=""2.0.0"" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include=""..\..\src\Buildalyzer\Buildalyzer.csproj"" />
  </ItemGroup>
</Project>");

            // When
            ProjectTransformer.AddSkipGetTargetFrameworkProperties(projectDocument);

            // Then
            projectDocument.ToString().ShouldBe(@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>netcoreapp2.1</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Microsoft.Extensions.Logging"" Version=""2.0.0"" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include=""..\..\src\Buildalyzer\Buildalyzer.csproj"">
      <SkipGetTargetFrameworkProperties>true</SkipGetTargetFrameworkProperties>
    </ProjectReference>
  </ItemGroup>
</Project>");
        }

        [Test]
        public void RemovesEnsureNuGetPackageBuildImports()
        {
            // Given
            XDocument projectDocument = XDocument.Parse(@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>netcoreapp2.1</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Microsoft.Extensions.Logging"" Version=""2.0.0"" />
  </ItemGroup>
  <Target Name=""EnsureNuGetPackageBuildImports"" BeforeTargets=""PrepareForBuild"">
    <PropertyGroup>
      <ErrorText>This project references NuGet package(s) that are missing on this computer. Use NuGet Package Restore to download them.For more information, see http://go.microsoft.com/fwlink/?LinkID=322105. The missing file is {0}.</ErrorText>
    </PropertyGroup>
    <Error Condition=""!Exists('..\packages\Xamarin.Forms.2.3.0.107\build\portable-win+net45+wp80+win81+wpa81+MonoAndroid10+MonoTouch10+Xamarin.iOS10\Xamarin.Forms.targets')"" Text = ""$([System.String]::Format('$(ErrorText)', '..\packages\Xamarin.Forms.2.3.0.107\build\portable-win+net45+wp80+win81+wpa81+MonoAndroid10+MonoTouch10+Xamarin.iOS10\Xamarin.Forms.targets'))"" />
  </Target>
</Project>");

            // When
            ProjectTransformer.RemoveEnsureNuGetPackageBuildImports(projectDocument);

            // Then
            projectDocument.ToString().ShouldBe(@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>netcoreapp2.1</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Microsoft.Extensions.Logging"" Version=""2.0.0"" />
  </ItemGroup>
</Project>");
        }

        [Test]
        public void RemovesMultipleTargets()
        {
            // Given
            XDocument projectDocument = XDocument.Parse(@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFrameworks>netstandard1.1;net45</TargetFrameworks>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Microsoft.Extensions.Logging"" Version=""2.0.0"" />
  </ItemGroup>
</Project>");

            // When
            ProjectTransformer.RemoveMultipleTargets(projectDocument);

            // Then
            projectDocument.ToString().ShouldBe(@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>netstandard1.1</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Microsoft.Extensions.Logging"" Version=""2.0.0"" />
  </ItemGroup>
</Project>");
        }
    }
}
