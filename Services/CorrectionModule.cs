using Reficio.Models;

namespace Reficio.Services;

public class CorrectionModule
{
    private readonly FirebirdDbService _db;
    public string TableName { get; }
    public List<string> Columns { get; set; } = new();

    public CorrectionModule(FirebirdDbService db, string tableName)
    { _db = db; TableName = tableName; }

    public void LoadColumns() => Columns = _db.GetColumns(TableName);

    public SearchResult SearchExact(string field, string value)
    { var r = _db.Query($"SELECT * FROM {TableName} WHERE {field} = @p0", value); return MapResult(r); }

    public SearchResult SearchByMultiple(Dictionary<string, object?> fields)
    {
        var conds = new List<string>(); var vals = new List<object?>(); int i = 0;
        foreach (var (f, v) in fields) { conds.Add($"{f} LIKE @p{i}"); vals.Add($"%{v}%"); i++; }
        var r = _db.Query($"SELECT * FROM {TableName} WHERE {string.Join(" AND ", conds)}", vals.ToArray());
        return MapResult(r);
    }

    public void UpdateRecord(string idField, object idValue, Dictionary<string, object?> updates)
    {
        if (updates.Count == 0) throw new InvalidOperationException("No hay campos para actualizar");
        var sets = new List<string>(); var vals = new List<object?>(); int i = 0;
        foreach (var (f, v) in updates) { sets.Add($"{f} = @p{i}"); vals.Add(v); i++; }
        vals.Add(idValue);
        _db.Execute($"UPDATE {TableName} SET {string.Join(", ", sets)} WHERE {idField} = @p{i}", vals.ToArray());
    }

    public int GetRecordCount()
    { var r = _db.Query($"SELECT COUNT(*) FROM {TableName}"); return r.Count > 0 && r.Rows[0].Values.First() is int c ? c : 0; }

    private static SearchResult MapResult(QueryResult qr) => new() { Records = qr.Rows.ToList() };
}
