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
                    // Use the .NET Core SDK if this is either a SDK-style project or running on .NET Core
                    if (reader.MoveToAttribute("Sdk")
                        || System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
                            .Replace(" ", "").StartsWith(".NETCore", StringComparison.OrdinalIgnoreCase))
                    {
                        return new CorePathHelper(projectPath);
                    }
                    if (reader.MoveToAttribute("ToolsVersion"))
                    {
                        return new FrameworkPathHelper();
                    }
                }
                throw new InvalidOperationException("Unrecognized project file format");
            }
        }
    }
}