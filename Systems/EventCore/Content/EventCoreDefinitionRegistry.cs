using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using NoREroMod;
using NoREroMod.Systems.EventCore.Core;

namespace NoREroMod.Systems.EventCore.Content;

/// <summary>
/// Loads eventcore_manifest.json and referenced JSON event definitions into memory.
/// </summary>
internal static class EventCoreDefinitionRegistry
{
    private static readonly Dictionary<string, EventCoreEventDefinitionFile> Events =
        new Dictionary<string, EventCoreEventDefinitionFile>();

    internal static void Clear() => Events.Clear();

    internal static bool TryGet(string id, out EventCoreEventDefinitionFile def) =>
        Events.TryGetValue(id, out def);

    internal static void EnsureLoaded()
    {
        if (Events.Count > 0)
            return;
        ReloadFromDisk();
    }

    internal static void ReloadFromDisk()
    {
        Clear();
        if (!Directory.Exists(EventCorePaths.JsonRoot))
        {
            Plugin.Log?.LogError($"[EventCore] JSON folder missing: {EventCorePaths.JsonRoot}");
            return;
        }

        string manifestPath = EventCorePaths.ManifestFile;
        if (!File.Exists(manifestPath))
        {
            Plugin.Log?.LogError($"[EventCore] Manifest missing: {manifestPath}");
            return;
        }

        string manifestJson = File.ReadAllText(manifestPath);
        var manifest = JsonUtility.FromJson<EventCoreManifestFile>(manifestJson);
        string[] files = manifest?.eventFiles;
        if (files == null || files.Length == 0)
        {
            Plugin.Log?.LogError("[EventCore] Manifest eventFiles is missing or empty (flat JSON array required for Unity 5 JsonUtility).");
            return;
        }

        foreach (string relative in files)
        {
            if (string.IsNullOrEmpty(relative))
                continue;

            string path = EventCorePaths.ResolveEventFile(relative);
            if (!File.Exists(path))
            {
                Plugin.Log?.LogError($"[EventCore] Event file not found: {path}");
                continue;
            }

            string raw = File.ReadAllText(path);
            var ev = JsonUtility.FromJson<EventCoreEventDefinitionFile>(raw);
            if (ev == null || string.IsNullOrEmpty(ev.id))
            {
                Plugin.Log?.LogError($"[EventCore] Invalid event JSON: {path}");
                continue;
            }

            HydrateStepsFromPerStepFiles(ev);
            HydrateAmbushesFromInlineJson(ev, raw, path);

            if (Events.ContainsKey(ev.id))
            {
                Plugin.Log?.LogError($"[EventCore] Duplicate event id '{ev.id}'");
                continue;
            }

            Events.Add(ev.id, ev);
        }

        Plugin.Log?.LogInfo($"[EventCore] Loaded {Events.Count} event definition(s).");
    }

    /// <summary>
    /// Unity 5.6 JsonUtility does not deserialize arrays of objects inside event JSON; load each step from its own file.
    /// </summary>
    private static void HydrateStepsFromPerStepFiles(EventCoreEventDefinitionFile ev)
    {
        if (ev.steps != null && ev.steps.Length > 0)
            return;

        if (ev.stepFiles == null || ev.stepFiles.Length == 0)
            return;

        var list = new List<EventCoreStepDefinition>();
        foreach (string relative in ev.stepFiles)
        {
            if (string.IsNullOrEmpty(relative))
                continue;

            string stepPath = EventCoreLanguage.ResolveStepFilePath(EventCorePaths.JsonRoot, relative.Trim());
            if (!File.Exists(stepPath))
            {
                Plugin.Log?.LogError($"[EventCore] Step file not found: {stepPath}");
                continue;
            }

            string stepRaw = File.ReadAllText(stepPath);
            var step = JsonUtility.FromJson<EventCoreStepDefinition>(stepRaw);
            if (step == null || string.IsNullOrEmpty(step.stepId))
            {
                Plugin.Log?.LogError($"[EventCore] Invalid step JSON: {stepPath}");
                continue;
            }

            SupplementStepArraysFromRaw(step, stepRaw, stepPath);
            list.Add(step);
        }

        ev.steps = list.ToArray();
    }

    /// <summary>
    /// Unity 5 JsonUtility often leaves string[] step fields empty; FSP branching depends on these arrays.
    /// </summary>
    private static void SupplementStepArraysFromRaw(EventCoreStepDefinition step, string raw, string pathForLog)
    {
        if (step == null || string.IsNullOrEmpty(raw))
            return;

        bool hadJumps = step.choiceJumpStepIds != null && step.choiceJumpStepIds.Length > 0;

        string[] jumps = ParseJsonStringArrayProperty(raw, "choiceJumpStepIds");
        if (jumps != null && jumps.Length > 0)
            step.choiceJumpStepIds = jumps;

        string[] outcomes = ParseJsonStringArrayProperty(raw, "choiceOutcomeIds");
        if (outcomes != null && outcomes.Length > 0)
            step.choiceOutcomeIds = outcomes;

        string[] labels = ParseJsonStringArrayProperty(raw, "choiceLabels");
        if (labels != null && labels.Length > 0)
            step.choiceLabels = labels;

        string[] poolIds = ParseJsonStringArrayProperty(raw, "choicePoolIds");
        if (poolIds != null && poolIds.Length > 0)
            step.choicePoolIds = poolIds;

        if (!hadJumps && step.choiceJumpStepIds != null && step.choiceJumpStepIds.Length > 0)
        {
            Plugin.Log?.LogWarning(
                $"[EventCore] Step '{step.stepId}': choiceJumpStepIds loaded via manual parser. File: {pathForLog}");
        }
    }

