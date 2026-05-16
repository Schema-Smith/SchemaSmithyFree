// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Schema.Domain;
using Schema.Domain.PostgreSQL;
using Schema.Domain.SqlServer;

namespace Schema.UnitTests.Domain
{
    [TestFixture]
    public class SchemaDefaultResolverTests
    {
        [Test]
        public void Resolve_NullArgument_DoesNotThrow()
        {
            // Defensive: resolver tolerates a null table/fk passed in (lists in domain may have nulls during construction).
            Assert.DoesNotThrow(() =>
                SchemaDefaultResolver.Resolve((SqlServerTable)null, isSchemaTemplate: false, Platform.SqlServer));
        }

        [Test]
        public void Resolve_SqlServerIndexedView_UnsetSchema_ResolvesToDbo_InRegularTemplate()
        {
            var view = new SqlServerIndexedView { Name = "vw_Test" };

            SchemaDefaultResolver.Resolve(view, isSchemaTemplate: false, Platform.SqlServer);

            Assert.That(view.Schema, Is.EqualTo("dbo"));
        }

        [Test]
        public void Resolve_SqlServerIndexedView_UnsetSchema_ResolvesToSchemaNameToken_InSchemaTemplate()
        {
            var view = new SqlServerIndexedView { Name = "vw_Test" };

            SchemaDefaultResolver.Resolve(view, isSchemaTemplate: true, Platform.SqlServer);

            Assert.That(view.Schema, Is.EqualTo("{{SchemaName}}"));
        }

        [Test]
        public void Resolve_SqlServerIndexedView_HardLiteralInSchemaTemplate_Rejected()
        {
            var view = new SqlServerIndexedView { Name = "vw_Test", Schema = "dbo" };

            var ex = Assert.Throws<InvalidOperationException>(() =>
                SchemaDefaultResolver.Resolve(view, isSchemaTemplate: true, Platform.SqlServer));

            Assert.That(ex.Message, Does.Contain("vw_Test"));
        }

        [Test]
        public void Resolve_PostgreSqlMaterializedView_UnsetSchema_ResolvesToPublic_InRegularTemplate()
        {
            var view = new PostgreSqlMaterializedView { Name = "mv_test" };

            SchemaDefaultResolver.Resolve(view, isSchemaTemplate: false, Platform.PostgreSQL);

            Assert.That(view.Schema, Is.EqualTo("public"));
        }

        [Test]
        public void Resolve_PostgreSqlMaterializedView_UnsetSchema_ResolvesToSchemaNameToken_InSchemaTemplate()
        {
            var view = new PostgreSqlMaterializedView { Name = "mv_test" };

            SchemaDefaultResolver.Resolve(view, isSchemaTemplate: true, Platform.PostgreSQL);

            Assert.That(view.Schema, Is.EqualTo("{{SchemaName}}"));
        }

        [Test]
        public void Resolve_PostgreSqlMaterializedView_HardLiteralInSchemaTemplate_Rejected()
        {
            var view = new PostgreSqlMaterializedView { Name = "mv_test", Schema = "public" };

            var ex = Assert.Throws<InvalidOperationException>(() =>
                SchemaDefaultResolver.Resolve(view, isSchemaTemplate: true, Platform.PostgreSQL));

            Assert.That(ex.Message, Does.Contain("mv_test"));
        }

        [Test]
        public void Resolve_Template_RegularTemplate_AppliesPlatformDefaultsToAllChildren()
        {
            var template = new Template { Name = "Regular" };
            var table = new SqlServerTable { Name = "Customers" };
            table.ForeignKeys.Add(new SqlServerForeignKey { Name = "FK_Customers_Region", RelatedTable = "Regions" });
            template.Tables.Add(table);
            template.IndexedViews.Add(new SqlServerIndexedView { Name = "vw_Active" });
            template.Product = new Product { Platform = Platform.SqlServer };

            SchemaDefaultResolver.Resolve(template);

            Assert.That(table.Schema, Is.EqualTo("dbo"));
            Assert.That(((SqlServerForeignKey)table.ForeignKeys[0]).RelatedTableSchema, Is.EqualTo("dbo"));
            Assert.That(template.IndexedViews[0].Schema, Is.EqualTo("dbo"));
        }

