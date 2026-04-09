// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Schema.Domain
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum CheckConstraintStyle
    {
        ColumnLevel,    // Default — current behavior (SQL Server splits column vs table)
        TableLevel      // All check constraints to Table.CheckConstraints[] with names
    }
}
