using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Utilities;

namespace Buildalyzer.Environment
{
    internal abstract class EnvironmentFactory
    {
        public static BuildEnvironment GetBuildEnvironment(string projectPath, XDocument projectDocument)
        {
            // If we're running on .NET Core, use the .NET Core SDK regardless of the project file
            if (System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
                .Replace(" ", "").StartsWith(".NETCore", StringComparison.OrdinalIgnoreCase))
            {
                return new CoreEnvironment(projectPath);
            }

            // Look at the project file to determine
            XElement projectElement = projectDocument.GetDescendants("Project").FirstOrDefault();
            if (projectElement != null)
            {
                // Does this project use the SDK?
                // Check for an SDK attribute on the project element
                // If no <Project> attribute, check for a SDK import (see https://github.com/Microsoft/msbuild/issues/1493)
                if (projectElement.GetAttributeValue("Sdk") != null
                    || projectElement.GetDescendants("Import").Any(x => x.GetAttributeValue("Sdk") != null))
                {
                    // Use the Framework tools if this project targets .NET Framework ("net" followed by a digit)
                    // https://docs.microsoft.com/en-us/dotnet/standard/frameworks
                    string targetFramework = projectElement.GetDescendants("TargetFramework").FirstOrDefault()?.Value;
                    if(targetFramework != null
                        && targetFramework.StartsWith("net", StringComparison.OrdinalIgnoreCase)
                        && targetFramework.Length > 3
                        && char.IsDigit(targetFramework[4]))
                    {
                        return new FrameworkEnvironment(projectPath, true);
                    }

                    // Otherwise use the .NET Core SDK
                    return new CoreEnvironment(projectPath);
                }

                // Use Framework tools if a ToolsVersion attribute
                if (projectElement.GetAttributeValue("ToolsVersion") != null)
                {
                    return new FrameworkEnvironment(projectPath, false);
                }
            }

            throw new InvalidOperationException("Unrecognized project file format");
        }
    }
}