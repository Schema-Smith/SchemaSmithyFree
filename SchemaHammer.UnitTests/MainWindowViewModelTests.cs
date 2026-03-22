using NSubstitute;
using SchemaHammer.Models;
using SchemaHammer.Services;
using SchemaHammer.ViewModels;
using SchemaHammer.ViewModels.Editors;

namespace SchemaHammer.UnitTests;

public class MainWindowViewModelTests
{
    [Test]
    public void Constructor_SetsDefaultTitle()
    {
        var settings = Substitute.For<IUserSettingsService>();
        settings.Settings.Returns(new UserSettings());
        var vm = new MainWindowViewModel(settings);
        Assert.That(vm.Title, Is.EqualTo("SchemaHammer Community"));
    }

    [Test]
    public void Constructor_SetsWelcomeEditor()
    {
        var settings = Substitute.For<IUserSettingsService>();
        settings.Settings.Returns(new UserSettings());
        var vm = new MainWindowViewModel(settings);
        Assert.That(vm.CurrentEditor, Is.TypeOf<WelcomeViewModel>());
    }

    [Test]
    public void ProductStatus_DefaultsToNoProduct()
    {
        var settings = Substitute.For<IUserSettingsService>();
        settings.Settings.Returns(new UserSettings());
        var vm = new MainWindowViewModel(settings);
        Assert.That(vm.ProductStatus, Is.EqualTo("NO PRODUCT"));
    }

    [Test]
    public void SaveWindowState_PersistsToSettings()
    {
        var settingsService = Substitute.For<IUserSettingsService>();
        settingsService.Settings.Returns(new UserSettings());
        var vm = new MainWindowViewModel(settingsService);

        vm.SaveWindowState(true, 100, 200, 1024, 768);

        Assert.Multiple(() =>
        {
            Assert.That(vm.Settings.IsMaximized, Is.True);
            Assert.That(vm.Settings.WindowX, Is.EqualTo(100));
            Assert.That(vm.Settings.WindowY, Is.EqualTo(200));
            Assert.That(vm.Settings.WindowWidth, Is.EqualTo(1024));
            Assert.That(vm.Settings.WindowHeight, Is.EqualTo(768));
        });
        settingsService.Received(1).Save();
    }
}
