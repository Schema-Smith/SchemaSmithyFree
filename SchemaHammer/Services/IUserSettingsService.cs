// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using SchemaHammer.Models;

namespace SchemaHammer.Services;

public interface IUserSettingsService
{
    UserSettings Settings { get; }
    void Load();
    void Save();
    void AddRecentProduct(string path);
}
