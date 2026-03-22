using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Schema.Domain;
using Schema.Utility;
using SchemaHammer.Models;

namespace SchemaHammer.ViewModels.Editors;

public partial class SqlScriptEditorViewModel : EditorBaseViewModel
{
    public override string EditorTitle => Node?.Text ?? "Script";

    [ObservableProperty]
    private string _displayContent = "";

    [ObservableProperty]
    private bool _isPreviewMode;

    public string PreviewButtonText => IsPreviewMode ? "Raw" : "Preview";

    private string _rawContent = "";

    public override void ChangeNode(TreeNodeModel node)
    {
        base.ChangeNode(node);
        IsPreviewMode = false;
        LoadScriptContent(node);
    }

    private void LoadScriptContent(TreeNodeModel node)
    {
        if (string.IsNullOrEmpty(node.NodePath)) return;
        try
        {
            _rawContent = Schema.Isolators.ProductFileWrapper.GetFromFactory().ReadAllText(node.NodePath);
        }
        catch
        {
            _rawContent = $"// Error loading {node.NodePath}";
        }
        DisplayContent = _rawContent;
    }

    [RelayCommand]
    private void TogglePreview()
    {
        if (IsPreviewMode)
        {
            DisplayContent = _rawContent;
            IsPreviewMode = false;
        }
        else
        {
            DisplayContent = ExpandTokens(_rawContent);
            IsPreviewMode = true;
        }
        OnPropertyChanged(nameof(PreviewButtonText));
    }

    internal string ExpandTokens(string content)
    {
        var tokens = CollectScriptTokens();
        foreach (var (key, value) in tokens)
        {
            content = content.Replace($"{{{{{{{key}}}}}}}", value);
        }
        return content;
    }

    internal Dictionary<string, string> CollectScriptTokens()
    {
        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Walk up to find Template and Product nodes
        TreeNodeModel? templateNode = null;
        TreeNodeModel? productNode = null;
        var current = Node?.Parent;
        while (current != null)
        {
            if (current.Tag == "Template" && templateNode == null)
                templateNode = current;
            if (current.Tag == "Product" || current.Parent == null)
                productNode = current;
            current = current.Parent;
        }

        // Load Product tokens first
        if (productNode != null && !string.IsNullOrEmpty(productNode.NodePath))
        {
            try
            {
                var productJsonPath = System.IO.Path.Combine(productNode.NodePath, "Product.json");
                var product = JsonHelper.ProductLoad<Product>(productJsonPath);
                foreach (var kvp in product.ScriptTokens)
                    tokens[kvp.Key] = kvp.Value;
            }
            catch { /* ignore load errors */ }
        }

        // Template tokens override product tokens
        if (templateNode != null && !string.IsNullOrEmpty(templateNode.NodePath))
        {
            try
            {
                var templateJsonPath = System.IO.Path.Combine(templateNode.NodePath, "Template.json");
                var template = JsonHelper.Load<Template>(templateJsonPath);
                foreach (var kvp in template.ScriptTokens)
                    tokens[kvp.Key] = kvp.Value;
            }
            catch { /* ignore load errors */ }
        }

        return tokens;
    }
}
