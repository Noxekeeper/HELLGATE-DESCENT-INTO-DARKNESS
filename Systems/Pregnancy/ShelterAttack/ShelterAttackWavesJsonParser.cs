using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace NoREroMod.Systems.Pregnancy.ShelterAttack;

/// <summary>Regex JSON helpers for <c>waves.json</c> (same approach as EconomicJsonParser).</summary>
internal static class ShelterAttackWavesJsonParser
{
    internal static int ReadInt(string json, string key, int fallback)
    {
        Match m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(-?\\d+)", RegexOptions.CultureInvariant);
        if (!m.Success)
            return fallback;

        return int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;
    }

    internal static float ReadFloat(string json, string key, float fallback)
    {
        Match m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(-?\\d+(?:\\.\\d+)?)", RegexOptions.CultureInvariant);
        if (!m.Success)
            return fallback;

        return float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;
    }

    internal static string ReadString(string json, string key)
    {
        Match m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.CultureInvariant);
        return m.Success ? m.Groups[1].Value : string.Empty;
    }

    internal static string ReadObjectBlock(string json, string key)
    {
        int idx = FindKeyIndex(json, key);
        if (idx < 0)
            return null;

        int braceStart = json.IndexOf('{', idx);
        if (braceStart < 0)
            return null;

        int braceEnd = MatchBracket(json, braceStart, '{', '}');
        if (braceEnd < 0)
            return null;

        return json.Substring(braceStart + 1, braceEnd - braceStart - 1);
    }

    internal static List<string> ReadObjectArray(string json, string key)
    {
        var result = new List<string>();
        int idx = FindKeyIndex(json, key);
        if (idx < 0)
            return result;

        int bracketStart = json.IndexOf('[', idx);
        if (bracketStart < 0)
            return result;

        int bracketEnd = MatchBracket(json, bracketStart, '[', ']');
        if (bracketEnd < 0)
            return result;

        int cursor = bracketStart + 1;
        while (cursor < bracketEnd)
        {
            int objStart = json.IndexOf('{', cursor);
            if (objStart < 0 || objStart >= bracketEnd)
                break;

            int objEnd = MatchBracket(json, objStart, '{', '}');
            if (objEnd < 0 || objEnd > bracketEnd)
                break;

            result.Add(json.Substring(objStart + 1, objEnd - objStart - 1));
            cursor = objEnd + 1;
        }

        return result;
    }

    internal static List<string> ReadStringArray(string json, string key)
    {
        var result = new List<string>();
        int idx = FindKeyIndex(json, key);
        if (idx < 0)
            return result;

        int bracketStart = json.IndexOf('[', idx);
        if (bracketStart < 0)
            return result;

        int bracketEnd = MatchBracket(json, bracketStart, '[', ']');
        if (bracketEnd < 0)
            return result;

        string slice = json.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);
        MatchCollection matches = Regex.Matches(slice, "\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.CultureInvariant);
        for (int i = 0; i < matches.Count; i++)
            result.Add(matches[i].Groups[1].Value);

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
            if (escape)
            {
                escape = false;
                continue;
            }

            if (ch == '\\')
            {
                escape = true;
                continue;
            }

            if (ch == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
                continue;

            if (ch == open)
                depth++;
            else if (ch == close)
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }

        return -1;
    }
}
