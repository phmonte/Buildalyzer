using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Evaluation;

namespace Buildalyzer.Construction
{
    /// <summary>
    /// Encapsulates an MSBuild project file and provides some information about it's format.
    /// This class only parses the existing XML and does not perform any evaluation.
    /// </summary>
    public class ProjectFile : IProjectFile
    {
        /// <summary>
        /// These imports are known to require a .NET Framework host and build tools.
        /// </summary>
        public static readonly string[] ImportsThatRequireNetFramework = new string[]
        {
            "Microsoft.Portable.CSharp.targets",
            "Microsoft.Windows.UI.Xaml.CSharp.targets"
        };

        private readonly XDocument _document;
        private readonly XElement _projectElement;

        private string[] _targetFrameworks = null;

        // The project file path should already be normalized
        internal ProjectFile(string path)
        {
            Path = path;
            _document = XDocument.Load(path);

            // Get the project element
            _projectElement = _document.GetDescendants(ProjectFileNames.Project).FirstOrDefault();
            if (_projectElement == null)
            {
                throw new ArgumentException("Unrecognized project file format");
            }
        }

        /// <summary>
        /// The full path to the project file.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// The target framework(s) in the project file.
        /// </summary>
        /// <remarks>
        /// This does not perform evaluation of the project file, only parsing.
        /// If TargetFramework or TargetFrameworks contains a property that
        /// needs to be evaluated, this will contain the pre-evaluated value(s).
        /// Try to find a TargetFrameworkIdentifier in the same PropertyGroup
        /// and if no TargetFrameworkIdentifier was found, assume ".NETFramework".
        /// </remarks>
        public string[] TargetFrameworks => _targetFrameworks
            ?? (_targetFrameworks = GetTargetFrameworks(
                _projectElement.GetDescendants(ProjectFileNames.TargetFrameworks).Select(x => x.Value),
                _projectElement.GetDescendants(ProjectFileNames.TargetFramework).Select(x => x.Value),
                _projectElement.GetDescendants(ProjectFileNames.TargetFrameworkVersion)
                    .Select(x => (x.Parent.GetDescendants(ProjectFileNames.TargetFrameworkIdentifier).FirstOrDefault()?.Value ?? ".NETFramework", x.Value))));

        /// <summary>
        /// Whether the project file uses an SDK.
        /// </summary>
        /// <remarks>
        /// Checks for an <c>Sdk</c> attribute on the <c>Project</c> element. If one can't be found,
        /// also checks for <c>Import</c> elements with an <c>Sdk</c> attribute (see https://github.com/Microsoft/msbuild/issues/1493).
        /// </remarks>
        public bool UsesSdk =>
            _projectElement.GetAttributeValue(ProjectFileNames.Sdk) != null
                || _projectElement.GetDescendants(ProjectFileNames.Import).Any(x => x.GetAttributeValue(ProjectFileNames.Sdk) != null);

        /// <summary>
        /// Whether the project file requires a .NET Framework host and build tools to build.
        /// </summary>
        /// <remarks>
        /// Checks for an <c>Import</c> element with a <c>Project</c> attribute ending with one of the targets in <see cref="ImportsThatRequireNetFramework"/>.
        /// Also looks for a <c>LanguageTargets</c> ending with one of the targets in <see cref="ImportsThatRequireNetFramework"/>.
        /// Projects that use these targets are known not to build under a .NET Core host or build tools.
        /// Also checks for a <c>ToolsVersion</c> attribute and uses the .NET Framework if one is found.
        /// </remarks>
        public bool RequiresNetFramework =>
            _projectElement.GetDescendants(ProjectFileNames.Import).Any(x => ImportsThatRequireNetFramework.Any(i => x.GetAttributeValue(ProjectFileNames.Project).EndsWith(i, StringComparison.OrdinalIgnoreCase)))
            || _projectElement.GetDescendants(ProjectFileNames.LanguageTargets).Any(x => ImportsThatRequireNetFramework.Any(i => x.Value.EndsWith(i, StringComparison.OrdinalIgnoreCase)))
            || ToolsVersion != null;

        /// <summary>
        /// Whether the project file is multi-targeted.
        /// </summary>
        /// <remarks>
        /// Checks for an <c>TargetFrameworks</c> element.
        /// </remarks>
        public bool IsMultiTargeted => _projectElement.GetDescendants(ProjectFileNames.TargetFrameworks).Any();

