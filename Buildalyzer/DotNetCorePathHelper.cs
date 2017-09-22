namespace Buildalyzer
{
    internal class DotNetCorePathHelper : IPathHelper
    {
        public string ToolsPath { get; }
        public string ExtensionsPath { get; }
        public string SDKsPath { get; }
        public string RoslynTargetsPath { get; }

        public DotNetCorePathHelper()
        {
        }
    }
}