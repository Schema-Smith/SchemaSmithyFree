// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

namespace Schema.Domain
{
    public enum DatabaseScriptSlot
    {
        Before,
        Object,
        AfterTablesObject,
        BetweenTablesAndKeys,
        AfterTable,
        TableData,
        After
    }
}
