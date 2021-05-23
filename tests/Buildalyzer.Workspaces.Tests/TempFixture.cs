using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using NUnit.Framework;

namespace Buildalyzer.Workspaces.Tests
{
    public class TempFixture
    {
        [Test]
        public void CustomControlCheck()
        {
            AnalyzerManager manager = new AnalyzerManager();
            IProjectAnalyzer analyzer = manager.GetProject(@"D:\ReCode\VisualRecode\Server\test\data\UpgradeWizard\WpfCustomControlLibrary1\WpfCustomControlLibrary1.csproj");

            AdhocWorkspace workspace = analyzer.GetWorkspace();
            Project project = workspace.CurrentSolution.Projects.FirstOrDefault(p => p.Name == "WpfCustomControlLibrary1");
            Assert.NotNull(project);
            Assert.IsNotEmpty(project.Documents);
        }
    }
}