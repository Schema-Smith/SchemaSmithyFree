using System.Collections.ObjectModel;
using Schema.Domain;
using Schema.Utility;
using SchemaHammer.Models;

namespace SchemaHammer.ViewModels.Editors;

public class TemplateEditorViewModel : EditorBaseViewModel
{
    public override string EditorTitle => Name;

    public string Name { get; private set; } = "";
    public string DatabaseIdentificationScript { get; private set; } = "";
    public string VersionStampScript { get; private set; } = "";
    public string BaselineValidationScript { get; private set; } = "";
    public bool UpdateFillFactor { get; private set; }
    public ObservableCollection<KeyValuePair<string, string>> ScriptTokens { get; } = [];

    public override void ChangeNode(TreeNodeModel node)
    {
        base.ChangeNode(node);
        if (string.IsNullOrEmpty(node.NodePath)) return;

        Template template;
        try { template = JsonHelper.ProductLoad<Template>(node.NodePath); }
        catch { return; }

        Name = template.Name ?? "";
        DatabaseIdentificationScript = template.DatabaseIdentificationScript ?? "";
        VersionStampScript = template.VersionStampScript ?? "";
        BaselineValidationScript = template.BaselineValidationScript ?? "";
        UpdateFillFactor = template.UpdateFillFactor;

        ScriptTokens.Clear();
        foreach (var kvp in template.ScriptTokens) ScriptTokens.Add(kvp);

        NotifyAllProperties();
    }

    private void NotifyAllProperties()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(DatabaseIdentificationScript));
        OnPropertyChanged(nameof(VersionStampScript));
        OnPropertyChanged(nameof(BaselineValidationScript));
        OnPropertyChanged(nameof(UpdateFillFactor));
    }
}
