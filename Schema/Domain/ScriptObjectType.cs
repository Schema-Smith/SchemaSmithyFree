// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Schema.Domain
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ScriptObjectType
    {
        None,               // Before Scripts, After Scripts, Table Data, custom folders
        Views,
        Functions,
        Procedures,
        Triggers,
        Schemas,
        // SQL Server
        DataTypes,
        FullTextCatalogs,
        FullTextStopLists,
        XMLSchemaCollections,
        DDLTriggers,
        IndexedViews,
        Synonyms,
        // PostgreSQL
        DomainTypes,
        EnumTypes,
        CompositeTypes,
        TriggerFunctions,
        WindowFunctions,
        Aggregates,
        Sequences,
        Rules,
        MaterializedViews,
        Collations,
        Publications,
        // MySQL
        Events
    }
}
