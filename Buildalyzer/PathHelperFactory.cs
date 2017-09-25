using System;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Utilities;

namespace Buildalyzer
{
    internal abstract class PathHelperFactory
    {
        public static IPathHelper GetPathHelper(string projectPath, XmlDocument projectDocument)
        {
            if (projectDocument.DocumentElement.HasAttribute("ToolsVersion"))
            {
                return new DotNetFrameworkPathHelper();
            }
            if (projectDocument.DocumentElement.HasAttribute("Sdk"))
            {
                return new DotNetCorePathHelper(projectPath);
            }
            throw new Exception("Unrecognized project file format");
        }
    }
}