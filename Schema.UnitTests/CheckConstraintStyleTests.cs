// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.Domain;
using Newtonsoft.Json;

namespace Schema.UnitTests;

public class CheckConstraintStyleTests
{
    [Test]
    public void Product_DefaultCheckConstraintStyle_IsColumnLevel()
    {
        var product = new Product();
        Assert.That(product.CheckConstraintStyle, Is.EqualTo(CheckConstraintStyle.ColumnLevel));
    }

    [Test]
    public void Product_ColumnLevelDefault_OmittedFromJson()
    {
        var product = new Product { Name = "Test", ValidationScript = "SELECT 1" };
        var json = JsonConvert.SerializeObject(product, new JsonSerializerSettings
            { DefaultValueHandling = DefaultValueHandling.Ignore });
        Assert.That(json, Does.Not.Contain("CheckConstraintStyle"));
    }

    [Test]
    public void Product_TableLevel_IncludedInJson()
    {
        var product = new Product
        {
            Name = "Test", ValidationScript = "SELECT 1",
            CheckConstraintStyle = CheckConstraintStyle.TableLevel
        };
        var json = JsonConvert.SerializeObject(product);
        Assert.That(json, Does.Contain("TableLevel"));
    }

    [Test]
    public void Product_RoundTrip_PreservesTableLevel()
    {
        var original = new Product
        {
            Name = "Test", ValidationScript = "SELECT 1",
            CheckConstraintStyle = CheckConstraintStyle.TableLevel
        };
        var json = JsonConvert.SerializeObject(original);
        var deserialized = JsonConvert.DeserializeObject<Product>(json);
        Assert.That(deserialized.CheckConstraintStyle, Is.EqualTo(CheckConstraintStyle.TableLevel));
    }
}
