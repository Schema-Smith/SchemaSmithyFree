// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Schema.Domain;

namespace Schema.UnitTests.Domain;

[TestFixture]
public class TableMetadataTokenTests
{
    [Test]
    public void GenerateTableMetadataToken_ContainsTableData()
    {
        var tables = new List<Table>
        {
            new() { Name = "Users", Schema = "dbo", Columns = [new Column { Name = "Id", DataType = "INT" }] }
        };

        var token = Template.GenerateTableMetadataToken(tables);
        Assert.That(token, Does.Contain("Users"));
        Assert.That(token, Does.Contain("dbo"));
    }

    [Test]
    public void GenerateTableMetadataToken_EscapesSingleQuotes()
    {
        var tables = new List<Table>
        {
            new() { Name = "User's", Schema = "dbo", Columns = [new Column { Name = "Id", DataType = "INT" }] }
        };

        var token = Template.GenerateTableMetadataToken(tables);
        Assert.That(token, Does.Contain("User''s"));
        Assert.That(token, Does.Not.Contain("User's"));
    }

    [Test]
    public void GenerateTableMetadataToken_IncludesExtensions()
    {
        var tables = new List<Table>
        {
            new()
            {
                Name = "Orders", Schema = "dbo",
                Columns = [new Column { Name = "Id", DataType = "INT" }],
                Extensions = JToken.Parse("""{ "Team": "Platform" }""")
            }
        };

        var token = Template.GenerateTableMetadataToken(tables);
        Assert.That(token, Does.Contain("Platform"));
        Assert.That(token, Does.Contain("Extensions"));
    }

    [Test]
    public void GenerateTableMetadataToken_IncludesColumnExtensions()
    {
        var tables = new List<Table>
        {
            new()
            {
                Name = "T1", Schema = "dbo",
                Columns = [new Column { Name = "Email", DataType = "NVARCHAR(255)", Extensions = JToken.Parse("""{ "PII": true }""") }]
            }
        };

        var token = Template.GenerateTableMetadataToken(tables);
        Assert.That(token, Does.Contain("PII"));
    }

    [Test]
    public void GenerateTableMetadataToken_EmptyTables_ReturnsEmptyArray()
    {
        var token = Template.GenerateTableMetadataToken(new List<Table>());
        Assert.That(token, Is.EqualTo("[]"));
    }
}
