using System.Data;

namespace Schema.Utility;

public static class ForgeKindler
{
    public static void KindleTheForge(IDbCommand command)
    {
        command.CommandText = ResourceLoader.Load("Kindling_SchemaSmith_Schema.sql");
        command.ExecuteNonQuery();

        command.CommandText = ResourceLoader.Load("SchemaSmith.fn_StripParenWrapping.sql");
        command.ExecuteNonQuery();

        command.CommandText = ResourceLoader.Load("SchemaSmith.fn_StripBracketWrapping.sql");
        command.ExecuteNonQuery();

        command.CommandText = ResourceLoader.Load("SchemaSmith.fn_SafeBracketWrap.sql");
        command.ExecuteNonQuery();

        command.CommandText = ResourceLoader.Load("SchemaSmith.TableQuench.sql");
        command.ExecuteNonQuery();

        command.CommandText = ResourceLoader.Load("Kindling_CompletedMigrations_Table.sql");
        command.ExecuteNonQuery();

        command.CommandText = ResourceLoader.Load("SchemaSmith.fn_FormatJson.sql");
        command.ExecuteNonQuery();

        command.CommandText = ResourceLoader.Load("SchemaSmith.GenerateTableJson.sql");
        command.ExecuteNonQuery();
    }
}