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

            ProjectId projectId = ProjectId.CreateNewId();

            // Get documents
            IEnumerable<DocumentInfo> documents = analyzer
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

            // Get metadata references
            IEnumerable<MetadataReference> metadataReferences = analyzer
                .GetReferences()
                ?.Where(File.Exists)
                .Select(x => MetadataReference.CreateFromFile(x))
                ?? (IEnumerable<MetadataReference>)Array.Empty<MetadataReference>();

            // Create the project
            string projectName = Path.GetFileNameWithoutExtension(analyzer.ProjectPath);
            string languageName = GetLanguageName(analyzer.ProjectPath);
            ProjectInfo projectInfo = ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                projectName,
                projectName,
                languageName,
                filePath: analyzer.ProjectPath,
                documents: documents,
                metadataReferences: metadataReferences);
            Project project = workspace.AddProject(projectInfo);

            return project;
        }

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
