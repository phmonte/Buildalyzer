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
        public void PackageReferenceWithIncludeShouldContainName()
        {
            // Given
            XElement xml = XElement.Parse(@"<PackageReference Include=""IncludedDependency"" Version=""1.0.0"" />");

            // When
            PackageReference packageReference = new(xml);

            // Then
            packageReference.Name.ShouldBe("IncludedDependency");
        }

        [Test]
        public void PackageReferenceWithVersionShouldContainVersion()
        {
            // Given
            XElement xml = XElement.Parse(@"<PackageReference Include=""IncludedDependency"" Version=""1.0.0"" />");

            // When
            PackageReference packageReference = new(xml);

            // Then
            packageReference.Version.ShouldBe("1.0.0");
        }

        [Test]
        public void PackageReferenceWithUpgradeShouldContainName()
        {
            // Given
            XElement xml = XElement.Parse(@"<PackageReference Update=""UpdatedDependency"" Version=""1.0.0"" />");

            // When
            PackageReference packageReference = new(xml);

            // Then
            packageReference.Name.ShouldBe("UpdatedDependency");
        }
    }
}