using System.IO;
using System.Linq;
using Schema.Domain;
using Schema.Isolators;

namespace Schema.Utility;

public static class RepositoryHelper
{
    public static void UpdateOrInitRepository(string productPath, string productName, string templateName, string dbName)
    {
        var file = FileWrapper.GetFromFactory();
        var directory = DirectoryWrapper.GetFromFactory();
        directory.CreateDirectory(Path.Combine(productPath, "Templates"));
        var productFile = Path.Combine(productPath, "Product.json");
        if (string.IsNullOrEmpty(productName)) productName = Path.GetFileName(productPath.TrimEnd(' ', '/', '\\'));
        if (string.IsNullOrEmpty(templateName)) templateName = dbName;
        var product = new Product { Name = productName, ValidationScript = "SELECT CAST(CASE WHEN EXISTS(SELECT * FROM master.sys.databases WHERE [Name] = '{{" + templateName + "Db}}') THEN 1 ELSE 0 END AS BIT)"};
        if (file.Exists(productFile))
            product = JsonHelper.Load<Product>(productFile) ?? product;
        else
            product.ScriptTokens.Add($"{templateName}Db", dbName);
        product.FilePath = productFile;
        if (product.TemplateOrder.All(t => !t.EqualsIgnoringCase(templateName))) product.TemplateOrder.Add(templateName);
        JsonHelper.Write(productFile, product);

        var schemaPath = Path.Combine(productPath, ".json-schemas");
        directory.CreateDirectory(schemaPath);
        WriteSchemaFile(schemaPath, "products.schema");
        WriteSchemaFile(schemaPath, "templates.schema");
        WriteSchemaFile(schemaPath, "tables.schema");
    }

    private static void WriteSchemaFile(string schemaPath, string fileName)
    {
        var file = FileWrapper.GetFromFactory();
        var schemaFile = Path.Combine(schemaPath, fileName);
        if (!file.Exists(schemaFile))
            file.WriteAllText(schemaFile, ResourceLoader.Load(fileName));
    }

    public static string UpdateOrInitTemplate(string productPath, string templateName, string dbName)
    {
        var file = FileWrapper.GetFromFactory();
        var directory = DirectoryWrapper.GetFromFactory();
        if (string.IsNullOrEmpty(templateName)) templateName = dbName;
        var templatePath = Path.Combine(productPath, "Templates", templateName);
        directory.CreateDirectory(templatePath);
        var templateFile = Path.Combine(templatePath, "Template.json");
        foreach (var folder in Template.GetTemplateFolders())
            directory.CreateDirectory(Path.Combine(templatePath, folder.FolderPath));
        if (!file.Exists(templateFile)) 
        {
            var template = new Template { Name = templateName, DatabaseIdentificationScript = "SELECT [Name] FROM master.sys.databases WHERE [Name] = '{{" + templateName + "Db}}'" };
            JsonHelper.Write(templateFile, template);
        }
        return templatePath;
    }
}