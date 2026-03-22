using SchemaHammer.ViewModels.Editors;

namespace SchemaHammer.UnitTests;

public class WelcomeViewModelTests
{
    [Test]
    public void EditorTitle_ReturnsWelcome()
    {
        var vm = new WelcomeViewModel();
        Assert.That(vm.EditorTitle, Is.EqualTo("Welcome"));
    }

    [Test]
    public void WelcomeMessage_IsNotEmpty()
    {
        var vm = new WelcomeViewModel();
        Assert.That(vm.WelcomeMessage, Is.Not.Empty);
    }

    [Test]
    public void Instructions_IsNotEmpty()
    {
        var vm = new WelcomeViewModel();
        Assert.That(vm.Instructions, Is.Not.Empty);
    }
}
