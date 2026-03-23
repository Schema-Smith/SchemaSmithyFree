// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using SchemaHammer.Models;

namespace SchemaHammer.ViewModels.Editors;

public class StatisticEditorViewModel : EditorBaseViewModel
{
    public override string EditorTitle => Name;
    public string Name { get; private set; } = "";
    public string Columns { get; private set; } = "";
    public byte SampleSize { get; private set; }
    public string FilterExpression { get; private set; } = "";

    public override void ChangeNode(TreeNodeModel node)
    {
        base.ChangeNode(node);
        var table = ColumnEditorViewModel.FindParentTable(node);
        var stat = table?.Statistics.FirstOrDefault(s => NameMatchesNodeText(s.Name, node.Text));
        if (stat != null)
        {
            Name = StripBrackets(stat.Name);
            Columns = stat.Columns ?? "";
            SampleSize = stat.SampleSize;
            FilterExpression = stat.FilterExpression ?? "";
        }
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Columns));
        OnPropertyChanged(nameof(SampleSize));
        OnPropertyChanged(nameof(FilterExpression));
    }
}
