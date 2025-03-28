namespace Schema.DataAccess;

public static class ConnectionString
{
    public static string Build(string serverName, string dbName, string user, string password)
    {
        var security = !string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(password) ? $"User ID={user};Password={password}" : "Integrated Security=True";

        return $"data source={serverName};Initial Catalog={dbName};{security};ApplicationIntent=ReadWrite;TrustServerCertificate=True;";
    }
}