        /// <summary>
        /// Whether the project file contains <c>PackageReference</c> items.
        /// </summary>
        public bool ContainsPackageReferences => _projectElement.GetDescendants(ProjectFileNames.PackageReference).Any();

        /// <summary>
        /// The list of <c>PackageReference</c> items in the project file.
        /// </summary>
        public IReadOnlyList<IPackageReference> PackageReferences => _projectElement.GetDescendants(ProjectFileNames.PackageReference).Select(s => new PackageReference(s)).ToList();

        /// <summary>
        /// Gets the <c>ToolsVersion</c> attribute of the <c>Project</c> element (or <c>null</c> if there isn't one).
        /// </summary>
        public string ToolsVersion => _projectElement.GetAttributeValue(ProjectFileNames.ToolsVersion);

        internal static string[] GetTargetFrameworks(
            IEnumerable<string> targetFrameworksValues,
            IEnumerable<string> targetFrameworkValues,
            IEnumerable<(string, string)> targetFrameworkIdentifierAndVersionValues)
        {
            // Use TargetFrameworks and/or TargetFramework if either were found
            IEnumerable<string> allTargetFrameworks = null;
            if (targetFrameworksValues != null)
            {
                allTargetFrameworks = targetFrameworksValues
                    .Where(x => x != null)
                    .SelectMany(x => x.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(v => v.Trim()));
            }
            if (targetFrameworkValues != null)
            {
                allTargetFrameworks = allTargetFrameworks == null
                    ? targetFrameworkValues.Where(x => x != null).Select(x => x.Trim())
                    : allTargetFrameworks.Concat(targetFrameworkValues.Where(x => x != null).Select(x => x.Trim()));
            }
            if (allTargetFrameworks != null)
            {
                string[] distinctTargetFrameworks = allTargetFrameworks.Distinct().ToArray();
                if (distinctTargetFrameworks.Length > 0)
                {
                    // Only return if we actually found any
                    return distinctTargetFrameworks;
                }
            }

            // Otherwise, try to find a TargetFrameworkIdentifier and/or TargetFrameworkVersion and puzzle it out
            // This is really hacky, would be great to find an official mapping
            // This is also unreliable because a particular TargetFrameworkIdentifier could result from different TargetFramework
            // For example, both "win" and "uap" TargetFramework map back to ".NETCore" TargetFrameworkIdentifier
            return targetFrameworkIdentifierAndVersionValues?
                .Where(value => value.Item1 != null && value.Item2 != null)
                .Select(value =>
                {
                    // If we have a mapping, use it
                    if (TargetFrameworkIdentifierToTargetFramework.TryGetValue(value.Item1, out (string, bool) targetFramework))
                    {
                        // Append the TargetFrameworkVersion, stripping non-digits (this probably isn't correct in some cases)
                        return targetFramework.Item1 + new string(value.Item2.Where(x => char.IsDigit(x) || (targetFramework.Item2 && x == '.')).ToArray());
                    }

                    // Otherwise ¯\_(ツ)_/¯
                    return null;
                })
                .Where(x => x != null).ToArray()
                    ?? Array.Empty<string>();
        }

        // Map from TargetFrameworkIdentifier back to a TargetFramework
        // Partly from https://github.com/onovotny/sdk/blob/83d93a58c0955386218d536580eac2ab1582b397/src/Tasks/Microsoft.NET.Build.Tasks/build/Microsoft.NET.TargetFrameworkInference.targets
        // See also https://blog.stephencleary.com/2012/05/framework-profiles-in-net.html
        // Can't handle ".NETPortable" because those split out as complex "portable-" TargetFramework
        // Value = (TargetFramework, preserve dots in version)
        private static readonly Dictionary<string, (string, bool)> TargetFrameworkIdentifierToTargetFramework = new Dictionary<string, (string, bool)>
        {
            { ".NETStandard", ("netstandard", true) },
            { ".NETCoreApp", ("netcoreapp", true) },
            { ".NETFramework", ("net", false) },
            { ".NETCore", ("uap", true) },
            { "WindowsPhoneApp", ("wpa", false) },
            { "WindowsPhone", ("wp", false) },
            { "Xamarin.iOS", ("xamarinios", false) },
            { "MonoAndroid", ("monoandroid", false) },
            { "Xamarin.TVOS", ("xamarintvos", false) },
            { "Xamarin.WatchOS", ("xamarinwatchos", false) },
            { "Xamarin.Mac", ("xamarinmac", false) }
        };
    }
}