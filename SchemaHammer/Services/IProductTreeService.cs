using Schema.Domain;
using SchemaHammer.Models;

namespace SchemaHammer.Services;

public interface IProductTreeService
{
    Product? Product { get; }
    List<TreeNodeModel> SearchList { get; }
    List<TreeNodeModel> LoadProduct(string productPath);
    List<TreeNodeModel> ReloadProduct();
}
