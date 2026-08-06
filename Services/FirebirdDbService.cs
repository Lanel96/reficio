using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Reficio.Models;

namespace Reficio.Services;

public class FirebirdDbService : IDisposable
{
    private FbConnection? _connection;

    public string Host { get; }
    public int Port { get; }
    public string Path { get; }
    public string User { get; }
    public string Password { get; }

    public FirebirdDbService(string dbPath, string user, string password)
    {
        Host = "localhost";
        Port = 3050;
        Path = dbPath;
        User = user;
        Password = password;
    }

    private string GetConnectionString()
        => $"User={User};Password={Password};Database={Path};DataSource={Host};Port={Port};Dialect=3;Pooling=true;MaxPoolSize=10;";

    private FbConnection GetConnection()
    {
        if (_connection != null && _connection.State == ConnectionState.Open)
            return _connection;

        _connection = new FbConnection(GetConnectionString());
        _connection.Open();
        return _connection;
    }

    public void TestConnection()
    {
        using var conn = new FbConnection(GetConnectionString());
        conn.Open();
    }

    public QueryResult Query(string sql, params object?[] args)
    {
        var conn = GetConnection();
        using var cmd = new FbCommand(sql, conn);
        for (int i = 0; i < args.Length; i++)
            cmd.Parameters.AddWithValue($"@p{i}", args[i] ?? DBNull.Value);

        using var reader = cmd.ExecuteReader();
        var columns = new List<string>();
        for (int i = 0; i < reader.FieldCount; i++)
            columns.Add(reader.GetName(i));

        var result = new QueryResult { Columns = columns };
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < columns.Count; i++)
                row[columns[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            result.Rows.Add(row);
        }
        return result;
    }

    public int Execute(string sql, params object?[] args)
    {
        var conn = GetConnection();
        using var cmd = new FbCommand(sql, conn);
        for (int i = 0; i < args.Length; i++)
            cmd.Parameters.AddWithValue($"@p{i}", args[i] ?? DBNull.Value);
        return cmd.ExecuteNonQuery();
    }

    public List<string> GetTables()
    {
        var result = Query("SELECT RDB$RELATION_NAME FROM RDB$RELATIONS WHERE RDB$SYSTEM_FLAG = 0 ORDER BY RDB$RELATION_NAME");
        return result.Rows.Select(r => r["RDB$RELATION_NAME"]?.ToString() ?? "").ToList();
    }

    public List<string> GetColumns(string table)
    {
        var result = Query("SELECT RDB$FIELD_NAME FROM RDB$RELATION_FIELDS WHERE RDB$RELATION_NAME = @p0 ORDER BY RDB$FIELD_POSITION", table);
        return result.Rows.Select(r => r["RDB$FIELD_NAME"]?.ToString() ?? "").ToList();
    }

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
        GC.SuppressFinalize(this);
    }

    public static void ClearAllPools()
    {
        FbConnection.ClearAllPools();
    }
}
