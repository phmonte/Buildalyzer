using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Build.Execution;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Linq;

namespace Buildalyzer.Workspaces
{
    public static class ProjectAnalyzerExtensions
    {
        /// <summary>
        /// Gets a Roslyn workspace for the analyzed project.
        /// </summary>
        /// <param name="analyzer">The Buildalyzer project analyzer.</param>
        /// <returns>A Roslyn workspace.</returns>
        public static AdhocWorkspace GetWorkspace(this ProjectAnalyzer analyzer)
        {
            if (analyzer == null)
            {
                throw new ArgumentNullException(nameof(analyzer));
            }
            AdhocWorkspace workspace = new AdhocWorkspace();
            AddToWorkspace(analyzer, workspace);
            return workspace;
        }

        /// <summary>
        /// Adds a project to an existing Roslyn workspace.
        /// </summary>
        /// <param name="analyzer">The Buildalyzer project analyzer.</param>
        /// <param name="workspace">A Roslyn workspace.</param>
        /// <returns>The newly added Roslyn project.</returns>
        public static Project AddToWorkspace(this ProjectAnalyzer analyzer, AdhocWorkspace workspace)
        {
            if (analyzer == null)
            {
                throw new ArgumentNullException(nameof(analyzer));
            }
            if (workspace == null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            // Get or create an ID for this project
            string projectGuid = analyzer.CompiledProject?.GetPropertyValue("ProjectGuid");
            ProjectId projectId = !string.IsNullOrEmpty(projectGuid)
                && Guid.TryParse(analyzer.CompiledProject?.GetPropertyValue("ProjectGuid"), out var projectIdGuid) 
                ? ProjectId.CreateFromSerialized(projectIdGuid) 
                : ProjectId.CreateNewId();

            // Create and add the project
            ProjectInfo projectInfo = GetProjectInfo(analyzer, workspace, projectId);
            Solution solution = workspace.CurrentSolution.AddProject(projectInfo);

            // Check if this project is referenced by any other projects in the workspace
            foreach (Project existingProject in solution.Projects.ToArray())
            {
                if (!existingProject.Id.Equals(projectId)
                    && analyzer.Manager.Projects.TryGetValue(existingProject.FilePath, out ProjectAnalyzer existingAnalyzer)
                    && (existingAnalyzer.GetProjectReferences()?.Contains(analyzer.ProjectPath) ?? false))
                {
                    // Add the reference to the existing project
                    ProjectReference projectReference = new ProjectReference(projectId);
                    solution = solution.AddProjectReference(existingProject.Id, projectReference);
                }
            }

            // Apply solution changes
            if (!workspace.TryApplyChanges(solution))
            {
                throw new InvalidOperationException("Could not apply workspace solution changes");
            }

            // Find and return this project
            return workspace.CurrentSolution.GetProject(projectId);
        }

        private static ProjectInfo GetProjectInfo(ProjectAnalyzer analyzer, AdhocWorkspace workspace, ProjectId projectId)
        {
            string projectName = Path.GetFileNameWithoutExtension(analyzer.ProjectPath);
            string languageName = GetLanguageName(analyzer.ProjectPath);
            ProjectInfo projectInfo = ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                projectName,
                projectName,
                languageName,
                filePath: analyzer.ProjectPath,
                outputFilePath: analyzer.CompiledProject?.GetPropertyValue("TargetPath"),
                documents: GetDocuments(analyzer, projectId),
                projectReferences: GetExistingProjectReferences(analyzer, workspace),
                metadataReferences: GetMetadataReferences(analyzer));
            return projectInfo;
        }

        private static IEnumerable<ProjectReference> GetExistingProjectReferences(ProjectAnalyzer analyzer, AdhocWorkspace workspace) =>
            analyzer.GetProjectReferences()
                ?.Select(x => workspace.CurrentSolution.Projects.FirstOrDefault(y => y.FilePath == x))
                .Where(x => x != null)
                .Select(x => new ProjectReference(x.Id))
            ?? Array.Empty<ProjectReference>();

        private static IEnumerable<DocumentInfo> GetDocuments(ProjectAnalyzer analyzer, ProjectId projectId) => 
            analyzer
                .GetSourceFiles()
                ?.Where(File.Exists)
                .Select(x => DocumentInfo.Create(
                    DocumentId.CreateNewId(projectId),
                    Path.GetFileName(x),
                    loader: TextLoader.From(
                        TextAndVersion.Create(
                            SourceText.From(File.ReadAllText(x)), VersionStamp.Create())),
                    filePath: x))
            ?? Array.Empty<DocumentInfo>();

        private static IEnumerable<MetadataReference> GetMetadataReferences(ProjectAnalyzer analyzer) => 
            analyzer
                .GetReferences()
                ?.Where(File.Exists)
                .Select(x => MetadataReference.CreateFromFile(x))
            ?? (IEnumerable<MetadataReference>)Array.Empty<MetadataReference>();

        private static string GetLanguageName(string projectPath)
        {
            switch (Path.GetExtension(projectPath))
            {
                case ".csproj":
                    return LanguageNames.CSharp;
                case ".vbproj":
                    return LanguageNames.VisualBasic;
                default:
                    throw new InvalidOperationException("Could not determine supported language from project path");
            }
        }
    }
}
