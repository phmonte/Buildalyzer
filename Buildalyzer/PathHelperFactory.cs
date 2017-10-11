using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Utilities;

namespace Buildalyzer
{
    internal abstract class PathHelperFactory
    {
        public static IPathHelper GetPathHelper(string projectPath, XDocument projectDocument)
        {
            // If we're running on .NET Core, use the .NET Core SDK regardless of the project file
            if (System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
                .Replace(" ", "").StartsWith(".NETCore", StringComparison.OrdinalIgnoreCase))
            {
                return new CorePathHelper(projectPath);
            }

            // Look at the project file to determine
            XElement projectElement = projectDocument.GetDescendants("Project").FirstOrDefault();
            if (projectElement != null)
            {
                // Use .NET Core SDK if a SDK attribute
                if (projectElement.GetAttributeValue("Sdk") != null)
                {
                    return new CorePathHelper(projectPath);
                }

                // Use Framework tools if a ToolsVersion attribute
                if (projectElement.GetAttributeValue("ToolsVersion") != null)
                {
                    return new FrameworkPathHelper();
                }

                // If no <Project> attribute, check for a SDK import
                // See https://github.com/Microsoft/msbuild/issues/1493
                if (projectElement.GetDescendants("Import").Any(x => x.GetAttributeValue("Sdk") != null))
                {
                    return new CorePathHelper(projectPath);
                }
            }

            throw new InvalidOperationException("Unrecognized project file format");
        }
    }
}