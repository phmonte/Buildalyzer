using System;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Buildalyzer
{
    /// <summary>
    /// Encapsulates an MSBuild project file and provides some information about it's format.
    /// </summary>
    public class ProjectFile
    {
        private readonly XDocument _document;
        private readonly XElement _projectElement;
        private readonly ProjectTransformer _transformer;

        /// <summary>
        /// Indicates if this project file was passed in directly as XML content.
        /// </summary>
        public bool Virtual { get; }

        /// <summary>
        /// The full path to the project file.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// The originally specified target frameworks.
        /// </summary>
        public string[] TargetFrameworks { get; }

        internal ProjectFile(string path, XDocument document, ProjectTransformer transformer)
        {
            Path = path;
            Virtual = document != null;
            _document = document ?? XDocument.Load(path);
            _transformer = transformer;

            // Get the project element
            _projectElement = _document.GetDescendants("Project").FirstOrDefault();
            if (_projectElement == null)
            {
                throw new ArgumentException("Unrecognized project file format");
            }

            // Get the target frameworks
            TargetFrameworks = _projectElement.GetDescendants("TargetFramework").Select(x => x.Value.Trim()).Concat(
                _projectElement.GetDescendants("TargetFrameworks").SelectMany(x => x.Value.Split(';').Select(y => y.Trim())))
                .ToArray();
        }

        /// <summary>
        /// Whether the project file uses an SDK.
        /// </summary>
        /// <remarks>
        /// Checks for an <c>Sdk</c> attribute on the <c>Project</c> element. If one can't be found,
        /// also checks for <c>Import</c> elements with an <c>Sdk</c> attribute (see https://github.com/Microsoft/msbuild/issues/1493).
        /// </remarks>
        public bool UsesSdk =>
            _projectElement.GetAttributeValue("Sdk") != null || _projectElement.GetDescendants("Import").Any(x => x.GetAttributeValue("Sdk") != null);

        /// <summary>
        /// Whether the project file is portable.
        /// </summary>
        /// <remarks>
        /// Checks for an <c>Import</c> element with a <c>Project</c> attribute of <c>Microsoft.Portable.CSharp.targets</c>.
        /// </remarks>
        public bool IsPortable =>
            _projectElement.GetDescendants("Import").Any(x => x.GetAttributeValue("Project").EndsWith("Microsoft.Portable.CSharp.targets"));

        /// <summary>
        /// Whether the project file is multi-targeted.
        /// </summary>
        /// <remarks>
        /// Checks for an <c>TargetFrameworks</c> element.
        /// </remarks>
        public bool IsMultiTargeted => _projectElement.GetDescendants("TargetFrameworks").Any();

        /// <summary>
        /// Whether the project file contains <c>PackageReference</c> items.
        /// </summary>
        public bool ContainsPackageReferences => _projectElement.GetDescendants("PackageReference").Any();

        /// <summary>
        /// Gets the <c>ToolsVersion</c> attribute of the <c>Project</c> element (or <c>null</c> if there isn't one).
        /// </summary>
        public string ToolsVersion => _projectElement.GetAttributeValue("ToolsVersion");

        internal XmlReader CreateReader(string targetFramework)
        {
            XDocument document = new XDocument(_document);
            _transformer.Apply(document, targetFramework);
            return _document.CreateReader();
        }
    }
}