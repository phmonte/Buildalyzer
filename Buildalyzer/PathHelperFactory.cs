using System;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Utilities;

namespace Buildalyzer
{
    internal abstract class PathHelperFactory
    {
        public static IPathHelper GetPathHelper(string projectPath)
        {
            using (XmlReader reader = XmlReader.Create(projectPath))
            {
                if (reader.MoveToContent() == XmlNodeType.Element && reader.HasAttributes)
                {
                    if (reader.MoveToAttribute("ToolsVersion"))
                    {
                        return new DotNetFrameworkPathHelper();
                    }
                    if (reader.MoveToAttribute("Sdk"))
                    {
                        return new DotNetCorePathHelper(projectPath);
                    }
                }
                throw new Exception("Unrecognized project file format");
            }
        }
    }
}