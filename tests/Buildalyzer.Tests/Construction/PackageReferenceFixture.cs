using System;
using System.Xml.Linq;
using Buildalyzer.Construction;
using NUnit.Framework;
using Shouldly;

namespace Buildalyzer.Tests.Construction
{
    [TestFixture]
    public class PackageReferenceFixture
    {
        [Test]
        public void PackageReferenceWithInclude_Should_ContainName()
        {
            // Given
            XElement xml = XElement.Parse(@"<PackageReference Include=""IncludedDependency"" Version=""1.0.0"" />");

            // When
            PackageReference packageReference = new PackageReference(xml);

            // Then
            packageReference.Name.ShouldBe("IncludedDependency");
        }

        [Test]
        public void PackageReferenceWithVersion_Should_ContainVersion()
        {
            // Given
            XElement xml = XElement.Parse(@"<PackageReference Include=""IncludedDependency"" Version=""1.0.0"" />");

            // When
            PackageReference packageReference = new PackageReference(xml);

            // Then
            packageReference.Version.ShouldBe("1.0.0");
        }

        [Test]
        public void PackageReferenceWithUpgrade_Should_ContainName()
        {
            // Given
            XElement xml = XElement.Parse(@"<PackageReference Update=""UpdatedDependency"" Version=""1.0.0"" />");

            // When
            PackageReference packageReference = new PackageReference(xml);

            // Then
            packageReference.Name.ShouldBe("UpdatedDependency");
        }
    }
}
