// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

#nullable enable

using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Schema.Isolators;

namespace DataTongs.IntegrationTests.Support;

[TestFixture]
public class IsolatedConfigScopeTests
{
    [TearDown]
    public void TearDown() => FactoryContainer.Unregister<IConfigurationRoot>();

    [Test]
    public void Create_ClonesRegisteredConfig_LeavesOriginalUnmutated_AndRestoresOnDispose()
    {
        var original = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["SqlServer:Server"] = "orig-host" })
            .Build();
        FactoryContainer.Register<IConfigurationRoot>(original);

        using (var scope = IsolatedConfigScope.Create(new Dictionary<string, string?> { ["Source:Server"] = "override-host" }))
        {
            Assert.That(scope.Config["Source:Server"], Is.EqualTo("override-host"));
            Assert.That(scope.Config["SqlServer:Server"], Is.EqualTo("orig-host"), "clone carries the original's values");
            Assert.That(original["Source:Server"], Is.Null, "the original config must NOT be mutated");
            Assert.That(FactoryContainer.Resolve<IConfigurationRoot>(), Is.SameAs(scope.Config), "the clone is the active config inside the scope");
        }

        Assert.That(FactoryContainer.Resolve<IConfigurationRoot>(), Is.SameAs(original), "dispose restores the pristine original");
    }

    [Test]
    public void Dispose_Unregisters_WhenNoConfigWasRegistered()
    {
        FactoryContainer.Unregister<IConfigurationRoot>();
        using (IsolatedConfigScope.Create()) { }
        Assert.That(FactoryContainer.Resolve<IConfigurationRoot>(), Is.Null);
    }
}
