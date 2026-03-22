using SchemaHammer.Models;

namespace SchemaHammer.Services;

public interface INavigationService
{
    bool CanGoBack { get; }
    IReadOnlyList<TreeNodeModel> History { get; }
    void Push(TreeNodeModel node);
    TreeNodeModel? Pop();
    TreeNodeModel? NavigateTo(int index);
    void Clear();
}
