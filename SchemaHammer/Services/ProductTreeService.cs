using Schema.Domain;
using Schema.Isolators;
using Schema.Utility;
using SchemaHammer.Models;

namespace SchemaHammer.Services;

public class ProductTreeService : IProductTreeService
{
    public Product? Product { get; private set; }
    public List<TreeNodeModel> SearchList { get; } = [];

    private string _productPath = "";

    public List<TreeNodeModel> LoadProduct(string productPath)
    {
        _productPath = productPath;
        SearchList.Clear();

        var productFile = Path.Combine(productPath, "Product.json");
        Product = JsonHelper.ProductLoad<Product>(productFile);
        Product.FilePath = productFile;

        var roots = new List<TreeNodeModel>();

        // Product-level before folders
        roots.AddRange(BuildProductScriptFolders(Product.BeforeFolders));

        // Templates container (lazy)
        roots.Add(BuildTemplatesContainer());

        // Product-level after folders
        roots.AddRange(BuildProductScriptFolders(Product.AfterFolders));

        return roots;
    }

    public List<TreeNodeModel> ReloadProduct()
    {
        if (string.IsNullOrEmpty(_productPath))
            return [];

        return LoadProduct(_productPath);
    }

    private List<TreeNodeModel> BuildProductScriptFolders(List<ProductFolder> folders)
    {
        var nodes = new List<TreeNodeModel>();
        foreach (var folder in folders)
        {
            var folderDir = Path.Combine(_productPath, folder.FolderPath);
            var node = BuildScriptFolderNode(folder.FolderPath, folderDir);
            if (node != null)
                nodes.Add(node);
        }
        return nodes;
    }

    private TreeNodeModel BuildTemplatesContainer()
    {
        var templatesDir = Path.Combine(_productPath, "Templates");
        var container = MakeNode("Templates", "Templates", "folder");

        var templateDirs = GetOrderedTemplateDirs(templatesDir);
        var templateNodes = new List<TreeNodeModel>();

        foreach (var templateDir in templateDirs)
        {
            var templateNode = BuildTemplateNode(templateDir);
            if (templateNode != null)
                templateNodes.Add(templateNode);
        }

        SetLazyChildren(container, templateNodes);
        return container;
    }

    private string[] GetOrderedTemplateDirs(string templatesDir)
    {
        var dir = ProductDirectoryWrapper.GetFromFactory();
        if (!dir.Exists(templatesDir))
            return [];

        var allDirs = dir.GetDirectories(templatesDir, "*", SearchOption.TopDirectoryOnly);

        if (Product?.TemplateOrder != null && Product.TemplateOrder.Count > 0)
        {
            var orderMap = Product.TemplateOrder
                .Select((name, idx) => (name, idx))
                .ToDictionary(x => x.name, x => x.idx, StringComparer.OrdinalIgnoreCase);

            return allDirs.OrderBy(d =>
            {
                var name = Path.GetFileName(d);
                return orderMap.TryGetValue(name, out var idx) ? idx : int.MaxValue;
            }).ThenBy(d => d).ToArray();
        }

        return allDirs.OrderBy(d => d).ToArray();
    }

    private TreeNodeModel? BuildTemplateNode(string templateDirPath)
    {
        var templateFile = Path.Combine(templateDirPath, "Template.json");
        var file = ProductFileWrapper.GetFromFactory();
        if (!file.Exists(templateFile))
            return null;

        var template = JsonHelper.ProductLoad<Template>(templateFile);
        var templateName = Path.GetFileName(templateDirPath);

        var templateNode = MakeNode(templateName, "Template", "template");
        templateNode.NodePath = templateFile;
        templateNode.TemplateName = templateName;

        var children = new List<TreeNodeModel>();

        // Before scripts
        AddScriptFolderNodes(children, templateDirPath, templateName,
            ["MigrationScripts/Before"]);

        // Objects scripts
        AddScriptFolderNodes(children, templateDirPath, templateName,
            ["Schemas", "DataTypes", "FullTextCatalogs", "FullTextStopLists",
             "XMLSchemaCollections", "Functions", "Views", "Procedures"]);

        // Tables container (lazy)
        children.Add(BuildTablesContainer(templateDirPath, templateName));

        // Indexed Views container (only if directory exists)
        var ivDir = Path.Combine(templateDirPath, "Indexed Views");
        if (ProductDirectoryWrapper.GetFromFactory().Exists(ivDir))
            children.Add(BuildIndexedViewsContainer(templateDirPath, templateName));

        // BetweenTablesAndKeys scripts
        AddScriptFolderNodes(children, templateDirPath, templateName,
            ["MigrationScripts/BetweenTablesAndKeys"]);

        // AfterTablesScripts
        AddScriptFolderNodes(children, templateDirPath, templateName,
            ["MigrationScripts/AfterTablesScripts"]);

        // AfterTablesObjects (Triggers, DDLTriggers)
        AddScriptFolderNodes(children, templateDirPath, templateName,
            ["Triggers", "DDLTriggers"]);

        // TableData
        AddScriptFolderNodes(children, templateDirPath, templateName,
            ["TableData"]);

        // After scripts
        AddScriptFolderNodes(children, templateDirPath, templateName,
            ["MigrationScripts/After"]);

        SetLazyChildren(templateNode, children);
        return templateNode;
    }

