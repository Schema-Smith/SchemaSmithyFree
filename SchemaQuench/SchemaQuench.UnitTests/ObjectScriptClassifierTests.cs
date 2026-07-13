// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using NUnit.Framework;

namespace SchemaQuench.UnitTests;

[TestFixture]
public class ObjectScriptClassifierTests
{
    [TestCase("Main/Procedures/usp_Get.sql", "procedure")]
    [TestCase("Main\\Views\\vw_Orders.sql", "view")]
    [TestCase("Functions/fn_Calc.sql", "function")]
    [TestCase("StoredProcedures/x.sql", "procedure")]
    [TestCase("Weird/thing.sql", "objectScript")]
    public void Classify_MapsByFolderLeaf(string path, string expected) =>
        Assert.That(ObjectScriptClassifier.Classify(path), Is.EqualTo(expected));
}
