// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.Domain;
using Schema.Domain.PostgreSQL;

namespace Schema.UnitTests.Domain.PostgreSQL
{
    [TestFixture]
    public class PostgreSqlForeignKeyDefaultingTests
    {
        [Test]
        public void BareConstructor_RelatedTableSchema_IsNull()
        {
            var fk = new PostgreSqlForeignKey();
            Assert.That(fk.RelatedTableSchema, Is.Null);
        }

        [Test]
        public void RegularTemplate_OmittedRelatedTableSchema_ResolvesToPublic()
        {
            var fk = new PostgreSqlForeignKey { Name = "fk_x" };

            SchemaDefaultResolver.Resolve(fk, isSchemaTemplate: false, Platform.PostgreSQL);

            Assert.That(fk.RelatedTableSchema, Is.EqualTo("public"));
        }

        [Test]
        public void RegularTemplate_EmptyRelatedTableSchema_ResolvesToPublic()
        {
            var fk = new PostgreSqlForeignKey { Name = "fk_x", RelatedTableSchema = "" };

            SchemaDefaultResolver.Resolve(fk, isSchemaTemplate: false, Platform.PostgreSQL);

            Assert.That(fk.RelatedTableSchema, Is.EqualTo("public"));
        }

        [Test]
        public void RegularTemplate_ExplicitLiteralRelatedTableSchema_Preserved()
        {
            var fk = new PostgreSqlForeignKey { Name = "fk_x", RelatedTableSchema = "sales" };

            SchemaDefaultResolver.Resolve(fk, isSchemaTemplate: false, Platform.PostgreSQL);

            Assert.That(fk.RelatedTableSchema, Is.EqualTo("sales"));
        }

        [Test]
        public void SchemaTemplate_OmittedRelatedTableSchema_ResolvesToSchemaNameToken()
        {
            var fk = new PostgreSqlForeignKey { Name = "fk_x" };

            SchemaDefaultResolver.Resolve(fk, isSchemaTemplate: true, Platform.PostgreSQL);

            Assert.That(fk.RelatedTableSchema, Is.EqualTo("{{SchemaName}}"));
        }

        [Test]
        public void SchemaTemplate_EmptyRelatedTableSchema_ResolvesToSchemaNameToken()
        {
            var fk = new PostgreSqlForeignKey { Name = "fk_x", RelatedTableSchema = "" };

            SchemaDefaultResolver.Resolve(fk, isSchemaTemplate: true, Platform.PostgreSQL);

            Assert.That(fk.RelatedTableSchema, Is.EqualTo("{{SchemaName}}"));
        }

        [Test]
        public void SchemaTemplate_LiteralSchemaNameTokenAccepted()
        {
            var fk = new PostgreSqlForeignKey { Name = "fk_x", RelatedTableSchema = "{{SchemaName}}" };

            SchemaDefaultResolver.Resolve(fk, isSchemaTemplate: true, Platform.PostgreSQL);

            Assert.That(fk.RelatedTableSchema, Is.EqualTo("{{SchemaName}}"));
        }

        [Test]
        public void SchemaTemplate_HardLiteralRelatedTableSchema_Preserved_ForCrossSchemaFk()
        {
            // Per design §3.3.5 / §3.4: explicit literal RelatedTableSchema on FKs is allowed
            // in schema templates (cross-schema reference, e.g. tenant table FK to public.countries).
            var fk = new PostgreSqlForeignKey { Name = "fk_orders_country", RelatedTableSchema = "public" };

            SchemaDefaultResolver.Resolve(fk, isSchemaTemplate: true, Platform.PostgreSQL);

            Assert.That(fk.RelatedTableSchema, Is.EqualTo("public"));
        }
    }
}
