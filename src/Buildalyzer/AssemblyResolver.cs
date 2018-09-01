using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Buildalyzer.Environment;
using Microsoft.Extensions.Logging;

namespace Buildalyzer
{
    /// <summary>
    /// Always resolves assembly requests to the already loaded version (if there is one).
    /// If not, attempts to find a matching assembly in certain build environment folders.
    /// </summary>
    internal class AssemblyResolver : IDisposable
    {
        private readonly BuildEnvironment _buildEnvironment;
        private readonly ILogger _logger;

        // Prevents recursion of OnAssemblyResolve -> LoadFrom -> OnAssemblyResolve -> etc.
        private readonly HashSet<string> _currentlyLoading = new HashSet<string>();

        public AssemblyResolver(BuildEnvironment buildEnvironment, ILogger logger)
        {
            _buildEnvironment = buildEnvironment;
            _logger = logger;
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
            
            logger?.LogDebug($"Currently loaded assemblies:{System.Environment.NewLine}");
            foreach(Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                logger?.LogDebug($"    {assembly.FullName} at {assembly.IsDynamic ? "dynamic" : assembly.Location}{System.Environment.NewLine}");
            }
        }

        public void Dispose()
        {
            AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
        }

        // First try full name, then simple name, then try loading from the MSBuild directory
        private Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            string[] components = args.Name.Split(',').Select(x => x.Trim()).ToArray();
            string simpleName = components[0];
            string versionString = components.FirstOrDefault(x => x.StartsWith("Version=", StringComparison.OrdinalIgnoreCase))?.Substring(8);
            if (string.IsNullOrWhiteSpace(versionString) || !Version.TryParse(versionString, out Version version))
            {
                version = null;
            }

            Assembly[] loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            Assembly result = loadedAssemblies.FirstOrDefault(x => x.FullName == args.Name)
                ?? loadedAssemblies.FirstOrDefault(x => x.GetName().Name == simpleName)
                ?? LoadFrom(simpleName, version);
            if (result == null)
            {
                _logger?.LogDebug($"Resolving assembly {args.Name} requested by {args.RequestingAssembly?.GetName()?.Name ?? "null"} failed{System.Environment.NewLine}");
            }
            else
            {
                _logger?.LogDebug($"Resolving assembly {args.Name} requested by {args.RequestingAssembly?.GetName()?.Name ?? "null"} to {result.FullName} at {result.Location ?? "null"}{System.Environment.NewLine}");
            }
            return result;
        }

        // Attempt to load the assembly from build environment folders
        private Assembly LoadFrom(string simpleName, Version version)
        {
            // Don't attempt to load if we're already loading this assembly (possible recursion) or it's a resources assembly
            if(_currentlyLoading.Contains(simpleName) || simpleName.EndsWith(".resources"))
            {
                return null;
            }

            _currentlyLoading.Add(simpleName);
            try
            {
                // Get all candidate paths, reflection-only load them (the get the version), and then take the one with the highest version
                (string, Version)[] candidates = Directory.GetFiles(Path.GetDirectoryName(_buildEnvironment.MsBuildExePath), simpleName + ".dll", SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(Path.GetFullPath(Directory.GetParent(_buildEnvironment.SDKsPath).FullName), simpleName + ".dll", SearchOption.AllDirectories))
                    .Concat(Directory.GetFiles(_buildEnvironment.ExtensionsPath, simpleName + ".dll", SearchOption.AllDirectories))
                    .Distinct()
                    .Where(x => File.Exists(x))
                    .Select(x =>
                    {
                        try
                        {
                            if (BuildEnvironment.IsRunningOnCore)
                            {
                                // Assembly reflection loading isn't supported on .NET Core
                                return (x, AssemblyName.GetAssemblyName(x).Version);
                            }
                            else
                            {
                                // On the other hand, we don't want to do .GetAssemblyName() on .NET Framework because it breaks our .Load() trick
                                return (x, Assembly.ReflectionOnlyLoadFrom(x).GetName().Version);
                            }
                        }
                        catch
                        {
                        }
                        return (x, null);
                    })
                    .Where(x => x.Item2 != null)
                    .OrderByDescending(x => x.Item2)
                    .ToArray();

                // Attempt to find an assembly with the same version
                if (version != null)
                {
                    foreach ((string, Version) candidate in candidates.Where(x => x.Item2.Equals(version)))
                    {
                        Assembly assembly = LoadFrom(candidate.Item1);
                        if (assembly != null)
                        {
                            return assembly;
                        }
                    }
                }

                // Attempt to find the highest version of any assembly that will load
                foreach ((string, Version) candidate in candidates.Where(x => !x.Item2.Equals(version)))
                {
                    Assembly assembly = LoadFrom(candidate.Item1);
                    if(assembly != null)
                    {
                        return assembly;
                    }
                }
            }
            catch
            {
            }
            finally
            {
                _currentlyLoading.Remove(simpleName);
            }
            return null;
        }

        private Assembly LoadFrom(string path)
        {
            try
            {
                if (BuildEnvironment.IsRunningOnCore)
                {
                    // The trick below doesn't work/isn't needed on .NET Core
                    return Assembly.LoadFrom(path);
                }
                else
                {
                    // This should act the same as calling .LoadFrom() but will load the assembly into the default context
                    // See https://github.com/Microsoft/MSBuildLocator/issues/8#issue-285040083
                    AssemblyName name = AssemblyName.GetAssemblyName(path);
                    return Assembly.Load(name);
                }
            }
            catch
            {
            }
            return null;
        }
    }
}