    private static string[] ParseJsonStringArrayProperty(string raw, string propertyName)
    {
        int arrayStart = FindArrayPropertyStart(raw, propertyName);
        if (arrayStart < 0)
            return null;

        List<string> lines = ParseJsonStringArray(raw, arrayStart);
        return lines == null || lines.Count == 0 ? null : lines.ToArray();
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

    private static void HydrateAmbushesFromInlineJson(EventCoreEventDefinitionFile ev, string raw, string pathForLog)
    {
        if (ev == null || (ev.ambushes != null && ev.ambushes.Length > 0))
            return;
        if (string.IsNullOrEmpty(raw))
            return;

        int arrayStart = FindArrayPropertyStart(raw, "ambushes");
        if (arrayStart < 0)
            return;

        var ambushes = new List<EventCoreAmbushDefinition>();
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
            string ambushId = ExtractJsonStringProperty(chunk, "ambushId");
            if (!string.IsNullOrEmpty(ambushId))
            {
                var ambush = new EventCoreAmbushDefinition();
                ambush.ambushId = ambushId;
                ambush.slots = ParseAmbushSlots(chunk).ToArray();
                ambushes.Add(ambush);
            }

            i = objectEnd + 1;
        }

        if (ambushes.Count > 0)
        {
            ev.ambushes = ambushes.ToArray();
            Plugin.Log?.LogInfo(
                $"[EventCore] JsonUtility skipped ambushes — manual parser loaded {ambushes.Count} ambush pack(s). File: {pathForLog}");
        }
    }

    private static List<EventCoreAmbushSpawnSlot> ParseAmbushSlots(string rawAmbushChunk)
    {
        var slots = new List<EventCoreAmbushSpawnSlot>();
        int arrayStart = FindArrayPropertyStart(rawAmbushChunk, "slots");
        if (arrayStart < 0)
            return slots;

        int i = arrayStart + 1;
        while (i < rawAmbushChunk.Length)
        {
            SkipJsonWhitespace(rawAmbushChunk, ref i);
            if (i >= rawAmbushChunk.Length || rawAmbushChunk[i] == ']')
                break;
            if (rawAmbushChunk[i] == ',')
            {
                i++;
                continue;
            }
            if (rawAmbushChunk[i] != '{')
            {
                i++;
                continue;
            }

            int objectEnd = FindMatchingJsonScope(rawAmbushChunk, i, '{', '}');
            if (objectEnd < 0)
                break;

            string slotChunk = rawAmbushChunk.Substring(i, objectEnd - i + 1);
            string enemyType = ExtractJsonStringProperty(slotChunk, "enemyType");
            if (!string.IsNullOrEmpty(enemyType))
            {
                var slot = new EventCoreAmbushSpawnSlot();
                slot.enemyType = enemyType;
                slot.factionId = ExtractJsonStringProperty(slotChunk, "factionId");
                slot.eventId = ExtractJsonStringProperty(slotChunk, "eventId");
                slot.offsetX = ExtractJsonFloatProperty(slotChunk, "offsetX", 0f);
                slot.offsetY = ExtractJsonFloatProperty(slotChunk, "offsetY", 0f);
                slot.count = Mathf.Max(1, ExtractJsonIntProperty(slotChunk, "count", 1));
                slots.Add(slot);
            }

            i = objectEnd + 1;
        }

        return slots;
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

    private static float ExtractJsonFloatProperty(string s, string propertyName, float defaultValue)
    {
        string number = ExtractJsonNumericProperty(s, propertyName);
        if (string.IsNullOrEmpty(number))
            return defaultValue;

        return float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            ? value
            : defaultValue;
    }

    private static int ExtractJsonIntProperty(string s, string propertyName, int defaultValue)
    {
        string number = ExtractJsonNumericProperty(s, propertyName);
        if (string.IsNullOrEmpty(number))
            return defaultValue;

        return int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : defaultValue;
    }

    private static string ExtractJsonNumericProperty(string s, string propertyName)
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
        int start = i;
        while (i < s.Length)
        {
            char c = s[i];
            if ((c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.')
            {
                i++;
                continue;
            }

            break;
        }

        return i > start ? s.Substring(start, i - start) : string.Empty;
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
}
