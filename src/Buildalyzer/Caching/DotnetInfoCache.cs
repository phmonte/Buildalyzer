using Microsoft.Build.Framework;

namespace Buildalyzer.Caching
{
    internal static class DotnetInfoCache
    {
        private static readonly Dictionary<string, IReadOnlyCollection<string>> Cache = new Dictionary<string, IReadOnlyCollection<string>>();

        public static IReadOnlyCollection<string> GetCache(string path)
        {
            return Cache.TryGetValue(path, out IReadOnlyCollection<string> values) ? values : null;
        }

        public static void AddCache(string path, IReadOnlyCollection<string> dotnetInfo)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("The path cannot be null or empty.");
            }

            if (dotnetInfo == null)
            {
                throw new ArgumentNullException(nameof(dotnetInfo), "Dotnet info information cannot be null.");
            }

            Cache[path] = dotnetInfo;
        }
    }
}
