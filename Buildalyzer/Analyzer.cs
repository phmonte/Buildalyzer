using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Utilities;

namespace Buildalyzer
{
    public class Analyzer
    {
        public Project Project { get; private set; }

        public Analyzer(string fileName)
        {
            ProjectCollection projectCollection = new ProjectCollection();
            projectCollection.AddToolset(new Toolset(ToolLocationHelper.CurrentToolsVersion, AppContext.BaseDirectory, projectCollection, string.Empty));
            Project = projectCollection.LoadProject(fileName);
        }
    }
}
