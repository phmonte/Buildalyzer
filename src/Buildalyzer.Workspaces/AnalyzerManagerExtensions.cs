using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace Buildalyzer.Workspaces
{
    public static class AnalyzerManagerExtensions
    {
        public static AdhocWorkspace GetWorkspace(this AnalyzerManager manager)
        {
            AdhocWorkspace workspace = new AdhocWorkspace();

            var builds = manager.Projects.Values
                .AsParallel()
                .Select(p => p.Build())
                .ToList();
            foreach (var build in builds)
            {
                build.FirstOrDefault().AddToWorkspace(workspace);
            }
            return workspace;
        }
    }
}