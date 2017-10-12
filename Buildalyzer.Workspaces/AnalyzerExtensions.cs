using Microsoft.CodeAnalysis;

namespace Buildalyzer.Workspaces
{
    public static class AnalyzerExtensions
    {
        public static AdhocWorkspace GetWorkspace(this AnalyzerManager manager)
        {
            AdhocWorkspace workspace = new AdhocWorkspace();
            foreach (ProjectAnalyzer project in manager.Projects.Values)
            {
                project.AddToWorkspace(workspace);
            }
            return workspace;
        }
    }
}