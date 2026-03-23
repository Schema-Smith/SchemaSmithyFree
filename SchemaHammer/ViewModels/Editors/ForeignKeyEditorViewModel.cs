// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using SchemaHammer.Models;

namespace SchemaHammer.ViewModels.Editors;

public class ForeignKeyEditorViewModel : EditorBaseViewModel
{
    public override string EditorTitle => Name;

    public string Name { get; private set; } = "";
    public string Columns { get; private set; } = "";
    public string RelatedTableSchema { get; private set; } = "";
    public string RelatedTable { get; private set; } = "";
    public string RelatedColumns { get; private set; } = "";
    public string DeleteAction { get; private set; } = "";
    public string UpdateAction { get; private set; } = "";

    public override void ChangeNode(TreeNodeModel node)
    {
        base.ChangeNode(node);
        var table = ColumnEditorViewModel.FindParentTable(node);
        var fk = table?.ForeignKeys.FirstOrDefault(f => NameMatchesNodeText(f.Name, node.Text));
        if (fk != null)
        {
            Name = StripBrackets(fk.Name);
            Columns = fk.Columns ?? "";
            RelatedTableSchema = fk.RelatedTableSchema ?? "dbo";
            RelatedTable = fk.RelatedTable ?? "";
            RelatedColumns = fk.RelatedColumns ?? "";
            DeleteAction = fk.DeleteAction ?? "";
            UpdateAction = fk.UpdateAction ?? "";
        }
        NotifyAllProperties();
    }

    private void NotifyAllProperties()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Columns));
        OnPropertyChanged(nameof(RelatedTableSchema));
        OnPropertyChanged(nameof(RelatedTable));
        OnPropertyChanged(nameof(RelatedColumns));
        OnPropertyChanged(nameof(DeleteAction));
        OnPropertyChanged(nameof(UpdateAction));
    }
}
