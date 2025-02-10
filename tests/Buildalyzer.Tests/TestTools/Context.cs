using System.Diagnostics.Contracts;
using System.IO;

namespace Buildalyzer.TestTools;

public static class Context
{
    [Pure]
    public static BuildalyzerTestContext ForProject(string path) => new(GetProjectPath(path));

    private static FileInfo GetProjectPath(string file)
    {
        var location = new FileInfo(typeof(Context).Assembly.Location).Directory!;
        return new FileInfo(Path.Combine(
            location.FullName,
            "..",
            "..",
            "..",
            "..",
            "projects",
            file));
    }
}
