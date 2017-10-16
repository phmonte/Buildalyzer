using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Buildalyzer;

namespace X64FrameworkApp
{
    class Program
    {
        static void Main(string[] args)
        {
            StringBuilder log = new StringBuilder();
            AnalyzerManager manager = new AnalyzerManager(log);
            ProjectAnalyzer project = manager.GetProject(args[0]);
            IReadOnlyList<string> sourceFiles = project.GetSourceFiles();
            if(sourceFiles == null)
            {
                Console.Error.Write(log);
                return;
            }
            foreach (string sourceFile in sourceFiles)
            {
                Console.WriteLine(sourceFile);
            }
        }
    }
}
