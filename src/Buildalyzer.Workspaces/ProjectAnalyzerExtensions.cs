using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Build.Execution;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.VisualBasic;

namespace Buildalyzer.Workspaces
{
    public static class ProjectAnalyzerExtensions
    {
        /// <summary>
        /// Gets a Roslyn workspace for the analyzed project.
        /// </summary>
        /// <param name="analyzer">The Buildalyzer project analyzer.</param>
        /// <param name="addProjectReferences"><c>true</c> to add projects to the workspace for project references that exist in the same <see cref="AnalyzerManager"/>.</param>
        /// <returns>A Roslyn workspace.</returns>
        public static AdhocWorkspace GetWorkspace(this ProjectAnalyzer analyzer, bool addProjectReferences = false)
        {
            if (analyzer == null)
            {
                throw new ArgumentNullException(nameof(analyzer));
            }
            AdhocWorkspace workspace = new AdhocWorkspace();
            AddToWorkspace(analyzer, workspace, addProjectReferences);
            return workspace;
        }

        /// <summary>
        /// Adds a project to an existing Roslyn workspace.
        /// </summary>
        /// <param name="analyzer">The Buildalyzer project analyzer.</param>
        /// <param name="workspace">A Roslyn workspace.</param>
        /// <param name="addProjectReferences"><c>true</c> to add projects to the workspace for project references that exist in the same <see cref="AnalyzerManager"/>.</param>
        /// <returns>The newly added Roslyn project.</returns>
        public static Project AddToWorkspace(this ProjectAnalyzer analyzer, Workspace workspace, bool addProjectReferences = false)
        {
            if (analyzer == null)
            {
                throw new ArgumentNullException(nameof(analyzer));
            }
            if (workspace == null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            return analyzer.Build().FirstOrDefault().AddToWorkspace(workspace, addProjectReferences);
        }
    }
}
