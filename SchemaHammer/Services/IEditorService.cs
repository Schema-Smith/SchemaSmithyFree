// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using SchemaHammer.Models;
using SchemaHammer.ViewModels.Editors;

namespace SchemaHammer.Services;

public interface IEditorService
{
    EditorBaseViewModel? GetEditor(TreeNodeModel node);
    string? GetEditorTag(string nodeTag);
}
