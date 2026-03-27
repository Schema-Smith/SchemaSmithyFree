// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
using Schema.Domain;
using Schema.Utility;
using DomainIndex = Schema.Domain.Index;

namespace SchemaHammer.FunctionalTests.Fixtures;

public class TestProductBuilder
{
    private string _name = "TestProduct";
    private readonly List<(string Name, Action<TemplateBuilder> Configure)> _templates = [];
    private readonly Dictionary<string, string> _scriptTokens = [];

    public TestProductBuilder WithName(string name) { _name = name; return this; }

    public TestProductBuilder WithTemplate(string templateName, Action<TemplateBuilder> configure)
    { _templates.Add((templateName, configure)); return this; }

    public TestProductBuilder WithScriptToken(string key, string value)
    { _scriptTokens[key] = value; return this; }

    public Product Build(string rootDirectory)
    {
        Directory.CreateDirectory(rootDirectory);
        var product = new Product
        {
            Name = _name,
            // ValidationScript is required by SchemaProperty but not used for UI tests
            ValidationScript = "SELECT 1"
        };
        foreach (var (key, value) in _scriptTokens)
            product.ScriptTokens[key] = value;

        var templatesRoot = Path.Combine(rootDirectory, "Templates");
        Directory.CreateDirectory(templatesRoot);

        foreach (var (templateName, configure) in _templates)
        {
            product.TemplateOrder.Add(templateName);
            var templateBuilder = new TemplateBuilder(templateName);
            configure(templateBuilder);
            templateBuilder.BuildTo(templatesRoot);
        }

        var productFilePath = Path.Combine(rootDirectory, "Product.json");
        JsonHelper.Write(productFilePath, product);
        product.FilePath = productFilePath;
        return product;
    }
}

public class TemplateBuilder
{
    private readonly string _templateName;
    private string _databaseIdentificationScript = "SELECT 1";
    private readonly List<(string Name, Action<TableBuilder> Configure)> _tables = [];
    private readonly List<string> _scriptFolders = [];
    private readonly List<(string FolderPath, string FileName, string Content)> _scripts = [];
    private readonly List<(string Name, string Schema, string Definition)> _indexedViews = [];

    internal TemplateBuilder(string templateName) { _templateName = templateName; }

    public TemplateBuilder WithDatabaseIdentificationScript(string script)
    { _databaseIdentificationScript = script; return this; }

    public TemplateBuilder WithTable(string tableName, Action<TableBuilder>? configure = null)
    { _tables.Add((tableName, configure ?? (_ => { }))); return this; }

    public TemplateBuilder WithScriptFolder(string folderPath)
    { _scriptFolders.Add(folderPath); return this; }

    public TemplateBuilder WithScript(string folderPath, string fileName, string content)
    { _scripts.Add((folderPath, fileName, content)); return this; }

    public TemplateBuilder WithIndexedView(string name, string schema, string definition)
    { _indexedViews.Add((name, schema, definition)); return this; }

    internal void BuildTo(string templatesRoot)
    {
        var templateDir = Path.Combine(templatesRoot, _templateName);
        Directory.CreateDirectory(templateDir);

        var template = new Template
        {
            Name = _templateName,
            DatabaseIdentificationScript = _databaseIdentificationScript
        };

        foreach (var folderPath in _scriptFolders)
            Directory.CreateDirectory(Path.Combine(templateDir, folderPath));

        foreach (var (folderPath, fileName, content) in _scripts)
        {
            var scriptDir = Path.Combine(templateDir, folderPath);
            Directory.CreateDirectory(scriptDir);
            File.WriteAllText(Path.Combine(scriptDir, fileName), content);
        }

        JsonHelper.Write(Path.Combine(templateDir, "Template.json"), template);

        var tablesDir = Path.Combine(templateDir, "Tables");
        Directory.CreateDirectory(tablesDir);

        foreach (var (tableName, configure) in _tables)
        {
            var tableBuilder = new TableBuilder(tableName);
            configure(tableBuilder);
            var table = tableBuilder.Build();
            var fileName = FileNameEncoder.Encode(tableName) + ".json";
            JsonHelper.Write(Path.Combine(tablesDir, fileName), table);
        }

        if (_indexedViews.Count > 0)
        {
            var indexedViewsDir = Path.Combine(templateDir, "Indexed Views");
            Directory.CreateDirectory(indexedViewsDir);
            foreach (var (name, schema, definition) in _indexedViews)
            {
                var iv = new IndexedView { Name = name, Schema = schema, Definition = definition };
                JsonHelper.Write(Path.Combine(indexedViewsDir, $"{schema}.{name}.json"), iv);
            }
        }
    }
}

