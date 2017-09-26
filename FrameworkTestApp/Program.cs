using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Buildalyzer;

namespace FrameworkTestApp
{
    class Program
    {
        static void Main(string[] args)
        {
            Analyzer analyzer = Analyzer.Analyze(@"E:\Code\FluentFrontend\src\adapters\FluentFrontend.Adapter.Mvc\FluentFrontend.Adapter.Mvc.csproj");
            //Analyzer analyzer = Analyzer.Analyze(@"E:\Code\FluentFrontend\src\FluentFrontend\FluentFrontend.csproj");
        }
    }
}
