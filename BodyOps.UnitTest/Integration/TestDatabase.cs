using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace BodyOps.UnitTest.Integration;

/// <summary>
/// A throw-away SQL Server database built from BodyOps.Database's table, view and stored procedure scripts,
/// dropped on dispose. Needs BODYOPS_TEST_SQL (a connection string to the server, e.g. the CI service container).
/// </summary>
internal sealed partial class TestDatabase : IDisposable
{
    /// <summary>The environment variable with the server connection string.</summary>
    public const string EnvironmentVariable = "BODYOPS_TEST_SQL";

    // Tables in foreign-key order; every stored procedure after them.
    private static readonly string[] TableOrder = ["User", "UserSetting", "PlanMeal", "PlanFood", "PlanExercise", "PlanDay", "Day", "DayMeal", "DayExercise", "DaySet", "WeightEntry", "Food", "PushSubscription", "PushSent"];

    public TestDatabase()
    {
        Name = $"BodyOpsTest_{Guid.NewGuid():N}";
        using (var master = new SqlConnection(ServerConnectionString))
        {
            master.Open();
            Execute(master, $"CREATE DATABASE [{Name}] COLLATE SQL_Latin1_General_CP1_CI_AS;");
        }

        ConnectionString = new SqlConnectionStringBuilder(ServerConnectionString) { InitialCatalog = Name }.ConnectionString;
        using var database = new SqlConnection(ConnectionString);
        database.Open();
        foreach (var file in ScriptFiles())
        {
            foreach (var batch in GoSeparator().Split(File.ReadAllText(file)).Where(static batch => !string.IsNullOrWhiteSpace(batch)))
            {
                Execute(database, batch);
            }
        }
    }

    /// <summary>Gets the server connection string, or null when the integration tests are off.</summary>
    public static string? ServerConnectionString => Environment.GetEnvironmentVariable(EnvironmentVariable);

    /// <summary>Gets the database name.</summary>
    public string Name { get; }

    /// <summary>Gets the connection string of the new database.</summary>
    public string ConnectionString { get; }

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SqlConnection.ClearAllPools();
        using var master = new SqlConnection(ServerConnectionString);
        master.Open();
        Execute(master, $"IF DB_ID(N'{Name}') IS NOT NULL BEGIN ALTER DATABASE [{Name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{Name}]; END");
    }

    /// <summary>Runs one statement against the database (for test setup and checks).</summary>
    public object? Scalar(string sql)
    {
        using var connection = new SqlConnection(ConnectionString);
        connection.Open();
        using var command = new SqlCommand(sql, connection);
        return command.ExecuteScalar();
    }

    private static void Execute(SqlConnection connection, string sql)
    {
        using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        command.ExecuteNonQuery();
    }

    private static IEnumerable<string> ScriptFiles()
    {
        var root = Path.Combine(RepositoryRoot(), "BodyOps.Database");
        foreach (var table in TableOrder)
        {
            yield return Path.Combine(root, "Tables", $"dbo.{table}.sql");
        }

        foreach (var view in Directory.GetFiles(Path.Combine(root, "Views"), "*.sql").Order(StringComparer.Ordinal))
        {
            yield return view;
        }

        foreach (var procedure in Directory.GetFiles(Path.Combine(root, "StoredProcedures"), "*.sql", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            yield return procedure;
        }
    }

    private static string RepositoryRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "BodyOps.slnx")))
            {
                return folder.FullName;
            }
        }

        throw new InvalidOperationException("BodyOps.slnx not found above " + AppContext.BaseDirectory);
    }

    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex GoSeparator();
}

/// <summary>A fact that runs only when <see cref="TestDatabase.EnvironmentVariable"/> is set.</summary>
public sealed class SqlFactAttribute : FactAttribute
{
    public SqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(TestDatabase.ServerConnectionString))
        {
            Skip = $"Set {TestDatabase.EnvironmentVariable} to run the SQL Server integration tests.";
        }
    }
}

/// <summary>A theory that runs only when <see cref="TestDatabase.EnvironmentVariable"/> is set.</summary>
public sealed class SqlTheoryAttribute : TheoryAttribute
{
    public SqlTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(TestDatabase.ServerConnectionString))
        {
            Skip = $"Set {TestDatabase.EnvironmentVariable} to run the SQL Server integration tests.";
        }
    }
}
