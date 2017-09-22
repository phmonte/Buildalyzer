namespace Buildalyzer
{
    internal interface IPathHelper
    {
        string ToolsPath { get; }
        string ExtensionsPath { get; }
        string SDKsPath { get; }
        string RoslynTargetsPath { get; }
    }
}