// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

namespace SchemaTongs.UnitTests;

[TestFixture]
public class FileNameEncodingTests
{
    [Test]
    public void EncodeFileName_SchemaAndName_EncodesIllegalChars()
    {
        var result = SchemaTongsEncoder.EncodeFileName("dbo", "My:Table", ".json");
        Assert.That(result, Is.EqualTo("dbo.My%3ATable.json"));
    }

    [Test]
    public void EncodeFileName_SchemaWithIllegalChars_EncodesSchema()
    {
        var result = SchemaTongsEncoder.EncodeFileName("my*schema", "MyTable", ".sql");
        Assert.That(result, Is.EqualTo("my%2Aschema.MyTable.sql"));
    }

    [Test]
    public void EncodeFileName_NameOnly_EncodesIllegalChars()
    {
        var result = SchemaTongsEncoder.EncodeFileName("My<Catalog>", ".sql");
        Assert.That(result, Is.EqualTo("My%3CCatalog%3E.sql"));
    }

    [Test]
    public void EncodeFileName_PlainName_Unchanged()
    {
        var result = SchemaTongsEncoder.EncodeFileName("dbo", "Users", ".json");
        Assert.That(result, Is.EqualTo("dbo.Users.json"));
    }

    [Test]
    public void EncodeFileName_TriggerThreePart_EncodesAllParts()
    {
        var result = SchemaTongsEncoder.EncodeTriggerFileName("dbo", "My:Table", "tr_Insert", ".sql");
        Assert.That(result, Is.EqualTo("dbo.My%3ATable.tr_Insert.sql"));
    }

    [Test]
    public void EncodeFileName_NameOnly_PlainName_Unchanged()
    {
        var result = SchemaTongsEncoder.EncodeFileName("MyCatalog", ".sql");
        Assert.That(result, Is.EqualTo("MyCatalog.sql"));
    }
}
