// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Schema.Domain;
using Schema.Utility;

namespace Schema.UnitTests.Utility;

[TestFixture]
public class ResourceLoaderTests
{
    [Test]
    public void Load_WithPlatform_SqlServer_ReturnsScriptContent()
    {
        var result = ResourceLoader.Load("SchemaSmith.TableQuench.sql", Platform.SqlServer);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.Not.Empty);
    }

    [Test]
    public void Load_WithPlatform_PostgreSQL_ReturnsScriptContent()
    {
        var result = ResourceLoader.Load("SchemaSmith.TableQuench.sql", Platform.PostgreSQL);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.Not.Empty);
    }

    [Test]
    public void Load_WithPlatform_MySQL_ReturnsScriptContent()
    {
        var result = ResourceLoader.Load("SchemaSmith_TableQuench.sql", Platform.MySQL);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.Not.Empty);
    }

    [Test]
    public void Load_WithPlatform_NonExistentScript_ReturnsNull()
    {
        var result = ResourceLoader.Load("NonExistent.sql", Platform.SqlServer);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void Load_WithPlatform_SqlServer_HasExpectedScripts()
    {
        // Verify multiple scripts exist for SQL Server
        var scripts = new[]
        {
            "SchemaSmith.TableQuench.sql",
            "SchemaSmith.MissingTableAndColumnQuench.sql",
            "SchemaSmith.GenerateTableJson.sql",
            "ParseTableJsonIntoTempTables.sql"
        };

        foreach (var script in scripts)
        {
            var result = ResourceLoader.Load(script, Platform.SqlServer);
            Assert.That(result, Is.Not.Null, $"Script {script} should be loadable for SqlServer");
            Assert.That(result, Is.Not.Empty, $"Script {script} should not be empty for SqlServer");
        }
    }

    [Test]
    public void Load_WithPlatform_PostgreSQL_HasExpectedScripts()
    {
        var scripts = new[]
        {
            "SchemaSmith.TableQuench.sql",
            "SchemaSmith.MissingTableAndColumnQuench.sql",
            "SchemaSmith.GenerateTableJson.sql",
            "ParseTableJsonIntoTempTables.sql"
        };

        foreach (var script in scripts)
        {
            var result = ResourceLoader.Load(script, Platform.PostgreSQL);
            Assert.That(result, Is.Not.Null, $"Script {script} should be loadable for PostgreSQL");
            Assert.That(result, Is.Not.Empty, $"Script {script} should not be empty for PostgreSQL");
        }
    }

    [Test]
    public void Load_WithPlatform_MySQL_HasExpectedScripts()
    {
        var scripts = new[]
        {
            "SchemaSmith_TableQuench.sql",
            "SchemaSmith_MissingTableAndColumnQuench.sql",
            "SchemaSmith_GenerateTableJson.sql",
            "SchemaSmith_ParseTableJson.sql"
        };

        foreach (var script in scripts)
        {
            var result = ResourceLoader.Load(script, Platform.MySQL);
            Assert.That(result, Is.Not.Null, $"Script {script} should be loadable for MySQL");
            Assert.That(result, Is.Not.Empty, $"Script {script} should not be empty for MySQL");
        }
    }

    [Test]
    public void Load_WithPlatform_MariaDb_FallsBackToMySqlScripts()
    {
        // MariaDb is a MySQL variant: with no Scripts/MariaDb override present, the variant
        // lookup misses and ResourceLoader falls back to the MySQL base script. This exercises
        // the previously-unused variant branch in GetPlatformResourceStream.
        var maria = ResourceLoader.Load("SchemaSmith_TableQuench.sql", Platform.MariaDb);
        var mysql = ResourceLoader.Load("SchemaSmith_TableQuench.sql", Platform.MySQL);

        Assert.That(maria, Is.Not.Null.And.Not.Empty);
        Assert.That(maria, Is.EqualTo(mysql));
    }

    [Test]
    public void GetPlatformResourceStream_MariaDb_PrefersVariantOverride_WhenPresent()
    {
        // Exercises the override-wins branch of GetPlatformResourceStream (variantMatch != null),
        // which no production script hits (shared-fix ships no Scripts/MariaDb overrides for v1).
        // Uses test-assembly fixtures embedded under Scripts/MariaDb + Scripts/MySQL.
        var assembly = typeof(ResourceLoaderTests).Assembly;
        using var stream = ResourceLoader.GetPlatformResourceStream("SchemaSmith_SeamProbe.sql", Platform.MariaDb, assembly);

        Assert.That(stream, Is.Not.Null);
        using var reader = new StreamReader(stream);
        Assert.That(reader.ReadToEnd(), Does.Contain("MARIADB OVERRIDE"));
    }

    [Test]
    public void Load_WithPlatform_DifferentPlatforms_ReturnDifferentContent()
    {
        // The same-named script should have different content per platform
        var sqlServerResult = ResourceLoader.Load("SchemaSmith.TableQuench.sql", Platform.SqlServer);
        var postgresResult = ResourceLoader.Load("SchemaSmith.TableQuench.sql", Platform.PostgreSQL);

        Assert.That(sqlServerResult, Is.Not.Null);
        Assert.That(postgresResult, Is.Not.Null);
        Assert.That(sqlServerResult, Is.Not.EqualTo(postgresResult));
    }

    [Test]
    public void Load_WithPlatform_WrongPlatformScriptName_ReturnsNull()
    {
        // MySQL uses underscores, SQL Server uses dots — wrong name returns null
        var result = ResourceLoader.Load("SchemaSmith_TableQuench.sql", Platform.SqlServer);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void GetPlatformResourceStream_NonExistentScript_ReturnsNull()
    {
        var assembly = typeof(ResourceLoader).Assembly;
        var result = ResourceLoader.GetPlatformResourceStream("DoesNotExist.sql", Platform.SqlServer, assembly);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void GetPlatformResourceStream_ValidScript_ReturnsStream()
    {
        var assembly = typeof(ResourceLoader).Assembly;
        using var result = ResourceLoader.GetPlatformResourceStream("SchemaSmith.TableQuench.sql", Platform.SqlServer, assembly);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.GreaterThan(0));
    }

    [Test]
    public void Load_WithoutPlatform_ThrowsForNonExistentFile()
    {
        // The non-platform Load method throws FileLoadException when file is not found
        Assert.Throws<FileLoadException>(() => ResourceLoader.Load("NonExistentFile.xyz"));
    }

    [Test]
    public void Load_WithoutPlatform_ThrowsForNullFileName()
    {
        Assert.Throws<ArgumentNullException>(() => ResourceLoader.Load((string)null));
    }

    [Test]
    public void Load_WithPlatform_EachPlatformHasAtLeastOneScript()
    {
        // Verify each platform has at least one loadable script
        var assembly = typeof(ResourceLoader).Assembly;
        var resourceNames = assembly.GetManifestResourceNames();

        foreach (Platform platform in Enum.GetValues(typeof(Platform)))
        {
            if (platform == Platform.Unknown) continue;
            var platformFolder = platform.GetBasePlatform().ToString();
            var hasScripts = resourceNames.Any(r =>
                r.ToUpper().Contains($".SCRIPTS.{platformFolder.ToUpper()}."));

            Assert.That(hasScripts, Is.True,
                $"Platform {platform} should have at least one embedded SQL script");
        }
    }
}
