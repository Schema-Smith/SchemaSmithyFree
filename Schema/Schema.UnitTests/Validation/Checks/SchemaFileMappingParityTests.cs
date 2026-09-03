// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using NUnit.Framework;
using Schema.Domain;
using Schema.Utility;
using Schema.Validation.Checks;

namespace Schema.UnitTests.Validation.Checks;

/// <summary>
/// <see cref="JsonSchemaCheck"/> carries a private copy of RepositoryHelper's schema-file → domain-type
/// mapping, duplicated because SchemaQuench must not reach into Schema's private members. The two drifting
/// out of lockstep is not hypothetical: the F4 (events) and F5 (enum types / sequences) promotions each
/// added a schema file to <see cref="RepositoryHelper.GetSchemaFileNames"/> and did NOT add the matching
/// row here, so <c>--Validate</c> threw <c>Unknown schema file mapping</c> on every clean package for those
/// engines — and the full integration gate was the only thing that caught it.
///
/// This guard makes the outcome the assertion: every schema file the repository will ever ask the validator
/// to resolve must resolve. Add a declarative type without wiring the validator and this reddens at unit
/// speed, instead of an hour into the integration suite.
/// </summary>
[TestFixture]
public class SchemaFileMappingParityTests
{
    private static readonly Platform[] Platforms =
        { Platform.SqlServer, Platform.PostgreSQL, Platform.MySQL, Platform.MariaDb };

    [Test]
    public void EverySchemaFileTheRepositoryProduces_ResolvesInTheValidator()
    {
        foreach (var platform in Platforms)
        {
            foreach (var fileName in RepositoryHelper.GetSchemaFileNames(platform))
            {
                Assert.DoesNotThrow(
                    () => JsonSchemaCheck.GetTypeForSchemaFile(fileName, platform),
                    $"RepositoryHelper produces '{fileName}' for {platform}, but JsonSchemaCheck cannot map "
                    + "it -- the two mappings have drifted. Add the matching row to "
                    + "JsonSchemaCheck.GetTypeForSchemaFile.");
            }
        }
    }
}
