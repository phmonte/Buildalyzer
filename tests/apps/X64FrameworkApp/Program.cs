using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Buildalyzer;

namespace X64FrameworkApp
{
    class Program
    {
        static void Main(string[] args)
        {
            AnalyzerManager manager = new AnalyzerManager();
            ProjectAnalyzer project = manager.GetProject(args[0]);
            foreach (string sourceFile in project.GetSourceFiles())
            {
                Console.WriteLine(sourceFile);
            }
        }
    }
}
