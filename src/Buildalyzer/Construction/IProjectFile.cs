using System.Collections.Generic;

namespace Buildalyzer.Construction
{
    public interface IProjectFile
    {
        bool ContainsPackageReferences { get; }
        bool IsMultiTargeted { get; }
        IReadOnlyList<IPackageReference> PackageReferences { get; }
        string Path { get; }
        bool RequiresNetFramework { get; }
        string[] TargetFrameworks { get; }
        string ToolsVersion { get; }
        bool UsesSdk { get; }
    }
}