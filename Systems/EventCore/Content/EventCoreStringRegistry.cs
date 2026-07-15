using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using NoREroMod;
using NoREroMod.Systems.EventCore.Core;
using NoREroMod.Systems.EventCore.Handlers;

namespace NoREroMod.Systems.EventCore.Content;

[Serializable]
internal sealed class EventCoreStringsFile
{
    public EventCoreStringEntry[] entries = new EventCoreStringEntry[0];
}

[Serializable]
internal sealed class EventCoreStringEntry
{
    public string key = string.Empty;
    public string text = string.Empty;
}

[Serializable]
internal sealed class EventCoreLangPackRoot
{
    public EventCoreStringEntry[] entries = new EventCoreStringEntry[0];
    public EventCoreLinePoolDef[] linePools = new EventCoreLinePoolDef[0];
}

[Serializable]
internal sealed class EventCoreLinePoolDef
{
    public string poolId = string.Empty;
    public string[] lines = new string[0];
}

/// <summary>
/// Localized keys and random line pools loaded from the active language pack.
/// Shared legacy keys from <c>strings_default.json</c> are merged without overriding language-specific values.
/// </summary>
internal static class EventCoreStringRegistry
{
    private static readonly Dictionary<string, string> Map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string[]> Pools = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

    internal static void ReloadFromDisk()
    {
        Map.Clear();
        Pools.Clear();
        try
        {
            if (!Directory.Exists(EventCorePaths.JsonRoot))
                return;

            string langPackPath = EventCoreLanguage.GetLanguagePackPath(EventCorePaths.JsonRoot);
            if (!File.Exists(langPackPath))
            {
                Plugin.Log?.LogError($"[EventCore] Language pack missing: {langPackPath}");
                return;
            }

            LoadLangPack(File.ReadAllText(langPackPath), langPackPath);
            MergeFactionSocialLangPacks(EventCorePaths.JsonRoot, EventCoreLanguage.ResolveFolderCode());

            string legacyPath = EventCorePaths.ResolveRootFile("strings_default.json");
            if (File.Exists(legacyPath))
                MergeLegacyEntries(File.ReadAllText(legacyPath), legacyPath);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[EventCore] Language pack load failed: {ex.Message}");
        }
    }

    private static void LoadLangPack(string raw, string pathForLog)
    {
        var pack = JsonUtility.FromJson<EventCoreLangPackRoot>(raw);
        IngestEntries(pack?.entries, replaceExisting: true);

        var pools = pack?.linePools;
        if (pools != null && pools.Length > 0)
        {
            for (int i = 0; i < pools.Length; i++)
            {
                EventCoreLinePoolDef p = pools[i];
                if (p == null || string.IsNullOrEmpty(p.poolId))
                    continue;
                string id = p.poolId.Trim();
                if (id.Length == 0)
                    continue;
                string[] lines = p.lines;
                if (lines == null || lines.Length == 0)
                    continue;
                if (!Pools.ContainsKey(id))
                    Pools.Add(id, lines);
            }
        }

        if (Map.Count == 0 && (pack?.entries == null || pack.entries.Length == 0))
            TryManualLoadEntries(raw);

        if (Map.Count == 0 && Pools.Count == 0)
            Plugin.Log?.LogWarning($"[EventCore] eventcore_lang.json parsed empty (lang={EventCoreLanguage.ResolveFolderCode()}). Path: {pathForLog}");

        // Unity 5 JsonUtility often drops linePools[] — always run manual supplement (FSP packs rely on this).
        TryManualIngestLinePools(raw, pathForLog);
    }

