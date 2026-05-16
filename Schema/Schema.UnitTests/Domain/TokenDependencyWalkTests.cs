// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Schema.Domain;

namespace Schema.UnitTests.Domain
{
    [TestFixture]
    public class TokenDependencyWalkTests
    {
        [Test]
        public void TokenReferencingSchemaName_MarkedIterationScoped()
        {
            var template = new Template
            {
                SchemaIdentificationScript = "SELECT 'tenant_a'",
                QueryTokens = { ["TenantId"] = "<*Query*>SELECT TenantId FROM {{SchemaName}}.Config" }
            };

            template.ResolveTokenScopes();

            Assert.That(template.IsIterationScoped("TenantId"), Is.True);
        }

        [Test]
        public void TokenReferencingIterationScopedToken_TransitivelyIterationScoped()
        {
            var template = new Template
            {
                SchemaIdentificationScript = "SELECT 'tenant_a'",
                QueryTokens =
                {
                    ["TenantId"] = "<*Query*>SELECT TenantId FROM {{SchemaName}}.Config",
                    ["TenantLabel"] = "<*Query*>SELECT Label FROM Tenants WHERE Id = {{TenantId}}"
                }
            };

            template.ResolveTokenScopes();

            Assert.That(template.IsIterationScoped("TenantId"), Is.True);
            Assert.That(template.IsIterationScoped("TenantLabel"), Is.True);
        }

        [Test]
        public void TokenWithNoSchemaNameReference_StaysPerDb()
        {
            var template = new Template
            {
                SchemaIdentificationScript = "SELECT 'tenant_a'",
                QueryTokens = { ["Region"] = "<*Query*>SELECT Region FROM ServerProperties" }
            };

            template.ResolveTokenScopes();

            Assert.That(template.IsIterationScoped("Region"), Is.False);
        }

        [Test]
        public void TokenCycle_DetectedAtLoad()
        {
            var template = new Template
            {
                SchemaIdentificationScript = "SELECT 'tenant_a'",
                QueryTokens =
                {
                    ["A"] = "<*Query*>SELECT {{B}}",
                    ["B"] = "<*Query*>SELECT {{A}}"
                }
            };

            var ex = Assert.Throws<InvalidOperationException>(() => template.ResolveTokenScopes());
            Assert.That(ex!.Message, Does.Contain("cycle").IgnoreCase);
            // Message should name the tokens involved so the user can find them.
            Assert.That(ex.Message, Does.Contain("A"));
            Assert.That(ex.Message, Does.Contain("B"));
        }

        [Test]
        public void NonQueryToken_ReferencingSchemaName_AlsoEscalatedToIteration()
        {
            // Static (non-<*Query*>) tokens that splice {{SchemaName}} into their body need
            // per-iteration re-evaluation too — the substituted value differs per iteration.
            var template = new Template
            {
                SchemaIdentificationScript = "SELECT 'tenant_a'",
                NonQueryTokens = { ["TenantPrefix"] = "Prefix_{{SchemaName}}" }
            };

            template.ResolveTokenScopes();

            Assert.That(template.IsIterationScoped("TenantPrefix"), Is.True);
        }

        [Test]
        public void IsIterationScoped_BeforeResolveCalled_ReturnsFalse()
        {
            // Pre-resolve, the dispatcher / per-iteration code paths should never observe a "true".
            var template = new Template
            {
                SchemaIdentificationScript = "SELECT 'tenant_a'",
                QueryTokens = { ["X"] = "<*Query*>SELECT 1 FROM {{SchemaName}}.T" }
            };

            Assert.That(template.IsIterationScoped("X"), Is.False);
        }

        [Test]
        public void IsIterationScoped_UnknownToken_ReturnsFalse()
        {
            var template = new Template
            {
                SchemaIdentificationScript = "SELECT 'tenant_a'",
                QueryTokens = { ["TenantId"] = "<*Query*>SELECT 1 FROM {{SchemaName}}.T" }
            };
            template.ResolveTokenScopes();

            Assert.That(template.IsIterationScoped("Bogus"), Is.False);
        }
    }
}