    private void AddScriptFolderNodes(List<TreeNodeModel> children, string templateDirPath,
        string templateName, string[] folderPaths)
    {
        foreach (var folderPath in folderPaths)
        {
            var folderDir = Path.Combine(templateDirPath, folderPath);
            var node = BuildScriptFolderNode(folderPath, folderDir);
            if (node != null)
            {
                node.TemplateName = templateName;
                children.Add(node);
            }
        }
    }

    private TreeNodeModel? BuildScriptFolderNode(string displayName, string folderDir)
    {
        var dir = ProductDirectoryWrapper.GetFromFactory();
        if (!dir.Exists(folderDir))
            return null;

        var sqlFiles = dir.GetFiles(folderDir, "*.sql", SearchOption.AllDirectories);
        if (sqlFiles.Length == 0)
            return null;

        var folderNode = MakeNode(displayName, "Sql Script FolderContainer", "folder");
        folderNode.NodePath = folderDir;

        var scriptNodes = sqlFiles
            .OrderBy(f => f)
            .Select(f =>
            {
                var scriptNode = MakeNode(Path.GetFileName(f), "Sql Script", "file");
                scriptNode.NodePath = f;
                return scriptNode;
            })
            .ToList();

        SetLazyChildren(folderNode, scriptNodes);
        return folderNode;
    }

    private TreeNodeModel BuildTablesContainer(string templateDirPath, string templateName)
    {
        var tablesDir = Path.Combine(templateDirPath, "Tables");
        var container = MakeNode("Tables", "Tables", "folder");
        container.NodePath = tablesDir;
        container.TemplateName = templateName;

        // Lazy expansion: load table JSON files on expand
        container.ExpandAction = () => ExpandTables(container, tablesDir, templateName);
        container.Children.Add(new TreeNodeModel { Text = "", Tag = "Placeholder" });

        return container;
    }

    private void ExpandTables(TreeNodeModel container, string tablesDir, string templateName)
    {
        var dir = ProductDirectoryWrapper.GetFromFactory();
        if (!dir.Exists(tablesDir))
            return;

        var jsonFiles = dir.GetFiles(tablesDir, "*.json", SearchOption.AllDirectories).OrderBy(f => f);

        foreach (var filePath in jsonFiles)
        {
            var table = Table.Load(filePath);
            var nodeText = Path.GetFileNameWithoutExtension(filePath);

            var tableNode = new TableNodeModel
            {
                Text = nodeText,
                Tag = "Table",
                NodePath = filePath,
                ImageKey = "file",
                TableData = table,
                TemplateName = templateName
            };
            SearchList.Add(tableNode);

            PopulateTableChildArrays(tableNode, templateName);

            tableNode.ExpandAction = () => tableNode.ExpandTable();
            tableNode.Children.Add(new TreeNodeModel { Text = "", Tag = "Placeholder" });

            tableNode.Parent = container;
            container.Children.Add(tableNode);
        }
    }

