// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.Domain;
using Schema.Domain.SqlServer;

namespace Schema.UnitTests.Domain.SqlServer
{
    [TestFixture]
    public class SqlServerForeignKeyDefaultingTests
    {
        [Test]
        public void BareConstructor_RelatedTableSchema_IsNull()
        {
            var fk = new SqlServerForeignKey();
            Assert.That(fk.RelatedTableSchema, Is.Null);
        }

        [Test]
        public void RegularTemplate_OmittedRelatedTableSchema_ResolvesToDbo()
        {
            var fk = new SqlServerForeignKey { Name = "FK_X" };

            SchemaDefaultResolver.Resolve(fk, isSchemaTemplate: false, Platform.SqlServer);

            Assert.That(fk.RelatedTableSchema, Is.EqualTo("dbo"));
        }

        [Test]
        public void RegularTemplate_EmptyRelatedTableSchema_ResolvesToDbo()
        {
            var fk = new SqlServerForeignKey { Name = "FK_X", RelatedTableSchema = "" };

            SchemaDefaultResolver.Resolve(fk, isSchemaTemplate: false, Platform.SqlServer);

            Assert.That(fk.RelatedTableSchema, Is.EqualTo("dbo"));
        }

        [Test]
        public void RegularTemplate_ExplicitLiteralRelatedTableSchema_Preserved()
        {
            var fk = new SqlServerForeignKey { Name = "FK_X", RelatedTableSchema = "Sales" };

            SchemaDefaultResolver.Resolve(fk, isSchemaTemplate: false, Platform.SqlServer);

            Assert.That(fk.RelatedTableSchema, Is.EqualTo("Sales"));
        }

        [Test]
        public void SchemaTemplate_OmittedRelatedTableSchema_ResolvesToSchemaNameToken()
        {
            var fk = new SqlServerForeignKey { Name = "FK_X" };

            SchemaDefaultResolver.Resolve(fk, isSchemaTemplate: true, Platform.SqlServer);

            Assert.That(fk.RelatedTableSchema, Is.EqualTo("{{SchemaName}}"));
        }

        [Test]
        public void SchemaTemplate_EmptyRelatedTableSchema_ResolvesToSchemaNameToken()
        {
            var fk = new SqlServerForeignKey { Name = "FK_X", RelatedTableSchema = "" };

            SchemaDefaultResolver.Resolve(fk, isSchemaTemplate: true, Platform.SqlServer);

            Assert.That(fk.RelatedTableSchema, Is.EqualTo("{{SchemaName}}"));
        }

        [Test]
        public void SchemaTemplate_LiteralSchemaNameTokenAccepted()
        {
            var fk = new SqlServerForeignKey { Name = "FK_X", RelatedTableSchema = "{{SchemaName}}" };

            SchemaDefaultResolver.Resolve(fk, isSchemaTemplate: true, Platform.SqlServer);

            Assert.That(fk.RelatedTableSchema, Is.EqualTo("{{SchemaName}}"));
        }

        [Test]
        public void SchemaTemplate_HardLiteralRelatedTableSchema_Preserved_ForCrossSchemaFk()
        {
            // Per design §3.3.5 / §3.4: explicit literal RelatedTableSchema on FKs is allowed
            // in schema templates (cross-schema reference, e.g. tenant table FK to dbo.Countries).
            var fk = new SqlServerForeignKey { Name = "FK_Orders_Country", RelatedTableSchema = "dbo" };

            SchemaDefaultResolver.Resolve(fk, isSchemaTemplate: true, Platform.SqlServer);

            Assert.That(fk.RelatedTableSchema, Is.EqualTo("dbo"));
        }
    }
}
