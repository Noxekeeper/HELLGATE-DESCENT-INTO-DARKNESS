using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace NoREroMod.Systems.Economy;

/// <summary>
/// Minimal regex-based JSON reader. Replaces UnityEngine.JsonUtility for the Economic module
/// because JsonUtility silently produces empty arrays / default-valued nested objects on
/// PowerShell-formatted JSON files used by HellGate (the same problem
/// <see cref="NoREroMod.Systems.Rewards.DropSystem"/> works around with its own regex
/// fallback). This parser is purpose-built for the Economic config shape — it is NOT a
/// general-purpose JSON parser.
///
/// Supports:
///   - scalar fields:        <c>"Key": 123 / 1.5 / true / "text"</c>
///   - nested objects:       <c>"Key": { ... }</c> returned as a body string for sub-parsing.
///   - array-of-objects:     <c>"Key": [ {...}, {...} ]</c> returned as a list of body strings.
///
/// Brace matching uses a string-aware counter so nested braces inside string literals are
/// not miscounted.
/// </summary>
internal static class EconomicJsonParser
{
    public static int ReadInt(string json, string key, int fallback)
    {
        Match m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(-?\\d+)", RegexOptions.CultureInvariant);
        if (!m.Success) return fallback;
        return int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;
    }

    public static float ReadFloat(string json, string key, float fallback)
    {
        Match m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(-?\\d+(?:\\.\\d+)?)", RegexOptions.CultureInvariant);
        if (!m.Success) return fallback;
        return float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;
    }

    public static bool ReadBool(string json, string key, bool fallback)
    {
        Match m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(true|false)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (!m.Success) return fallback;
        return string.Equals(m.Groups[1].Value, "true", System.StringComparison.OrdinalIgnoreCase);
    }

    public static string ReadString(string json, string key, string fallback)
    {
        Match m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.CultureInvariant);
        if (!m.Success) return fallback;
        return m.Groups[1].Value;
    }

    /// <summary>
    /// Returns the body string between the matching <c>{</c> and <c>}</c> for <c>"Key": { ... }</c>.
    /// The returned body can be passed to other read methods to parse its inner fields.
    /// Returns <c>null</c> if the key or block is missing.
    /// </summary>
    public static string ReadObjectBlock(string json, string key)
    {
        int idx = FindKeyIndex(json, key);
        if (idx < 0) return null;

        int braceStart = json.IndexOf('{', idx);
        if (braceStart < 0) return null;
        int braceEnd = MatchBracket(json, braceStart, '{', '}');
        if (braceEnd < 0) return null;

        return json.Substring(braceStart + 1, braceEnd - braceStart - 1);
    }

    /// <summary>
    /// Returns each <c>{...}</c> body inside <c>"Key": [ { ... }, { ... } ]</c>.
    /// Each body can be passed to <see cref="ReadInt"/> / <see cref="ReadFloat"/> / etc.
    /// </summary>
    public static List<string> ReadObjectArray(string json, string key)
    {
        var result = new List<string>();
        int idx = FindKeyIndex(json, key);
        if (idx < 0) return result;

        int bracketStart = json.IndexOf('[', idx);
        if (bracketStart < 0) return result;
        int bracketEnd = MatchBracket(json, bracketStart, '[', ']');
        if (bracketEnd < 0) return result;

        // Walk inside the array, collecting each balanced `{ ... }` block.
        int cursor = bracketStart + 1;
        while (cursor < bracketEnd)
        {
            int objStart = json.IndexOf('{', cursor);
            if (objStart < 0 || objStart >= bracketEnd) break;
            int objEnd = MatchBracket(json, objStart, '{', '}');
            if (objEnd < 0 || objEnd > bracketEnd) break;
            result.Add(json.Substring(objStart + 1, objEnd - objStart - 1));
            cursor = objEnd + 1;
        }
        return result;
    }

    private static int FindKeyIndex(string json, string key)
    {
        Match m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:", RegexOptions.CultureInvariant);
        return m.Success ? m.Index : -1;
    }

    private static int MatchBracket(string text, int openIndex, char open, char close)
    {
        int depth = 0;
        bool inString = false;
        bool escape = false;
        for (int i = openIndex; i < text.Length; i++)
        {
            char ch = text[i];
            if (escape) { escape = false; continue; }
            if (ch == '\\') { escape = true; continue; }
            if (ch == '"') { inString = !inString; continue; }
            if (inString) continue;

            if (ch == open) depth++;
            else if (ch == close)
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }
}
