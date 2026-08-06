namespace Reficio.Models;

public class SearchResult
{
    public List<Dictionary<string, object?>> Records { get; set; } = new();
    public int Count => Records.Count;
}
