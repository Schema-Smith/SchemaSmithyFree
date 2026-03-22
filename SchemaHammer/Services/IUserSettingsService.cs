using SchemaHammer.Models;

namespace SchemaHammer.Services;

public interface IUserSettingsService
{
    UserSettings Settings { get; }
    void Load();
    void Save();
    void AddRecentProduct(string path);
}
