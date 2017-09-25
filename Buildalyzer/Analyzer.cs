using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Utilities;

namespace Buildalyzer
{
    public class Analyzer
    {
        public Project Project { get; }

        public Analyzer(string projectPath)
        {
            if (projectPath == null)
            {
                throw new ArgumentNullException(nameof(projectPath));
            }
            projectPath = Path.GetFullPath(projectPath); // Normalize the path
            if (!File.Exists(projectPath))
            {
                throw new ArgumentException($"The project file {projectPath} could not be found.");
            }

            // Load the project file XML
            XmlDocument projectDocument = new XmlDocument();
            projectDocument.Load(projectPath);

            // Get the paths
            IPathHelper pathHelper = PathHelperFactory.GetPathHelper(projectPath, projectDocument);

            // Get global properties
            Dictionary<string, string> globalProperties = new Dictionary<string, string>
            {
                { MsBuildProperties.SolutionDir, Path.GetDirectoryName(projectPath) },
                { MsBuildProperties.MSBuildExtensionsPath, pathHelper.ExtensionsPath },
                { MsBuildProperties.MSBuildSDKsPath, pathHelper.SDKsPath },
                { MsBuildProperties.RoslynTargetsPath, pathHelper.RoslynTargetsPath }
            };

            // Set environment variables (for some strange reason, this is required for SDK-style projects)
            Environment.SetEnvironmentVariable(
                MsBuildProperties.MSBuildExtensionsPath,
                globalProperties[MsBuildProperties.MSBuildExtensionsPath]);
            Environment.SetEnvironmentVariable(
                MsBuildProperties.MSBuildSDKsPath,
                globalProperties[MsBuildProperties.MSBuildSDKsPath]);

            // Create the project collection
            ProjectCollection projectCollection = new ProjectCollection(globalProperties);
            projectCollection.AddToolset(new Toolset(ToolLocationHelper.CurrentToolsVersion, pathHelper.ToolsPath, projectCollection, string.Empty));

            // Add the project
            XmlReader projectReader = new XmlNodeReader(projectDocument);
            Project = projectCollection.LoadProject(projectReader);
        }
    }
}
