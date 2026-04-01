// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

namespace SchemaHammer.ViewModels.Editors;

public class WelcomeViewModel : EditorBaseViewModel
{
    public override string EditorTitle => "Welcome";
    public string WelcomeMessage => "Welcome to SchemaHammer Community";
    public string Instructions => "Use File \u2192 Choose Product to open a schema package.";
}