    /// <summary>
    /// Supplements <see cref="JsonUtility"/> for larger language packs where array fields may deserialize incompletely.
    /// </summary>
    private static void TryManualIngestLinePools(string raw, string pathForLog)
    {
        if (string.IsNullOrEmpty(raw))
            return;

        int before = Pools.Count;
        int arrayStart = FindArrayPropertyStart(raw, "linePools");
        if (arrayStart < 0)
            return;

        int i = arrayStart + 1;
        while (i < raw.Length)
        {
            SkipJsonWhitespace(raw, ref i);
            if (i >= raw.Length || raw[i] == ']')
                break;
            if (raw[i] == ',')
            {
                i++;
                continue;
            }
            if (raw[i] != '{')
            {
                i++;
                continue;
            }

            int objectEnd = FindMatchingJsonScope(raw, i, '{', '}');
            if (objectEnd < 0)
                break;

            string chunk = raw.Substring(i, objectEnd - i + 1);
            string id = ExtractJsonStringProperty(chunk, "poolId");
            if (id.Length == 0)
            {
                i = objectEnd + 1;
                continue;
            }

            if (!Pools.ContainsKey(id))
            {
                int linesArrayStart = FindArrayPropertyStart(chunk, "lines");
                if (linesArrayStart >= 0)
                {
                    List<string> lines = ParseJsonStringArray(chunk, linesArrayStart);
                    if (lines != null && lines.Count > 0)
                        Pools.Add(id, lines.ToArray());
                }
            }

            i = objectEnd + 1;
        }

        if (Pools.Count > before)
            Plugin.Log?.LogInfo(
                $"[EventCore] JsonUtility skipped linePools — manual parser loaded {Pools.Count - before} pool(s). File: {pathForLog}");
    }

    private static List<string> ParseJsonStringArray(string s, int openBracketIdx)
    {
        var result = new List<string>();
        int i = openBracketIdx + 1;
        while (i < s.Length)
        {
            SkipJsonWhitespace(s, ref i);
            if (i >= s.Length || s[i] == ']')
                break;
            if (s[i] == ',')
            {
                i++;
                continue;
            }

            if (s[i] != '"')
            {
                i++;
                continue;
            }

            string parsed = ParseJsonStringValue(s, ref i);
            if (parsed != null)
                result.Add(parsed);
        }

        return result;
    }

    private static void MergeLegacyEntries(string raw, string pathForLog)
    {
        var file = JsonUtility.FromJson<EventCoreStringsFile>(raw);
        var entries = file?.entries;
        if (entries == null || entries.Length == 0)
        {
            TryManualLoadEntries(raw);
            return;
        }

        IngestEntries(entries, replaceExisting: false);

        if (Map.Count == 0)
            TryManualLoadEntries(raw);
    }

    private static void IngestEntries(EventCoreStringEntry[] entries, bool replaceExisting)
    {
        if (entries == null || entries.Length == 0)
            return;

        for (int i = 0; i < entries.Length; i++)
        {
            EventCoreStringEntry e = entries[i];
            if (e == null || string.IsNullOrEmpty(e.key))
                continue;
            string k = e.key.Trim();
            if (k.Length == 0)
                continue;
            if (!replaceExisting && Map.ContainsKey(k))
                continue;
            Map[k] = e.text ?? string.Empty;
        }
    }

    internal static bool TryGet(string key, out string value)
    {
        value = null;
        if (string.IsNullOrEmpty(key))
            return false;
        return Map.TryGetValue(key.Trim(), out value) && !string.IsNullOrEmpty(value);
    }

    internal static bool TryGetRandomLine(string poolId, out string line)
    {
        line = null;
        if (string.IsNullOrEmpty(poolId))
            return false;

        string id = poolId.Trim();
        if (!Pools.TryGetValue(id, out string[] lines) || lines == null || lines.Length == 0)
            return false;

        line = lines[UnityEngine.Random.Range(0, lines.Length)];
        return !string.IsNullOrEmpty(line);
    }

    internal static string FormatLine(string raw, long reqGold, long playerGold)
    {
        if (string.IsNullOrEmpty(raw))
            return string.Empty;

        string s = raw;
        s = s.Replace("{GOLD}", playerGold.ToString());
        s = s.Replace("{REQ}", reqGold.ToString());
        if (EventCoreFactionSocialSession.Active)
            s = s.Replace("{SEX_PRICE}", EventCoreFactionSocialSession.SexPriceGold.ToString());
        else
            s = s.Replace("{SEX_PRICE}", reqGold.ToString());

        int idx = s.IndexOf("(---)", StringComparison.Ordinal);
        while (idx >= 0)
        {
            string repl = reqGold.ToString();
            s = s.Substring(0, idx) + repl + s.Substring(idx + 5);
            idx = s.IndexOf("(---)", StringComparison.Ordinal);
        }

        return s;
    }

