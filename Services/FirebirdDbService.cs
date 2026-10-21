using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Reficio.Models;

namespace Reficio.Services;

public class FirebirdDbService : IDisposable
{
    private readonly string _connectionString;
    private bool _disposed;
    
    public string Host { get; }
    public int Port { get; }
    public string Path { get; }
    public string User { get; }
    public string Password { get; }
    
    public FirebirdDbService(string dbPath, string user, string password)
        : this("localhost", 3050, dbPath, user, password)
    {
    }
    
    public FirebirdDbService(string host, int port, string dbPath, string user, string password)
    {
        Host = host;
        Port = port;
        Path = dbPath;
        User = user;
        Password = password;
        _connectionString = BuildConnectionString();
    }
    
    private string BuildConnectionString()
    {
        var builder = new FbConnectionStringBuilder
        {
            UserID = User,
            Password = Password,
            Database = Path,
            DataSource = Host,
            Port = Port,
            Dialect = 3,
            Pooling = true,
            MinPoolSize = 0,
            MaxPoolSize = 10,
            ConnectionLifeTime = 300,
            ConnectionTimeout = 15
        };
        return builder.ToString();
    }
    
    private FbConnection CreateConnection()
    {
        var conn = new FbConnection(_connectionString);
        conn.Open();
        return conn;
    }
    
    public void TestConnection()
    {
        using var conn = CreateConnection();
    }
    
    public QueryResult Query(string sql, params object?[] args)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        using var conn = CreateConnection();
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
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        using var conn = CreateConnection();
        using var cmd = new FbCommand(sql, conn);
        for (int i = 0; i < args.Length; i++)
            cmd.Parameters.AddWithValue($"@p{i}", args[i] ?? DBNull.Value);
        return cmd.ExecuteNonQuery();
    }
    
    public List<string> GetTables()
    {
        var result = Query("SELECT TRIM(RDB$RELATION_NAME) AS RELATION_NAME FROM RDB$RELATIONS WHERE RDB$SYSTEM_FLAG = 0 ORDER BY RELATION_NAME");
        return result.Rows.Select(r => r["RELATION_NAME"]?.ToString() ?? "").ToList();
    }
    
    public List<string> GetColumns(string table)
    {
        var result = Query("SELECT TRIM(RDB$FIELD_NAME) AS FIELD_NAME FROM RDB$RELATION_FIELDS WHERE TRIM(RDB$RELATION_NAME) = @p0 ORDER BY RDB$FIELD_POSITION", table);
        return result.Rows.Select(r => r["FIELD_NAME"]?.ToString() ?? "").ToList();
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
    
    public static void ClearAllPools()
    {
        FbConnection.ClearAllPools();
    }
}