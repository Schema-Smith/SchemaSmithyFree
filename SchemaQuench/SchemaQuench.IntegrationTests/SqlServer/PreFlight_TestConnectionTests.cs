// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Microsoft.Extensions.Configuration;
using Schema.IntegrationTests;
using Schema.Isolators;
using Schema.Utility;

namespace SchemaQuench.IntegrationTests.SqlServer;

[TestFixture]
[Category("SqlServer")]
[NonParallelizable]
public class PreFlight_TestConnectionTests : BaseTableQuenchTests
{
    [Test]
    public void TestConnection_ValidServer_Succeeds()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            var config = FactoryContainer.Resolve<IConfigurationRoot>();
            config["SchemaPackagePath"] = TestHelper.GetTestProductPath("SqlServer", "ValidProduct");
            try
            {
                var pq = new ProductQuench();
                Assert.That(pq.RunPreFlight(previewTargets: false), Is.True);
                Assert.That(pq.Failed, Is.False);
            }
            finally
            {
                config["SchemaPackagePath"] = null;
            }
        }
    }

    [Test]
    public void TestConnection_BadCredentials_FailsWithoutThrowing()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            var config = FactoryContainer.Resolve<IConfigurationRoot>();
            config["SchemaPackagePath"] = TestHelper.GetTestProductPath("SqlServer", "ValidProduct");
            var originalPassword = config["Target:Password"];
            config["Target:Password"] = "WrongPassword_xK9z!";
            try
            {
                var pq = new ProductQuench();
                Assert.That(pq.RunPreFlight(previewTargets: false), Is.False);
                Assert.That(pq.Failed, Is.True);
            }
            finally
            {
                config["Target:Password"] = originalPassword;
                config["SchemaPackagePath"] = null;
            }
        }
    }
}