    internal static string ResolveStepBody(EventCoreStepDefinition step, long reqGold, long playerGold)
    {
        if (step == null)
            return string.Empty;

        string body = step.npcLine ?? string.Empty;

        if (!string.IsNullOrEmpty(step.npcLinePoolId))
        {
            if (TryGetRandomLine(step.npcLinePoolId, out string pick))
                body = pick;
            else if (string.IsNullOrEmpty(body))
                Plugin.Log?.LogWarning($"[EventCore] npcLinePoolId '{step.npcLinePoolId}' — pool missing or empty.");
        }

        if (!string.IsNullOrEmpty(step.npcLineKey))
        {
            if (TryGet(step.npcLineKey, out string resolved))
                body = resolved;
            else if (string.IsNullOrEmpty(body))
                Plugin.Log?.LogWarning($"[EventCore] Missing string key '{step.npcLineKey}'.");
        }

        return FormatLine(body, reqGold, playerGold);
    }

    internal static string[] ResolveChoiceLabels(EventCoreStepDefinition step, long reqGold, long playerGold)
    {
        if (step == null)
            return new string[0];

        string[] poolIds = step.choicePoolIds;
        if (poolIds != null && poolIds.Length > 0)
        {
            int n = Mathf.Min(poolIds.Length, 5);
            var labels = new string[n];
            for (int i = 0; i < n; i++)
            {
                string pid = poolIds[i];
                if (!string.IsNullOrEmpty(pid) && TryGetRandomLine(pid.Trim(), out string line))
                    labels[i] = FormatLine(line, reqGold, playerGold);
                else if (step.choiceLabels != null && i < step.choiceLabels.Length)
                    labels[i] = FormatLine(step.choiceLabels[i] ?? string.Empty, reqGold, playerGold);
                else
                    labels[i] = string.Empty;
            }

            return labels;
        }

        if (step.choiceLabels == null || step.choiceLabels.Length == 0)
            return new string[0];

        var plain = new string[step.choiceLabels.Length];
        for (int i = 0; i < step.choiceLabels.Length; i++)
            plain[i] = FormatLine(step.choiceLabels[i] ?? string.Empty, reqGold, playerGold);
        return plain;
    }

    private static void TryManualLoadEntries(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return;

        int arrayStart = FindArrayPropertyStart(raw, "entries");
        if (arrayStart < 0)
            return;

        int i = arrayStart + 1;
        while (i < raw.Length)
        {
            SkipJsonWhitespace(raw, ref i);
            if (i >= raw.Length || raw[i] == ']')
                break;
            if (raw[i] == ',')
            {
                i++;
                continue;
            }
            if (raw[i] != '{')
            {
                i++;
                continue;
            }

            int objectEnd = FindMatchingJsonScope(raw, i, '{', '}');
            if (objectEnd < 0)
                break;

            string chunk = raw.Substring(i, objectEnd - i + 1);
            string k = ExtractJsonStringProperty(chunk, "key").Trim();
            string t = ExtractJsonStringProperty(chunk, "text") ?? string.Empty;
            if (k.Length == 0 || Map.ContainsKey(k))
            {
                i = objectEnd + 1;
                continue;
            }

            Map.Add(k, t);
            i = objectEnd + 1;
        }
    }

    private static int FindArrayPropertyStart(string s, string propertyName)
    {
        if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(propertyName))
            return -1;

        string token = "\"" + propertyName + "\"";
        int propertyIndex = s.IndexOf(token, StringComparison.Ordinal);
        if (propertyIndex < 0)
            return -1;

        int colonIndex = s.IndexOf(':', propertyIndex + token.Length);
        if (colonIndex < 0)
            return -1;

