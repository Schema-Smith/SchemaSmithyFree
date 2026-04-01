// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

namespace Schema.Domain;

public enum TemplateQuenchSlot : ushort
{
    Before,
    Objects,
    BetweenTablesAndKeys,
    AfterTablesScripts,
    AfterTablesObjects,
    TableData,
    After,
    None
}
