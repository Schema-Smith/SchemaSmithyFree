using SchemaHammer.Models;
using SchemaHammer.ViewModels.Editors;

namespace SchemaHammer.Services;

public interface IEditorService
{
    EditorBaseViewModel? GetEditor(TreeNodeModel node);
    string? GetEditorTag(string nodeTag);
}
