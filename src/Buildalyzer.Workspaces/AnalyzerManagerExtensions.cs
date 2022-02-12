using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Buildalyzer.Workspaces
{
    public static class AnalyzerManagerExtensions
    {
        /// <summary>
        /// Instantiates an empty AdhocWorkspace with logging event handlers.
        /// </summary>
        internal static AdhocWorkspace CreateWorkspace(this IAnalyzerManager manager)
        {
            ILogger logger = manager.LoggerFactory?.CreateLogger<AdhocWorkspace>();
            AdhocWorkspace workspace = new AdhocWorkspace();
            workspace.WorkspaceChanged += (sender, args) => logger?.LogDebug($"Workspace changed: {args.Kind.ToString()}{System.Environment.NewLine}");
            workspace.WorkspaceFailed += (sender, args) => logger?.LogError($"Workspace failed: {args.Diagnostic}{System.Environment.NewLine}");
            return workspace;
        }

        public static AdhocWorkspace GetWorkspace(this IAnalyzerManager manager)
        {
            if (manager is null)
            {
                throw new ArgumentNullException(nameof(manager));
            }

            // Run builds in parallel
            List<IAnalyzerResult> results = manager.Projects.Values
                .AsParallel()
                .Select(p => p.Build().FirstOrDefault())
                .Where(x => x != null)
                .ToList();

            // Add each result to a new workspace
            AdhocWorkspace workspace = manager.CreateWorkspace();

            if (!string.IsNullOrEmpty(manager.SolutionFilePath))
            {
                SolutionInfo solutionInfo = SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Default, manager.SolutionFilePath);
                workspace.AddSolution(solutionInfo);
            }

            foreach (AnalyzerResult result in results)
            {
                result.AddToWorkspace(workspace);
            }
            return workspace;
        }
    }
}