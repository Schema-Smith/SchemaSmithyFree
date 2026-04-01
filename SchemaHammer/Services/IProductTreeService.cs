// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Schema.Domain;
using SchemaHammer.Models;

namespace SchemaHammer.Services;

public interface IProductTreeService
{
    Product? Product { get; }
    List<TreeNodeModel> SearchList { get; }
    Dictionary<string, Template> Templates { get; }
    List<TreeNodeModel> LoadProduct(string productPath);
    List<TreeNodeModel> ReloadProduct();
}
