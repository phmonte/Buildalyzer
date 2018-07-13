using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Buildalyzer.Environment;

namespace Buildalyzer
{
    /// <summary>
    /// Always resolves assembly requests to the already loaded version (if there is one).
    /// If not, attempts to find a matching assembly in certain build environment folders.
    /// </summary>
    internal class AssemblyResolver : IDisposable
    {
        private readonly BuildEnvironment _buildEnvironment;

        // Prevents recursion of OnAssemblyResolve -> LoadFrom -> OnAssemblyResolve -> etc.
        private readonly HashSet<string> _currentlyLoading = new HashSet<string>();

        public AssemblyResolver(BuildEnvironment buildEnvironment)
        {
            _buildEnvironment = buildEnvironment;
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
        }

        public void Dispose()
        {
            AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
        }

        // First try full name, then simple name, then try loading from the MSBuild directory
        private Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            string simpleName = args.Name.Split(',')[0];
            Assembly[] loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            return loadedAssemblies.FirstOrDefault(x => x.FullName == args.Name)
                ?? loadedAssemblies.FirstOrDefault(x => x.GetName().Name == simpleName)
                ?? LoadFrom(simpleName);
        }

        // Attempt to load the assembly from build enironment folders
        private Assembly LoadFrom(string simpleName)
        {
            // Don't attempt to load if we're already loading this assembly (possible recursion) or it's a resources assembly
            if(_currentlyLoading.Contains(simpleName) || simpleName.EndsWith(".resources"))
            {
                return null;
            }

            // Get all candidate paths
            string[] candidatePaths = Directory.GetFiles(Path.GetDirectoryName(_buildEnvironment.MsBuildExePath), simpleName + ".dll", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(Path.GetFullPath(Path.Combine(_buildEnvironment.SDKsPath, @"..\")), simpleName + ".dll", SearchOption.AllDirectories))
                .Concat(Directory.GetFiles(_buildEnvironment.ExtensionsPath, simpleName + ".dll", SearchOption.AllDirectories))
                .Distinct()
                .Where(x => File.Exists(x))
                .ToArray();

            // Attempt to find a matching assembly that will load
            foreach (string path in candidatePaths)
            {
                _currentlyLoading.Add(simpleName);
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
                finally
                {
                    _currentlyLoading.Remove(simpleName);
                }
            }
            return null;
        }
    }
}