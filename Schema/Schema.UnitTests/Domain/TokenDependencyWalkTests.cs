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

        [Test]
        public void IsIterationScoped_LookupIsCaseInsensitive()
        {
            // Token names are case-insensitive throughout the token system (SqlScript.TokenReplace
            // matches case-insensitively at runtime); the resolution map must agree, otherwise
            // callers querying with a casing different from the walk's keys would get wrong-false.
            var template = new Template
            {
                SchemaIdentificationScript = "SELECT 'tenant_a'",
                QueryTokens = { ["TenantId"] = "<*Query*>SELECT TenantId FROM {{SchemaName}}.Config" }
            };

            template.ResolveTokenScopes();

            Assert.That(template.IsIterationScoped("TenantId"), Is.True);
            Assert.That(template.IsIterationScoped("tenantid"), Is.True);
            Assert.That(template.IsIterationScoped("TENANTID"), Is.True);
        }

        [Test]
        public void TokenReferencingLowercaseSchemaName_AlsoMarkedIterationScoped()
        {
            // {{schemaname}} (any casing) is recognized as the iteration trigger — locks in the
            // case-insensitive matching the walk applies via TokenHelper.GetTokensFromString.
            var template = new Template
            {
                SchemaIdentificationScript = "SELECT 'tenant_a'",
                QueryTokens = { ["TenantId"] = "<*Query*>SELECT TenantId FROM {{schemaname}}.Config" }
            };

            template.ResolveTokenScopes();

            Assert.That(template.IsIterationScoped("TenantId"), Is.True);
        }

        [Test]
        public void TokenReferencingItself_DetectedAsCycle()
        {
            // Degenerate single-node cycle (A → A). The walk must surface this as a cycle rather
            // than silently looping or treating the self-reference as an unknown name.
            var template = new Template
            {
                SchemaIdentificationScript = "SELECT 'tenant_a'",
                QueryTokens = { ["A"] = "<*Query*>SELECT {{A}}" }
            };

            var ex = Assert.Throws<InvalidOperationException>(() => template.ResolveTokenScopes());
            Assert.That(ex!.Message, Does.Contain("cycle").IgnoreCase);
            Assert.That(ex.Message, Does.Contain("A"));
        }

        [Test]
        public void TokenCycle_ThreeNodes_DetectedAtLoad()
        {
            // A → B → C → A. All three should appear in the cycle-path message; the order in
            // which Walk happens to encounter them first doesn't matter, but each must be named.
            var template = new Template
            {
                SchemaIdentificationScript = "SELECT 'tenant_a'",
                QueryTokens =
                {
                    ["A"] = "<*Query*>SELECT {{B}}",
                    ["B"] = "<*Query*>SELECT {{C}}",
                    ["C"] = "<*Query*>SELECT {{A}}"
                }
            };

            var ex = Assert.Throws<InvalidOperationException>(() => template.ResolveTokenScopes());
            Assert.That(ex!.Message, Does.Contain("cycle").IgnoreCase);
            Assert.That(ex.Message, Does.Contain("A"));
            Assert.That(ex.Message, Does.Contain("B"));
            Assert.That(ex.Message, Does.Contain("C"));
        }
    }
}
