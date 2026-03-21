// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
using System.Data;

namespace Schema.Utility;

public static class ForgeKindler
{
    public static void KindleTheForge(IDbCommand command)
    {
        KindleOneFile(command, "Kindling_SchemaSmith_Schema.sql");

        KindleOneFile(command, "SchemaSmith.fn_StripParenWrapping.sql");
        KindleOneFile(command, "SchemaSmith.fn_StripBracketWrapping.sql");
        KindleOneFile(command, "SchemaSmith.fn_SafeBracketWrap.sql");
        KindleOneFile(command, "SchemaSmith.PrintWithNoWait.sql");

        KindleOneFile(command, "SchemaSmith.MissingTableAndColumnQuench.sql");
        KindleOneFile(command, "SchemaSmith.ModifiedTableQuench.sql");
        KindleOneFile(command, "SchemaSmith.MissingIndexesAndConstraintsQuench.sql");
        KindleOneFile(command, "SchemaSmith.ForeignKeyQuench.sql");
        KindleOneFile(command, "SchemaSmith.TableQuench.sql", replaceParseJsonToken: true);

        KindleOneFile(command, "Kindling_CompletedMigrations_Table.sql");
        KindleOneFile(command, "SchemaSmith.fn_FormatJson.sql");
        KindleOneFile(command, "SchemaSmith.GenerateTableJson.sql");
    }

    public static string GetParseTableJsonScript()
    {
        return ResourceLoader.Load("ParseTableJsonIntoTempTables.sql");
    }

    private static void KindleOneFile(IDbCommand command, string fileName, bool replaceParseJsonToken = false)
    {
        var script = ResourceLoader.Load(fileName);

        if (replaceParseJsonToken)
            script = script.Replace("{{ParseJson}}", ResourceLoader.Load("ParseTableJsonIntoTempTables.sql"));

        command.CommandText = script;
        command.ExecuteNonQuery();
    }
}
