namespace Buildalyzer.Caching
{
    internal static class DotnetInfoCache
    {
        private static Dictionary<string, List<string>> cache = new Dictionary<string, List<string>>();

        [Pure]
        public static List<string> GetCache(string path)
        {
            return cache.FirstOrDefault(x => x.Key == path).Value;
        }

        [Pure]
        public static Dictionary<string, List<string>> AddCache(string path, List<string> dotnetInfo)
        {
            cache.Add(path, dotnetInfo);
            return cache;
        }
    }
}
