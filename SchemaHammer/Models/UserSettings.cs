// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

namespace SchemaHammer.Models;

public class UserSettings
{
    public bool IsMaximized { get; set; }
    public double WindowX { get; set; }
    public double WindowY { get; set; }
    public double WindowWidth { get; set; }
    public double WindowHeight { get; set; }
    public List<string> RecentProducts { get; set; } = [];
    public string ActiveThemeName { get; set; } = "Light";
    public string LastSelectedNodePath { get; set; } = "";
}
