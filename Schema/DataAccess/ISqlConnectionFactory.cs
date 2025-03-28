using System.Data;

namespace Schema.DataAccess;

public interface ISqlConnectionFactory
{
    IDbConnection GetSqlConnection(string connectionString);
}