        [Test]
        public void Resolve_Template_SchemaTemplate_AppliesSchemaNameTokenDefaults()
        {
            var template = new Template
            {
                Name = "TenantBody",
                SchemaIdentificationScript = "SELECT 'tenant_a' AS SchemaName"
            };
            var table = new SqlServerTable { Name = "Customers" };
            table.ForeignKeys.Add(new SqlServerForeignKey { Name = "FK_Customers_Region", RelatedTable = "Regions" });
            // Cross-schema FK kept explicit (preserved by resolver)
            table.ForeignKeys.Add(new SqlServerForeignKey
            {
                Name = "FK_Customers_Country", RelatedTable = "Countries", RelatedTableSchema = "dbo"
            });
            template.Tables.Add(table);
            template.IndexedViews.Add(new SqlServerIndexedView { Name = "vw_Active" });
            template.Product = new Product { Platform = Platform.SqlServer };

            SchemaDefaultResolver.Resolve(template);

            Assert.That(table.Schema, Is.EqualTo("{{SchemaName}}"));
            Assert.That(((SqlServerForeignKey)table.ForeignKeys[0]).RelatedTableSchema, Is.EqualTo("{{SchemaName}}"));
            Assert.That(((SqlServerForeignKey)table.ForeignKeys[1]).RelatedTableSchema, Is.EqualTo("dbo"),
                "Cross-schema FK with explicit literal must be preserved.");
            Assert.That(template.IndexedViews[0].Schema, Is.EqualTo("{{SchemaName}}"));
        }

        [Test]
        public void Resolve_Template_PostgreSql_SchemaTemplate_AppliesSchemaNameTokenDefaults()
        {
            var template = new Template
            {
                Name = "tenant_body",
                SchemaIdentificationScript = "SELECT 'tenant_a' AS schema_name"
            };
            var table = new PostgreSqlTable { Name = "customers" };
            table.ForeignKeys.Add(new PostgreSqlForeignKey { Name = "fk_customers_region", RelatedTable = "regions" });
            table.ForeignKeys.Add(new PostgreSqlForeignKey
            {
                Name = "fk_customers_country", RelatedTable = "countries", RelatedTableSchema = "public"
            });
            template.Tables.Add(table);
            template.MaterializedViews.Add(new PostgreSqlMaterializedView { Name = "mv_active" });
            template.Product = new Product { Platform = Platform.PostgreSQL };

            SchemaDefaultResolver.Resolve(template);

            Assert.That(table.Schema, Is.EqualTo("{{SchemaName}}"));
            Assert.That(((PostgreSqlForeignKey)table.ForeignKeys[0]).RelatedTableSchema, Is.EqualTo("{{SchemaName}}"));
            Assert.That(((PostgreSqlForeignKey)table.ForeignKeys[1]).RelatedTableSchema, Is.EqualTo("public"),
                "Cross-schema FK with explicit literal must be preserved.");
            Assert.That(template.MaterializedViews[0].Schema, Is.EqualTo("{{SchemaName}}"));
        }

        [Test]
        public void Resolve_Template_DerivesSchemaTemplateFlag_FromSchemaIdentificationScriptPresence()
        {
            var regular = new Template { Name = "Regular" };
            Assert.That(regular.IsSchemaTemplate, Is.False);

            var schemaTemplate = new Template { Name = "Tenant", SchemaIdentificationScript = "SELECT 'x'" };
            Assert.That(schemaTemplate.IsSchemaTemplate, Is.True);

            var emptyScript = new Template { Name = "Empty", SchemaIdentificationScript = "   " };
            Assert.That(emptyScript.IsSchemaTemplate, Is.False, "Whitespace-only script should not flip the flag.");
        }
    }
}
