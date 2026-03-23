using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Schema.Domain;
using Schema.Utility;
using SchemaHammer.Models;

namespace SchemaHammer.ViewModels.Editors;

public partial class ProductEditorViewModel : EditorBaseViewModel
{
    public override string EditorTitle => Name;

    public string Name { get; private set; } = "";
    public string Platform { get; private set; } = "";
    public string MinimumVersion { get; private set; } = "";
    public bool DropUnknownIndexes { get; private set; }
    public string ValidationScript { get; private set; } = "";
    public string BaselineValidationScript { get; private set; } = "";
    public string VersionStampScript { get; private set; } = "";
    public ObservableCollection<string> TemplateOrder { get; } = [];
    public ObservableCollection<KeyValuePair<string, string>> ScriptTokens { get; } = [];

    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private KeyValuePair<string, string>? _selectedScriptToken;

    public override void ChangeNode(TreeNodeModel node)
    {
        base.ChangeNode(node);
        if (string.IsNullOrEmpty(node.NodePath)) return;

        var productJsonPath = System.IO.Path.Combine(node.NodePath, "Product.json");
        Product product;
        try { product = JsonHelper.ProductLoad<Product>(productJsonPath); }
        catch { return; }

        Name = product.Name ?? "";
        Platform = product.Platform ?? "MSSQL";
        MinimumVersion = product.MinimumVersion?.ToString() ?? "";
        DropUnknownIndexes = product.DropUnknownIndexes;
        ValidationScript = product.ValidationScript ?? "";
        BaselineValidationScript = product.BaselineValidationScript ?? "";
        VersionStampScript = product.VersionStampScript ?? "";

        TemplateOrder.Clear();
        foreach (var t in product.TemplateOrder) TemplateOrder.Add(t);

        ScriptTokens.Clear();
        foreach (var kvp in product.ScriptTokens) ScriptTokens.Add(kvp);

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
        OnPropertyChanged(nameof(Platform));
        OnPropertyChanged(nameof(MinimumVersion));
        OnPropertyChanged(nameof(DropUnknownIndexes));
        OnPropertyChanged(nameof(ValidationScript));
        OnPropertyChanged(nameof(BaselineValidationScript));
        OnPropertyChanged(nameof(VersionStampScript));
    }
}
