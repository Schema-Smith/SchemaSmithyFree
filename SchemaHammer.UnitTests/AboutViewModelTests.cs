using SchemaHammer.ViewModels;

namespace SchemaHammer.UnitTests;

public class AboutViewModelTests
{
    [Test]
    public void AppName_IsSchemaHammerCommunity()
    {
        var vm = new AboutViewModel();
        Assert.That(vm.AppName, Is.EqualTo("SchemaHammer Community"));
    }

    [Test]
    public void Version_IsNotEmpty()
    {
        var vm = new AboutViewModel();
        Assert.That(vm.Version, Is.Not.Empty);
    }

    [Test]
    public void Description_IsNotEmpty()
    {
        var vm = new AboutViewModel();
        Assert.That(vm.Description, Is.Not.Empty);
    }

    [Test]
    public void GitHubUrl_ContainsSchemaSmithyFree()
    {
        var vm = new AboutViewModel();
        Assert.That(vm.GitHubUrl, Does.Contain("SchemaSmithyFree"));
    }
}