        int i = colonIndex + 1;
        SkipJsonWhitespace(s, ref i);
        return i < s.Length && s[i] == '[' ? i : -1;
    }

    private static string ExtractJsonStringProperty(string s, string propertyName)
    {
        if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(propertyName))
            return string.Empty;

        string token = "\"" + propertyName + "\"";
        int propertyIndex = s.IndexOf(token, StringComparison.Ordinal);
        if (propertyIndex < 0)
            return string.Empty;

        int colonIndex = s.IndexOf(':', propertyIndex + token.Length);
        if (colonIndex < 0)
            return string.Empty;

        int i = colonIndex + 1;
        SkipJsonWhitespace(s, ref i);
        if (i >= s.Length || s[i] != '"')
            return string.Empty;

        return ParseJsonStringValue(s, ref i) ?? string.Empty;
    }

    private static void SkipJsonWhitespace(string s, ref int i)
    {
        while (i < s.Length && char.IsWhiteSpace(s[i]))
            i++;
    }

    private static string ParseJsonStringValue(string s, ref int i)
    {
        if (i < 0 || i >= s.Length || s[i] != '"')
            return null;

        i++;
        var sb = new StringBuilder();
        while (i < s.Length)
        {
            char c = s[i];
            if (c == '\\' && i + 1 < s.Length)
            {
                char n = s[i + 1];
                if (n == '"' || n == '\\' || n == '/')
                {
                    sb.Append(n);
                    i += 2;
                    continue;
                }

                if (n == 'b')
                {
                    sb.Append('\b');
                    i += 2;
                    continue;
                }

                if (n == 'f')
                {
                    sb.Append('\f');
                    i += 2;
                    continue;
                }

                if (n == 'n')
                {
                    sb.Append('\n');
                    i += 2;
                    continue;
                }

                if (n == 'r')
                {
                    sb.Append('\r');
                    i += 2;
                    continue;
                }

                if (n == 't')
                {
                    sb.Append('\t');
                    i += 2;
                    continue;
                }

                if (n == 'u' && i + 5 < s.Length)
                {
                    string hex = s.Substring(i + 2, 4);
                    if (int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int cp))
                        sb.Append(char.ConvertFromUtf32(cp));
                    i += 6;
                    continue;
                }

                i += 2;
                continue;
            }

            if (c == '"')
            {
                i++;
                return sb.ToString();
            }

            sb.Append(c);
            i++;
        }

        return sb.ToString();
    }

    private static int FindMatchingJsonScope(string s, int startIndex, char openChar, char closeChar)
    {
        if (string.IsNullOrEmpty(s) || startIndex < 0 || startIndex >= s.Length || s[startIndex] != openChar)
            return -1;

        int depth = 0;
        bool inString = false;
        for (int i = startIndex; i < s.Length; i++)
        {
            char c = s[i];
            if (inString)
            {
                if (c == '\\')
                {
                    i++;
                    continue;
                }

                if (c == '"')
                    inString = false;

                continue;
            }

            if (c == '"')
            {
                inString = true;
                continue;
            }

            if (c == openChar)
            {
                depth++;
                continue;
            }

            if (c == closeChar)
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }

        return -1;
    }

    private static void MergeFactionSocialLangPacks(string jsonRoot, string langFolder)
    {
        if (string.IsNullOrEmpty(jsonRoot))
            return;

        MergeFactionSocialLangFromFolder(jsonRoot, langFolder);
        // FSP content is authored under Ru/ for now — always merge Ru packs as fallback.
        if (!string.Equals(langFolder, "Ru", StringComparison.OrdinalIgnoreCase))
            MergeFactionSocialLangFromFolder(jsonRoot, "Ru");
    }

    private static void MergeFactionSocialLangFromFolder(string jsonRoot, string langFolder)
    {
        if (string.IsNullOrEmpty(langFolder))
            return;

        string factionRoot = Path.Combine(Path.Combine(jsonRoot, langFolder), "FactionSocial");
        if (!Directory.Exists(factionRoot))
            return;

        string banditsDir = Path.Combine(factionRoot, "bandits");
        if (Directory.Exists(banditsDir))
            LoadFactionSocialLangFilesRecursive(banditsDir);
    }

    /// <summary>
    /// Loads only <c>*_lang.json</c> packs (one file per FSP event folder), not step graphs.
    /// </summary>
    private static void LoadFactionSocialLangFilesRecursive(string directory)
    {
        foreach (string file in Directory.GetFiles(directory, "*_lang.json", SearchOption.AllDirectories))
        {
            if (file.IndexOf("README", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            LoadLangPack(File.ReadAllText(file), file);
        }
    }

}
