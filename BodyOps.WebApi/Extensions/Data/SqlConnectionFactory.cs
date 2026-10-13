using Microsoft.Data.SqlClient;

namespace BodyOps.WebApi.Extensions.Data;

/// <summary>
/// Opens connections to the BodyOps SQL Server database.
/// </summary>
internal interface ISqlConnectionFactory
{
    /// <summary>
    /// Opens a new connection. The caller disposes it.
    /// </summary>
    /// <returns>An open connection.</returns>
    Task<SqlConnection> OpenAsync();
}

/// <summary>
/// Connections from the "BodyOps" connection string (env <c>ConnectionStrings__BodyOps</c>).
/// </summary>
internal sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    /// <summary>The name of the connection string.</summary>
    public const string ConnectionStringName = "BodyOps";

    private readonly string _connectionString;

    /// <summary>
    /// Reads the connection string.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    public SqlConnectionFactory(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:{ConnectionStringName} is empty (env ConnectionStrings__{ConnectionStringName}).");
        }

        _connectionString = new SqlConnectionStringBuilder(connectionString)
        {
            ApplicationName = "BodyOps",
        }.ConnectionString;
    }

    /// <inheritdoc />
    public async Task<SqlConnection> OpenAsync()
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }
}
