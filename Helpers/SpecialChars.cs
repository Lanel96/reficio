using System.Text.RegularExpressions;

namespace Reficio.Helpers;

public static class SpecialChars
{
    private static readonly Dictionary<char, string> Map = new()
    {
        ['À'] = "A", ['Á'] = "A", ['Â'] = "A", ['Ã'] = "A", ['Ä'] = "A",
        ['à'] = "a", ['á'] = "a", ['â'] = "a", ['ã'] = "a", ['ä'] = "a",
        ['È'] = "E", ['É'] = "E", ['Ê'] = "E", ['Ë'] = "E",
        ['è'] = "e", ['é'] = "e", ['ê'] = "e", ['ë'] = "e",
        ['Í'] = "I", ['Ó'] = "O", ['Ú'] = "U",
        ['í'] = "i", ['ó'] = "o", ['ú'] = "u",
        ['Ñ'] = "N", ['ñ'] = "n", ['Ç'] = "C", ['ç'] = "c",
        ['Ü'] = "U", ['ü'] = "u",
    };

    public static string Normalize(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (var c in text) sb.Append(Map.TryGetValue(c, out var r) ? r : c.ToString());
        return Regex.Replace(sb.ToString(), @"  +", " ").Trim();
    }
}
