using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Execution;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.VisualBasic;

namespace Buildalyzer.Workspaces
{
    public static class ProjectAnalyzerExtensions
    {
        /// <summary>
        /// Gets a Roslyn workspace for the analyzed project. Note that this will rebuild the project. Use an <see cref="AnalyzerResult"/> instead if you already have one available.
        /// </summary>
        /// <param name="analyzer">The Buildalyzer project analyzer.</param>
        /// <param name="addProjectReferences">
        /// <c>true</c> to add projects to the workspace for project references that exist in the same <see cref="AnalyzerManager"/>.
        /// If <c>true</c> this will trigger (re)building all referenced projects. Directly add <see cref="AnalyzerResult"/> instances instead if you already have them available.
        /// </param>
        /// <returns>A Roslyn workspace.</returns>
        public static AdhocWorkspace GetWorkspace(this IProjectAnalyzer analyzer, bool addProjectReferences = false)
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
        /// Adds a project to an existing Roslyn workspace. Note that this will rebuild the project. Use an <see cref="AnalyzerResult"/> instead if you already have one available.
        /// </summary>
        /// <param name="analyzer">The Buildalyzer project analyzer.</param>
        /// <param name="workspace">A Roslyn workspace.</param>
        /// <param name="addProjectReferences">
        /// <c>true</c> to add projects to the workspace for project references that exist in the same <see cref="AnalyzerManager"/>.
        /// If <c>true</c> this will trigger (re)building all referenced projects. Directly add <see cref="AnalyzerResult"/> instances instead if you already have them available.
        /// </param>
        /// <returns>The newly added Roslyn project.</returns>
        public static Project AddToWorkspace(this IProjectAnalyzer analyzer, Workspace workspace, bool addProjectReferences = false)
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
