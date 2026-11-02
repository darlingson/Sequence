namespace Sequence.Data;
using Npgsql;
using Microsoft.Extensions.Configuration;
public interface IDbConnectionFactory
{
    NpgsqlConnection CreateConnection();
}

public class PostgresConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public PostgresConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
    }

    public NpgsqlConnection CreateConnection()
    {
        return new NpgsqlConnection(_connectionString);
    }
}