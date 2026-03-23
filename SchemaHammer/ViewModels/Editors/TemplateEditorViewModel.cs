using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Schema.Domain;
using Schema.Utility;
using SchemaHammer.Models;

namespace SchemaHammer.ViewModels.Editors;

public partial class TemplateEditorViewModel : EditorBaseViewModel
{
    public override string EditorTitle => Name;

    public string Name { get; private set; } = "";
    public string DatabaseIdentificationScript { get; private set; } = "";
    public string VersionStampScript { get; private set; } = "";
    public string BaselineValidationScript { get; private set; } = "";
    public bool UpdateFillFactor { get; private set; }
    public ObservableCollection<KeyValuePair<string, string>> ScriptTokens { get; } = [];

    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private KeyValuePair<string, string>? _selectedScriptToken;

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

        if (PendingTokenName != null)
        {
            var tokenName = PendingTokenName;
            PendingTokenName = null;
            SelectedTabIndex = 1;
            SelectedScriptToken = ScriptTokens.FirstOrDefault(
                t => t.Key.Equals(tokenName, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            SelectedTabIndex = 0;
        }
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
