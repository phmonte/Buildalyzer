using System;
using System.Linq;
using Buildalyzer;

namespace X64CoreApp
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
