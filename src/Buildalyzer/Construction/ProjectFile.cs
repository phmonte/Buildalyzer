using Microsoft.Build.Evaluation;
using System;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Buildalyzer.Construction
{
    /// <summary>
    /// Encapsulates an MSBuild project file and provides some information about it's format.
    /// This class only parses the existing XML and does not perform any evaluation.
    /// </summary>
    public class ProjectFile
    {
        private readonly XDocument _document;
        private readonly XElement _projectElement;
        private readonly IProjectTransformer _transformer;

        private Project _project;
        
        internal ProjectFile(string path, XDocument document, IProjectTransformer transformer)
        {
            Path = path;
            Virtual = document != null;
            _document = document ?? XDocument.Load(path);
            _transformer = transformer;

            // Get the project element
            _projectElement = _document.GetDescendants(ProjectFileNames.Project).FirstOrDefault();
            if (_projectElement == null)
            {
                throw new ArgumentException("Unrecognized project file format");
            }
        }

        /// <summary>
        /// Indicates if this project file was passed in directly as XML content.
        /// </summary>
        public bool Virtual { get; }

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
        /// </remarks>
        public string[] TargetFrameworks =>
            GetTargetFrameworks(
                _projectElement.GetDescendants(ProjectFileNames.TargetFrameworks).FirstOrDefault()?.Value,
                _projectElement.GetDescendants(ProjectFileNames.TargetFramework).FirstOrDefault()?.Value,
                _projectElement.GetDescendants(ProjectFileNames.TargetFrameworkVersion).FirstOrDefault()?.Value);
        
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
        /// Whether the project file is portable.
        /// </summary>
        /// <remarks>
        /// Checks for an <c>Import</c> element with a <c>Project</c> attribute of <c>Microsoft.Portable.CSharp.targets</c>.
        /// </remarks>
        public bool IsPortable =>
            _projectElement.GetDescendants(ProjectFileNames.Import).Any(x => x.GetAttributeValue(ProjectFileNames.Project).EndsWith("Microsoft.Portable.CSharp.targets"));

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
        /// Gets the <c>ToolsVersion</c> attribute of the <c>Project</c> element (or <c>null</c> if there isn't one).
        /// </summary>
        public string ToolsVersion => _projectElement.GetAttributeValue(ProjectFileNames.ToolsVersion);

        internal XmlReader CreateReader()
        {
            XDocument document = new XDocument(_document);
            _transformer?.Transform(document);
            return document.CreateReader();
        }

        internal static string[] GetTargetFrameworks(string targetFrameworks, string targetFramework, string targetFrameworkVersion)
        {
            if (!string.IsNullOrEmpty(targetFrameworks))
            {
                return targetFrameworks.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();
            }
            if (!string.IsNullOrEmpty(targetFramework))
            {
                return new[] { targetFramework.Trim() };
            }
            if (!string.IsNullOrEmpty(targetFrameworkVersion))
            {
                return new[] { "net" + new string(targetFrameworkVersion.Where(x => char.IsDigit(x)).ToArray()) };
            }
            return Array.Empty<string>();
        }
    }
}