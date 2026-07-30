// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NUnit.Framework;
using Schema.Utility;

namespace Schema.UnitTests.Utility
{
    [TestFixture]
    public class ModelXmlSerializerTests
    {
        [Test]
        public void ToIngestXml_WrapsArrayInRoot_WithRepeatedItemElement()
        {
            const string json = "[{\"Schema\":\"dbo\",\"Name\":\"A\"},{\"Schema\":\"dbo\",\"Name\":\"B\"}]";

            var xml = ModelXmlSerializer.ToIngestXml(json, "Tables", "Table");

            Assert.That(xml, Is.EqualTo(
                "<Tables><Table><Schema>dbo</Schema><Name>A</Name></Table>" +
                "<Table><Schema>dbo</Schema><Name>B</Name></Table></Tables>"));
        }

        [Test]
        public void ToIngestXml_EntityEncodes_SpecialCharacters()
        {
            const string json = "[{\"Name\":\"Fo&o\",\"Expr\":\"X<Y\"}]";

            var xml = ModelXmlSerializer.ToIngestXml(json, "Tables", "Table");

            Assert.That(xml, Does.Contain("<Name>Fo&amp;o</Name>"));
            Assert.That(xml, Does.Contain("<Expr>X&lt;Y</Expr>"));
        }

        [Test]
        public void ToIngestXml_RendersJsonNull_AsEmptyElement()
        {
            const string json = "[{\"Name\":\"A\",\"OldName\":null}]";

            var xml = ModelXmlSerializer.ToIngestXml(json, "Tables", "Table");

            // An empty element carries no text node, so .value('(OldName/text())[1]', ...) shreds to NULL.
            Assert.That(xml, Does.Contain("<OldName />"));
        }

        [Test]
        public void ToIngestXml_NestedArray_BecomesRepeatedElementNamedByProperty()
        {
            const string json = "[{\"Name\":\"A\",\"Columns\":[{\"Name\":\"Id\"},{\"Name\":\"X\"}]}]";

            var xml = ModelXmlSerializer.ToIngestXml(json, "Tables", "Table");

            // Each column is a repeated <Columns> element with its fields inline (Json.NET has no singular name).
            Assert.That(xml, Does.Contain("<Columns><Name>Id</Name></Columns><Columns><Name>X</Name></Columns>"));
        }

        [Test]
        public void ToIngestXml_EmptyNestedArray_IsOmitted()
        {
            const string json = "[{\"Name\":\"A\",\"Indexes\":[]}]";

            var xml = ModelXmlSerializer.ToIngestXml(json, "Tables", "Table");

            Assert.That(xml, Does.Not.Contain("Indexes"));
        }

        [Test]
        public void ToIngestXml_RendersBooleans_AsLowercaseText()
        {
            // The shred reads these as text and CASEs on 'true'/'false' (a direct BIT cast of 'true' errors).
            const string json = "[{\"Name\":\"A\",\"Nullable\":true,\"Sparse\":false}]";

            var xml = ModelXmlSerializer.ToIngestXml(json, "Tables", "Table");

            Assert.That(xml, Does.Contain("<Nullable>true</Nullable>"));
            Assert.That(xml, Does.Contain("<Sparse>false</Sparse>"));
        }

        [Test]
        public void ToIngestXml_PreservesUnicode()
        {
            const string json = "[{\"Name\":\"Ünïcödé\"}]";

            var xml = ModelXmlSerializer.ToIngestXml(json, "Tables", "Table");

            Assert.That(xml, Does.Contain("<Name>Ünïcödé</Name>"));
        }
    }
}
