using System.Linq;
using System.Xml.Linq;

namespace Buildalyzer
{
    public class ProjectTransformer
    {
        private bool _applyDefaultTransformations;

        public ProjectTransformer(bool applyDefaultTransformations = true)
        {
            _applyDefaultTransformations = applyDefaultTransformations;
        }

        internal void Apply(XDocument projectDocument, string targetFramework)
        {
            DefaultTransform(projectDocument, targetFramework);
            Transform(projectDocument);
        }

        public virtual void Transform(XDocument projectDocument)
        {
        }

        private void DefaultTransform(XDocument projectDocument, string targetFramework)
        {
            if (_applyDefaultTransformations)
            {
                AddSkipGetTargetFrameworkProperties(projectDocument);
                RemoveEnsureNuGetPackageBuildImports(projectDocument);
                SetTargetFramework(projectDocument, targetFramework);
            }
        }

        // Add SkipGetTargetFrameworkProperties to every ProjectReference
        public static void AddSkipGetTargetFrameworkProperties(XDocument projectDocument)
        {
            //foreach (XElement projectReference in projectDocument.GetDescendants("ProjectReference").ToArray())
            //{
            //    projectReference.AddChildElement("SkipGetTargetFrameworkProperties", "true");
            //}
        }

        // Removes all EnsureNuGetPackageBuildImports
        public static void RemoveEnsureNuGetPackageBuildImports(XDocument projectDocument)
        {
            //foreach (XElement ensureNuGetPackageBuildImports in
            //    projectDocument.GetDescendants("Target").Where(x => x.GetAttributeValue("Name") == "EnsureNuGetPackageBuildImports").ToArray())
            //{
            //    ensureNuGetPackageBuildImports.Remove();
            //}
        }

        public static void SetTargetFramework(XDocument projectDocument, string targetFramework)
        {
            //if (!string.IsNullOrEmpty(targetFramework))
            //{
            //    foreach (XElement targetFrameworkElement in
            //        projectDocument.GetDescendants("TargetFramework").Concat(projectDocument.GetDescendants("TargetFrameworks")).ToArray())
            //    {
            //        targetFrameworkElement.Name = "TargetFramework";
            //        targetFrameworkElement.Value = targetFramework;
            //    }
            //}
        }
    }
}