using System.Data;
using Microsoft.Data.SqlClient;
using Schema.Isolators;

namespace Schema.DataAccess;

public class SqlConnectionFactory : ISqlConnectionFactory
{
    public IDbConnection GetSqlConnection(string connectionString)
    {
        return new SqlConnection(connectionString);
    }

    public static ISqlConnectionFactory GetFromFactory()
    {
        return FactoryContainer.ResolveOrCreate<ISqlConnectionFactory, SqlConnectionFactory>();
    }
}