// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using SchemaHammer.Models;

namespace SchemaHammer.Services;

public class UserSettingsService : IUserSettingsService
{
    private readonly string _settingsPath;

    public UserSettings Settings { get; private set; } = new();

    public UserSettingsService()
    {
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SchemaHammer Community");
        Directory.CreateDirectory(appDataDir);
        _settingsPath = Path.Combine(appDataDir, "settings.json");
    }

    internal UserSettingsService(string settingsPath)
    {
        _settingsPath = settingsPath;
    }

    public void Load()
    {
        if (!File.Exists(_settingsPath)) return;

        try
        {
            var json = File.ReadAllText(_settingsPath);
            Settings = JsonConvert.DeserializeObject<UserSettings>(json) ?? new UserSettings();
        }
        catch
        {
            Settings = new UserSettings();
        }

        PruneStaleProducts();
    }

    public void Save()
    {
        var json = JsonConvert.SerializeObject(Settings, Formatting.Indented);
        File.WriteAllText(_settingsPath, json);
    }

    public void AddRecentProduct(string path)
    {
        Settings.RecentProducts.Remove(path);
        Settings.RecentProducts.Insert(0, path);
        if (Settings.RecentProducts.Count > 10)
            Settings.RecentProducts.RemoveRange(10, Settings.RecentProducts.Count - 10);
    }

    private void PruneStaleProducts()
    {
        Settings.RecentProducts.RemoveAll(p =>
            !Directory.Exists(p) && !File.Exists(p));
    }
}
