using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace Buildalyzer.Workspaces
{
    public static class AnalyzerManagerExtensions
    {
        public static AdhocWorkspace GetWorkspace(this IAnalyzerManager manager)
        {
            // Run builds in parallel
            List<IAnalyzerResult> results = manager.Projects.Values
                .AsParallel()
                .Select(p => p.Build().FirstOrDefault())
                .Where(x => x != null)
                .ToList();

            // Add each result to a new workspace
            AdhocWorkspace workspace = new AdhocWorkspace();
            foreach (AnalyzerResult result in results)
            {
                result.AddToWorkspace(workspace);
            }
            return workspace;
        }
    }
}