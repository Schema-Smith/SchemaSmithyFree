// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.Domain;

namespace SchemaHammer.Models;

public class TableNodeModel : TreeNodeModel
{
    public Table? TableData { get; set; }

    public TreeNodeModel[] ColumnNodes { get; set; } = [];
    public TreeNodeModel[] IndexNodes { get; set; } = [];
    public TreeNodeModel[] XmlIndexNodes { get; set; } = [];
    public TreeNodeModel[] ForeignKeyNodes { get; set; } = [];
    public TreeNodeModel[] CheckConstraintNodes { get; set; } = [];
    public TreeNodeModel[] StatisticNodes { get; set; } = [];
    public TreeNodeModel[] FullTextIndexNodes { get; set; } = [];

    private bool _isTableExpanded;

    public void ExpandTable(bool forceRefresh = false)
    {
        if (TableData == null) return;
        if (_isTableExpanded && !forceRefresh) return;

        Children.Clear();

        AddContainer("Columns", "Column Container", ColumnNodes);
        AddContainer("Indexes", "Index Container", IndexNodes);
        AddContainer("Xml Indexes", "Xml Index Container", XmlIndexNodes);
        AddContainer("Foreign Keys", "Foreign Key Container", ForeignKeyNodes);
        AddContainer("Check Constraints", "Check Constraint Container", CheckConstraintNodes);
        AddContainer("Statistics", "Statistic Container", StatisticNodes);

        if (FullTextIndexNodes.Length > 0)
        {
            var ftNode = FullTextIndexNodes[0];
            ftNode.Parent = this;
            Children.Add(ftNode);
        }

        _isTableExpanded = true;
    }

    private void AddContainer(string text, string tag, TreeNodeModel[] childNodes)
    {
        if (childNodes.Length == 0) return;

        var container = new TreeNodeModel
        {
            Text = text,
            Tag = tag,
            ImageKey = "folder",
            Parent = this,
            TemplateName = TemplateName
        };

        foreach (var child in childNodes)
        {
            child.Parent = container;
            container.Children.Add(child);
        }

        Children.Add(container);
    }
}
