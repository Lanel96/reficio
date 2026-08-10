using System.Text.RegularExpressions;
using Reficio.Models;

namespace Reficio.Services;

public class CorrectionModule
{
    private readonly FirebirdDbService _db;
    public string TableName { get; }
    public List<string> Columns { get; set; } = new();
    
    private static readonly Regex ValidIdentifier = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
    private static readonly HashSet<string> AllowedTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "DINGR", "MPACI"
    };
    
    public CorrectionModule(FirebirdDbService db, string tableName)
    {
        _db = db;
        if (!IsValidTableName(tableName))
            throw new ArgumentException($"Tabla no permitida: {tableName}", nameof(tableName));
        TableName = tableName;
    }
    
    private static bool IsValidTableName(string name)
        => AllowedTables.Contains(name);
    
    private static bool IsValidIdentifier(string name)
        => ValidIdentifier.IsMatch(name);
    
    private void ValidateColumn(string column)
    {
        if (!IsValidIdentifier(column))
            throw new ArgumentException($"Nombre de columna inválido: {column}", nameof(column));
        if (!Columns.Contains(column, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"Columna no existe en la tabla: {column}", nameof(column));
    }
    
    public void LoadColumns() => Columns = _db.GetColumns(TableName);
    
    public SearchResult SearchExact(string field, string value)
    {
        ValidateColumn(field);
        var r = _db.Query($"SELECT * FROM {TableName} WHERE {field} = @p0", value);
        return MapResult(r);
    }
    
    public SearchResult SearchByMultiple(Dictionary<string, object?> fields)
    {
        var conds = new List<string>();
        var vals = new List<object?>();
        int i = 0;
        foreach (var (f, v) in fields)
        {
            ValidateColumn(f);
            conds.Add($"{f} LIKE @p{i}");
            vals.Add($"%{v}%");
            i++;
        }
        var r = _db.Query($"SELECT * FROM {TableName} WHERE {string.Join(" AND ", conds)}", vals.ToArray());
        return MapResult(r);
    }
    
    public void UpdateRecord(string idField, object idValue, Dictionary<string, object?> updates)
    {
        if (updates.Count == 0) throw new InvalidOperationException("No hay campos para actualizar");
        ValidateColumn(idField);
        
        var sets = new List<string>();
        var vals = new List<object?>();
        int i = 0;
        foreach (var (f, v) in updates)
        {
            ValidateColumn(f);
            sets.Add($"{f} = @p{i}");
            vals.Add(v);
            i++;
        }
        vals.Add(idValue);
        _db.Execute($"UPDATE {TableName} SET {string.Join(", ", sets)} WHERE {idField} = @p{i}", vals.ToArray());
    }
    
    public int GetRecordCount()
    {
        var r = _db.Query($"SELECT COUNT(*) FROM {TableName}");
        return r.Count > 0 && r.Rows[0].Values.First() is int c ? c : 0;
    }
    
    private static SearchResult MapResult(QueryResult qr) => new() { Records = qr.Rows.ToList() };
}