public class TableBuilder
{
    private readonly string _tableName;
    private readonly List<Column> _columns = [];
    private readonly List<DomainIndex> _indexes = [];
    private readonly List<ForeignKey> _foreignKeys = [];
    private readonly List<CheckConstraint> _checkConstraints = [];
    private readonly List<XmlIndex> _xmlIndexes = [];
    private readonly List<Statistic> _statistics = [];
    private FullTextIndex? _fullTextIndex;

    internal TableBuilder(string tableName) { _tableName = tableName; }

    public TableBuilder WithColumn(string name, string dataType, bool nullable = true)
    { _columns.Add(new Column { Name = name, DataType = dataType, Nullable = nullable }); return this; }

    public TableBuilder WithColumn(string name, string dataType, bool nullable, string defaultValue)
    { _columns.Add(new Column { Name = name, DataType = dataType, Nullable = nullable, Default = defaultValue }); return this; }

    public TableBuilder WithIndex(string name, string indexColumns, bool unique = false)
    { _indexes.Add(new DomainIndex { Name = name, IndexColumns = indexColumns, Unique = unique }); return this; }

    public TableBuilder WithIndex(string name, string indexColumns, bool unique, byte fillFactor)
    { _indexes.Add(new DomainIndex { Name = name, IndexColumns = indexColumns, Unique = unique, FillFactor = fillFactor }); return this; }

    public TableBuilder WithClusteredIndex(string name, string indexColumns)
    { _indexes.Add(new DomainIndex { Name = name, IndexColumns = indexColumns, Clustered = true }); return this; }

    public TableBuilder WithForeignKey(string name, string columns, string relatedTable, string relatedColumns)
    { _foreignKeys.Add(new ForeignKey { Name = name, Columns = columns, RelatedTable = relatedTable, RelatedColumns = relatedColumns }); return this; }

    public TableBuilder WithCheckConstraint(string name, string expression)
    { _checkConstraints.Add(new CheckConstraint { Name = name, Expression = expression }); return this; }

    public TableBuilder WithXmlIndex(string name, string column, bool isPrimary = true)
    { _xmlIndexes.Add(new XmlIndex { Name = name, Column = column, IsPrimary = isPrimary }); return this; }

    public TableBuilder WithStatistic(string name, string columns)
    { _statistics.Add(new Statistic { Name = name, Columns = columns }); return this; }

    public TableBuilder WithStatistic(string name, string columns, byte sampleSize)
    { _statistics.Add(new Statistic { Name = name, Columns = columns, SampleSize = sampleSize }); return this; }

    public TableBuilder WithFullTextIndex(string columns, string fullTextCatalog, string keyIndex)
    { _fullTextIndex = new FullTextIndex { Columns = columns, FullTextCatalog = fullTextCatalog, KeyIndex = keyIndex }; return this; }

    internal Table Build()
    {
        var table = new Table { Name = _tableName };
        table.Columns.AddRange(_columns);
        table.Indexes.AddRange(_indexes);
        table.ForeignKeys.AddRange(_foreignKeys);
        table.CheckConstraints.AddRange(_checkConstraints);
        table.XmlIndexes.AddRange(_xmlIndexes);
        table.Statistics.AddRange(_statistics);
        if (_fullTextIndex != null) table.FullTextIndex = _fullTextIndex;
        return table;
    }
}
