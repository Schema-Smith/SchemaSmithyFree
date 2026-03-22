using Schema.Domain;

namespace SchemaHammer.Models;

public class IndexedViewNodeModel : TreeNodeModel
{
    public IndexedView? IndexedViewData { get; set; }
    public TreeNodeModel[] IndexNodes { get; set; } = [];

    private bool _isExpanded;

    public void ExpandIndexedView(bool forceRefresh = false)
    {
        if (IndexedViewData == null) return;
        if (_isExpanded && !forceRefresh) return;

        Children.Clear();

        if (IndexNodes.Length > 0)
        {
            var container = new TreeNodeModel
            {
                Text = "Indexes",
                Tag = "Index Container",
                ImageKey = "folder",
                Parent = this,
                TemplateName = TemplateName
            };

            foreach (var child in IndexNodes)
            {
                child.Parent = container;
                container.Children.Add(child);
            }

            Children.Add(container);
        }

        _isExpanded = true;
    }
}
