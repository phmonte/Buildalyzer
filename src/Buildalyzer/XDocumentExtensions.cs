using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Buildalyzer
{
    internal static class XDocumentExtensions
    {
        public static IEnumerable<XElement> GetDescendants(this XDocument document, string name) =>
            document.Descendants().Where(x => string.Equals(x.Name.LocalName, name, StringComparison.OrdinalIgnoreCase));

        public static IEnumerable<XElement> GetDescendants(this XElement element, string name) =>
            element.Descendants().Where(x => string.Equals(x.Name.LocalName, name, StringComparison.OrdinalIgnoreCase));

        public static string GetAttributeValue(this XElement element, string name) =>
            element.Attributes().FirstOrDefault(x => string.Equals(x.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))?.Value;

        // Adds a child element with the same namespace as the parent
        public static void AddChildElement(this XElement element, string name, string value) =>
            element.Add(new XElement(XName.Get(name, element.Name.NamespaceName), value));
    }
}