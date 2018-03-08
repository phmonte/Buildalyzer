namespace Buildalyzer.Environment
{
    public sealed class BuildEnvironment
    {
        public string ToolsPath { get; set; }

        public string ExtensionsPath { get; set; }

        public string SDKsPath { get; set; }

        public string RoslynTargetsPath { get; set; }
    }
}