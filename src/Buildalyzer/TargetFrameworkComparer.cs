using System.Collections.Generic;
using NuGet.Frameworks;

namespace Buildalyzer
{
    internal class TargetFrameworkComparer : IComparer<string>
    {
        public static readonly TargetFrameworkComparer Instance = new TargetFrameworkComparer();

        private static readonly NuGetFrameworkSorter Sorter = new NuGetFrameworkSorter();

        private TargetFrameworkComparer()
        {
        }

        public int Compare(string x, string y)
        {
            NuGetFramework xFramework = NuGetFramework.ParseFolder(x);
            NuGetFramework yFramework = NuGetFramework.ParseFolder(y);
            return Sorter.Compare(xFramework, yFramework);
        }
    }
}
