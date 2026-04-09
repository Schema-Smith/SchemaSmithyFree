// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using Schema.Domain.SqlServer;

namespace Schema.UnitTests.Domain.SqlServer
{
    [TestFixture]
    public class ServerToQuenchTests
    {
        [Test]
        public void HasExpectedValues()
        {
            Assert.That(Enum.GetValues<ServerToQuench>(), Has.Length.EqualTo(3));
            Assert.That(ServerToQuench.Primary, Is.EqualTo((ServerToQuench)0));
            Assert.That(ServerToQuench.Secondary, Is.EqualTo((ServerToQuench)1));
            Assert.That(ServerToQuench.Both, Is.EqualTo((ServerToQuench)2));
        }
    }
}
