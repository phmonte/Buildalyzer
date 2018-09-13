using Microsoft.Build.Logging.StructuredLogger;
using System;

namespace Buildalyzer
{
    internal static class ObjectModelExtensions
    {
        public static string GetProperty(this Project project, string name) => project?.GetValue("Properties", name);

        public static string GetValue(this TreeNode treeNode, string folder, string name) =>
            treeNode
                ?.FindChild<Folder>(folder)
                ?.GetValue(name);

        public static string GetValue(this TreeNode treeNode, string name) =>
            treeNode
               ?.FindChild<NameValueNode>(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
               ?.Value;

    }
}