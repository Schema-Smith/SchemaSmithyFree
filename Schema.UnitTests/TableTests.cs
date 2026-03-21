// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
using NSubstitute;
﻿using NSubstitute;
using Newtonsoft.Json;
using Schema.Isolators;
using Schema.Domain;
using System;

namespace Schema.UnitTests;

public class TableTests
{
    [Test]
    public void ShouldProvideTheFileNameWhenErrorLoadingATable()
    {
        var mockFileWrapper = Substitute.For<IFile>();
        mockFileWrapper.Exists(Arg.Any<string>()).Returns(false);
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(mockFileWrapper);

            var ex = Assert.Throws<Exception>(() => Table.Load("badPath"));
            Assert.That(ex!.Message, Contains.Substring("Error loading table from badPath"));

            FactoryContainer.Clear();
        }
    }

    [Test]
    public void ShouldSerializeUpdateFillFactor()
    {
        var table = new Table { Name = "Test", UpdateFillFactor = true };
        var json = JsonConvert.SerializeObject(table);
        Assert.That(json, Contains.Substring("\"UpdateFillFactor\":true"));
    }

    [Test]
    public void ShouldDeserializeUpdateFillFactorDefaultsToFalse()
    {
        var json = "{\"Name\":\"Test\"}";
        var table = JsonConvert.DeserializeObject<Table>(json);
        Assert.That(table!.UpdateFillFactor, Is.False);
    }
}
