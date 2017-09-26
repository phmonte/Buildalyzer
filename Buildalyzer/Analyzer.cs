using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Xml;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Project = Microsoft.Build.Evaluation.Project;

namespace Buildalyzer
{
    public class Analyzer
    {
        public Project Project { get; }

        public IReadOnlyList<string> SourceFiles { get; }

        public IReadOnlyList<string> References { get; }

        private Analyzer(Project project, IReadOnlyList<string> sourceFiles, IReadOnlyList<string> references)
        {
            Project = project;
            SourceFiles = sourceFiles;
            References = references;
        }

        public static Analyzer Analyze(string projectPath)
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
            
            // Get the paths
            IPathHelper pathHelper = PathHelperFactory.GetPathHelper(projectPath);

            // Get global properties
            Dictionary<string, string> globalProperties = new Dictionary<string, string>
            {
                { MsBuildProperties.SolutionDir, Path.GetDirectoryName(projectPath) },
                { MsBuildProperties.MSBuildExtensionsPath, pathHelper.ExtensionsPath },
                { MsBuildProperties.MSBuildSDKsPath, pathHelper.SDKsPath },
                { MsBuildProperties.RoslynTargetsPath, pathHelper.RoslynTargetsPath },
                { MsBuildProperties.DesignTimeBuild, "true" },
                { MsBuildProperties.BuildProjectReferences, "false" },
                { MsBuildProperties.SkipCompilerExecution, "true" },
                { MsBuildProperties.ProvideCommandLineArgs, "true" }
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
            projectCollection.DefaultToolsVersion = ToolLocationHelper.CurrentToolsVersion;

            // Create a logger to capture errors
            StringBuilder logBuilder = new StringBuilder();
            ConsoleLogger logger = new ConsoleLogger(LoggerVerbosity.Normal, x => logBuilder.Append(x), null, null);
            projectCollection.RegisterLogger(logger);

            // Add the project (we can't reuse the XML because the path is used to calculate some properties)
            Project project = projectCollection.LoadProject(projectPath);
            if (project == null)
            {
                throw new ProjectException("Could not load project", logBuilder.ToString());
            }

            // Create an independent instance and build the project
            Copy copy = new Copy();
            Assembly.LoadFile(Path.GetFullPath(Path.Combine(pathHelper.RoslynTargetsPath, "Microsoft.Build.Tasks.CodeAnalysis.dll")));
            ProjectInstance projectInstance = project.CreateProjectInstance();
            if (!projectInstance.Build("Clean", new ILogger[] { logger }))
            {
                throw new ProjectException("Could not clean project", logBuilder.ToString());
            }
            if (!projectInstance.Build("Compile", new ILogger[] { logger }))
            {
                throw new ProjectException("Could not compile project", logBuilder.ToString());
            }

            // Get the command line args
            List<string> sourceFiles = new List<string>();
            List<string> references = new List<string>();
            foreach(ProjectItemInstance commandLineArg in projectInstance.Items.Where(x => x.ItemType == "CscCommandLineArgs"))
            {
                if (!commandLineArg.EvaluatedInclude.StartsWith("/"))
                {
                    sourceFiles.Add(
                        Path.GetFullPath(
                            Path.Combine(
                                Path.GetDirectoryName(projectPath),
                                commandLineArg.EvaluatedInclude)));
                }
                else if (commandLineArg.EvaluatedInclude.StartsWith("/reference:"))
                {
                    references.Add(commandLineArg.EvaluatedInclude.Substring(11));
                }
            }

            return new Analyzer(project, sourceFiles, references);
        }
    }
}
