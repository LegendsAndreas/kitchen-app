using Npgsql;

namespace WebKitchen;

public class DBService
{
    // jdbc:postgresql://[HOST]/[DATABASE_NAME]?password=[PASSWORD]&sslmode=require&user=[USERNAME]
    private readonly string _connectionString;

    public DBService(string connectionString)
    {
        Console.WriteLine("Conneting DBService:" + connectionString);
        _connectionString = connectionString;
    }

    // We then made a NpgsqlConnection, open it and then returns it.
    public NpgsqlConnection GetConnection()
    {
        NpgsqlConnection connection = new(_connectionString);
        connection.Open();
        return connection;
    }
}