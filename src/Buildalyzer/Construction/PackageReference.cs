using System;
using System.Xml.Linq;

namespace Buildalyzer.Construction
{
    public class PackageReference : IPackageReference
    {
        public string Name { get; }
        public string Version { get; }

        internal PackageReference(XElement packageReferenceElement)
        {
            this.Name = packageReferenceElement.GetAttributeValue("Include");
            this.Version = packageReferenceElement.GetAttributeValue("Version");
        }
    }
}
