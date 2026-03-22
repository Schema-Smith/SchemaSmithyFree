using SchemaHammer.Services;

namespace SchemaHammer.UnitTests;

public class UserSettingsServiceTests
{
    private string _tempDir;
    private string _settingsPath;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"SH_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _settingsPath = Path.Combine(_tempDir, "settings.json");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Test]
    public void Load_WhenFileDoesNotExist_UsesDefaults()
    {
        var service = new UserSettingsService(_settingsPath);
        service.Load();
        Assert.Multiple(() =>
        {
            Assert.That(service.Settings.ActiveThemeName, Is.EqualTo("Light"));
            Assert.That(service.Settings.RecentProducts, Is.Empty);
            Assert.That(service.Settings.IsMaximized, Is.False);
        });
    }

    [Test]
    public void SaveAndLoad_RoundTrips()
    {
        var service = new UserSettingsService(_settingsPath);
        service.Settings.ActiveThemeName = "Dark";
        service.Settings.WindowWidth = 1024;
        service.Settings.WindowHeight = 768;
        service.Save();

        var service2 = new UserSettingsService(_settingsPath);
        service2.Load();
        Assert.Multiple(() =>
        {
            Assert.That(service2.Settings.ActiveThemeName, Is.EqualTo("Dark"));
            Assert.That(service2.Settings.WindowWidth, Is.EqualTo(1024));
            Assert.That(service2.Settings.WindowHeight, Is.EqualTo(768));
        });
    }

    [Test]
    public void AddRecentProduct_InsertsAtFront()
    {
        var service = new UserSettingsService(_settingsPath);
        service.AddRecentProduct("/path/a");
        service.AddRecentProduct("/path/b");
        Assert.That(service.Settings.RecentProducts[0], Is.EqualTo("/path/b"));
        Assert.That(service.Settings.RecentProducts[1], Is.EqualTo("/path/a"));
    }

    [Test]
    public void AddRecentProduct_DeduplicatesExisting()
    {
        var service = new UserSettingsService(_settingsPath);
        service.AddRecentProduct("/path/a");
        service.AddRecentProduct("/path/b");
        service.AddRecentProduct("/path/a");
        Assert.That(service.Settings.RecentProducts, Has.Count.EqualTo(2));
        Assert.That(service.Settings.RecentProducts[0], Is.EqualTo("/path/a"));
    }

    [Test]
    public void AddRecentProduct_CapsAtTen()
    {
        var service = new UserSettingsService(_settingsPath);
        for (var i = 0; i < 15; i++)
            service.AddRecentProduct($"/path/{i}");
        Assert.That(service.Settings.RecentProducts, Has.Count.EqualTo(10));
    }

    [Test]
    public void Load_WithCorruptFile_UsesDefaults()
    {
        File.WriteAllText(_settingsPath, "not json at all");
        var service = new UserSettingsService(_settingsPath);
        service.Load();
        Assert.That(service.Settings.ActiveThemeName, Is.EqualTo("Light"));
    }

    [Test]
    public void Load_PrunesStaleProducts()
    {
        var validPath = _tempDir; // exists — the temp dir itself
        var stalePath = Path.Combine(Path.GetTempPath(), "nonexistent_product_" + Guid.NewGuid().ToString("N"));

        // Write settings JSON with both a valid and a stale path
        var json = $"{{\"RecentProducts\":[\"{EscapeJson(validPath)}\",\"{EscapeJson(stalePath)}\"]}}";
        File.WriteAllText(_settingsPath, json);

        var service = new UserSettingsService(_settingsPath);
        service.Load();

        Assert.Multiple(() =>
        {
            Assert.That(service.Settings.RecentProducts, Does.Contain(validPath));
            Assert.That(service.Settings.RecentProducts, Does.Not.Contain(stalePath));
        });
    }

    [Test]
    public void Load_PrunesAllStaleWhenNoneExist()
    {
        var stale1 = Path.Combine(Path.GetTempPath(), "stale_a_" + Guid.NewGuid().ToString("N"));
        var stale2 = Path.Combine(Path.GetTempPath(), "stale_b_" + Guid.NewGuid().ToString("N"));

        var json = $"{{\"RecentProducts\":[\"{EscapeJson(stale1)}\",\"{EscapeJson(stale2)}\"]}}";
        File.WriteAllText(_settingsPath, json);

        var service = new UserSettingsService(_settingsPath);
        service.Load();

        Assert.That(service.Settings.RecentProducts, Is.Empty);
    }

    [Test]
    public void DefaultConstructor_SetsSettingsPathUnderApplicationData()
    {
        // Ensure the default constructor runs without error and produces valid settings
        var service = new UserSettingsService();
        Assert.That(service.Settings, Is.Not.Null);
        Assert.That(service.Settings.ActiveThemeName, Is.EqualTo("Light"));
    }

    private static string EscapeJson(string path) =>
        path.Replace("\\", "\\\\");
}