    private void PopulateTableChildArrays(TableNodeModel tableNode, string templateName)
    {
        if (tableNode.TableData == null) return;

        tableNode.ColumnNodes = tableNode.TableData.Columns
            .Select(c => MakeChildNode(TrimBrackets(c.Name), "Column", templateName))
            .ToArray();

        tableNode.IndexNodes = tableNode.TableData.Indexes
            .Select(i => MakeChildNode(TrimBrackets(i.Name), "Index", templateName))
            .ToArray();

        tableNode.XmlIndexNodes = tableNode.TableData.XmlIndexes
            .Select(x => MakeChildNode(TrimBrackets(x.Name), "Xml Index", templateName))
            .ToArray();

        tableNode.ForeignKeyNodes = tableNode.TableData.ForeignKeys
            .Select(fk => MakeChildNode(TrimBrackets(fk.Name), "Foreign Key", templateName))
            .ToArray();

        tableNode.CheckConstraintNodes = tableNode.TableData.CheckConstraints
            .Select(cc => MakeChildNode(TrimBrackets(cc.Name), "Check Constraint", templateName))
            .ToArray();

        tableNode.StatisticNodes = tableNode.TableData.Statistics
            .Select(s => MakeChildNode(TrimBrackets(s.Name), "Statistic", templateName))
            .ToArray();

        if (tableNode.TableData.FullTextIndex != null)
        {
            tableNode.FullTextIndexNodes =
            [
                MakeChildNode("Full Text Index", "Full Text Index", templateName)
            ];
        }
    }

    private TreeNodeModel BuildIndexedViewsContainer(string templateDirPath, string templateName)
    {
        var ivDir = Path.Combine(templateDirPath, "Indexed Views");
        var container = MakeNode("Indexed Views", "Indexed Views", "folder");
        container.NodePath = ivDir;
        container.TemplateName = templateName;

        // Lazy expansion
        container.ExpandAction = () => ExpandIndexedViews(container, ivDir, templateName);
        container.Children.Add(new TreeNodeModel { Text = "", Tag = "Placeholder" });

        return container;
    }

    private void ExpandIndexedViews(TreeNodeModel container, string ivDir, string templateName)
    {
        var dir = ProductDirectoryWrapper.GetFromFactory();
        if (!dir.Exists(ivDir))
            return;

        var jsonFiles = dir.GetFiles(ivDir, "*.json", SearchOption.AllDirectories).OrderBy(f => f);

        foreach (var filePath in jsonFiles)
        {
            var iv = JsonHelper.ProductLoad<IndexedView>(filePath);
            var nodeText = Path.GetFileNameWithoutExtension(filePath);

            var ivNode = new IndexedViewNodeModel
            {
                Text = nodeText,
                Tag = "Indexed View",
                NodePath = filePath,
                ImageKey = "file",
                IndexedViewData = iv,
                TemplateName = templateName
            };
            SearchList.Add(ivNode);

            ivNode.IndexNodes = iv.Indexes
                .Select(i => MakeChildNode(TrimBrackets(i.Name), "Index", templateName))
                .ToArray();

            ivNode.ExpandAction = () => ivNode.ExpandIndexedView();
            ivNode.Children.Add(new TreeNodeModel { Text = "", Tag = "Placeholder" });

            ivNode.Parent = container;
            container.Children.Add(ivNode);
        }
    }

    private TreeNodeModel MakeNode(string text, string tag, string imageKey)
    {
        var node = new TreeNodeModel
        {
            Text = text,
            Tag = tag,
            ImageKey = imageKey
        };
        SearchList.Add(node);
        return node;
    }

    private TreeNodeModel MakeChildNode(string text, string tag, string templateName)
    {
        var node = new TreeNodeModel
        {
            Text = text,
            Tag = tag,
            ImageKey = "file",
            TemplateName = templateName
        };
        SearchList.Add(node);
        return node;
    }

    private static string TrimBrackets(string name)
    {
        if (string.IsNullOrEmpty(name)) return name ?? "";
        return name.Trim('[', ']');
    }

    private static void SetLazyChildren(TreeNodeModel parent, List<TreeNodeModel> children)
    {
        if (children.Count == 0) return;

        parent.Children.Add(new TreeNodeModel { Text = "", Tag = "Placeholder" });
        parent.ExpandAction = () =>
        {
            foreach (var child in children)
            {
                child.Parent = parent;
                parent.Children.Add(child);
            }
        };
    }
}
