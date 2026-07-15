using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using NoREroMod.Systems.CombatAi.Factions;
using NoREroMod.Systems.Pregnancy.OffspringArchetype;
using NoREroMod.Systems.Pregnancy.ShelterAttack;
using UnityEngine;

namespace NoREroMod.Systems.Pregnancy;

/// <summary>
/// Persistent per-slot storage for pregnancy/offspring data.
/// Pattern: GoldWallet (regex read + StringBuilder write, JSON per slot).
/// </summary>
internal static class PregnancySlotStore
{
    private static readonly List<ChildData> _children = new();
    private static int _activeSlot = -1;
    private static bool _dirty = false;

    internal static int ActiveSlot => _activeSlot;

    // JSON file paths: BepInEx/plugins/HellGateJson/Pregnancy/PlayerPregnancy_Slot{01..03}.json
    private static string GetFilePath(int slot)
    {
        string fileName = string.Format("PlayerPregnancy_Slot{0:D2}.json", slot);
        return ResolvePregnancyFilePath(fileName);
    }

    private static string GetCurrentPregnancyFilePath(int slot)
    {
        string fileName = string.Format("PlayerCurrentPregnancy_Slot{0:D2}.json", slot);
        return ResolvePregnancyFilePath(fileName);
    }

    private static string ResolvePregnancyFilePath(string fileName)
    {
        try
        {
            string dir = Path.Combine(Path.Combine(Paths.PluginPath, "HellGateJson"), "Pregnancy");
            try { Directory.CreateDirectory(dir); } catch { }
            return Path.Combine(dir, fileName);
        }
        catch
        {
            string gameRoot = Application.dataPath;
            if (gameRoot.EndsWith("_Data", StringComparison.OrdinalIgnoreCase))
                gameRoot = gameRoot.Substring(0, gameRoot.Length - 5);

            string dir = Path.Combine(Path.Combine(Path.Combine(Path.Combine(gameRoot, "BepInEx"), "plugins"), "HellGateJson"), "Pregnancy");
            try { Directory.CreateDirectory(dir); } catch { }
            return Path.Combine(dir, fileName);
        }
    }

    /// <summary>Bind the active save slot (1..3). Flushes the previous slot if dirty.</summary>
    public static void BindActiveSlot(int slot)
    {
        if (slot < 1 || slot > 3)
        {
            Plugin.Log?.LogWarning("[Pregnancy.Store] BindActiveSlot rejected: slot=" + slot);
            return;
        }

        _activeSlot = slot;
        ShelterAttackSlotStore.BindActiveSlot(slot);
    }

    public static void LoadFromActiveSlot()
    {
        if (_activeSlot < 1)
        {
            Plugin.Log?.LogWarning("[Pregnancy.Store] LoadFromActiveSlot called with no active slot");
            return;
        }

        WitchPregnancyState.ClearAll();
        Load(_activeSlot);
        LoadCurrentPregnancy(_activeSlot);
        ShelterAttackSlotStore.LoadFromActiveSlot();
        ResetHideoutSpawnFlags();
    }

    /// <summary>Flushes pregnancy + shelter attack JSON. Call only from the vanilla SaveFile hook.</summary>
    public static void SaveToActiveSlot(bool force = true)
    {
        if (_activeSlot < 1)
        {
            Plugin.Log?.LogWarning("[Pregnancy.Store] SaveToActiveSlot called with no active slot — bind slot on Save/Load first");
            return;
        }

        if (!force && !_dirty)
            return;

        Save(_activeSlot);
        SaveCurrentPregnancy(_activeSlot);
        ShelterAttackSlotStore.SaveToActiveSlot(force: true);
    }

    private static void ResetHideoutSpawnFlags()
    {
        foreach (var child in _children)
            child.IsSpawned = false;
    }

    public static IList<ChildData> GetAllChildren()
    {
        return _children;
    }

    public static List<ChildData> GetAliveChildren()
    {
        var result = new List<ChildData>(_children.Count);
        foreach (var c in _children)
        {
            if (c.IsAlive)
                result.Add(c);
        }
        return result;
    }

    public static List<ChildData> GetAliveChildrenInHideout()
    {
        var result = new List<ChildData>(_children.Count);
        foreach (var c in _children)
        {
            if (c.IsInHideout)
                result.Add(c);
        }
        return result;
    }

    public static int CountAliveChildren()
    {
        int count = 0;
        foreach (var c in _children)
        {
            if (c.IsAlive) count++;
        }
        return count;
    }

    public static int CountChildrenInHideout()
    {
        int count = 0;
        foreach (var c in _children)
        {
            if (c.IsInHideout) count++;
        }
        return count;
    }

    /// <summary>Adds a newborn child (called on birth).</summary>
    public static ChildData AddChild(int factionSource, string name = null)
    {
        var child = new ChildData
        {
            Guid = Guid.NewGuid().ToString("N"),
            FactionSource = factionSource,
            GrowthStage = (int)ChildGrowthStage.Infant,
            State = (int)ChildState.InHideout,
            HideoutNodeIndex = AssignFreeHideoutNode(),
            IsAlive = true,
            BirthTimestamp = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds,
            Name = name ?? GenerateDefaultName(factionSource)
        };

        _children.Add(child);
        OffspringArchetypeRoll.AssignToChild(child);
        _dirty = true;

        if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
            Plugin.Log?.LogInfo($"[Pregnancy.Store] Child born: {child.Name} (faction={factionSource}, archetype={child.SpawnArchetype}, node={child.HideoutNodeIndex})");

        return child;
    }

    public static void MarkDirty()
    {
        _dirty = true;
    }

    public static void Save(int slot)
    {
        string path = GetFilePath(slot);
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"version\": 1,");
            sb.AppendLine($"  \"slot\": {slot},");
            sb.AppendLine($"  \"savedAt\": {DateTime.UtcNow:O},");
            sb.AppendLine($"  \"childCount\": {_children.Count},");
            sb.AppendLine("  \"children\": [");

            for (int i = 0; i < _children.Count; i++)
            {
                var c = _children[i];
                sb.AppendLine("    {");
                sb.AppendLine($"      \"guid\": \"{c.Guid}\",");
                sb.AppendLine($"      \"factionSource\": {c.FactionSource},");
                if (!string.IsNullOrEmpty(c.SpawnArchetype))
                    sb.AppendLine($"      \"spawnArchetype\": \"{EscapeJson(c.SpawnArchetype)}\",");
                sb.AppendLine($"      \"growthStage\": {c.GrowthStage},");
                sb.AppendLine($"      \"state\": {c.State},");
                sb.AppendLine($"      \"hideoutNodeIndex\": {c.HideoutNodeIndex},");
                sb.AppendLine($"      \"isAlive\": {c.IsAlive.ToString().ToLowerInvariant()},");
                sb.AppendLine($"      \"currentHp\": {c.CurrentHp.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
                sb.Append($"      \"birthTimestamp\": {c.BirthTimestamp}");
                if (!string.IsNullOrEmpty(c.Name))
                    sb.AppendLine($",\n      \"name\": \"{EscapeJson(c.Name)}\"");
                else
                    sb.AppendLine();
                sb.Append("    }");
                if (i < _children.Count - 1)
                    sb.AppendLine(",");
                else
                    sb.AppendLine();
            }

            sb.AppendLine("  ]");
            sb.AppendLine("}");

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            _dirty = false;

            if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
                Plugin.Log?.LogInfo($"[Pregnancy.Store] Saved {_children.Count} children to slot {slot}");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[Pregnancy.Store] Failed to save slot {slot}: {ex.Message}");
        }
    }

    public static void Load(int slot)
    {
        string path = GetFilePath(slot);
        _children.Clear();

        if (!File.Exists(path))
        {
            if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
                Plugin.Log?.LogInfo($"[Pregnancy.Store] No save file for slot {slot}, starting fresh");
            return;
        }

        try
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            ParseJson(json);
            CleanupInvalidChildren();
            _dirty = false;

            if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
                Plugin.Log?.LogInfo($"[Pregnancy.Store] Loaded {_children.Count} children from slot {slot}");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[Pregnancy.Store] Failed to load slot {slot}: {ex.Message}");
        }
    }

    private static void SaveCurrentPregnancy(int slot)
    {
        string path = GetCurrentPregnancyFilePath(slot);
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"version\": 1,");
            sb.AppendLine($"  \"slot\": {slot},");
            sb.AppendLine($"  \"savedAt\": {DateTime.UtcNow:O},");
            sb.AppendLine($"  \"isActive\": {WitchPregnancyState.IsActive.ToString().ToLowerInvariant()},");
            sb.AppendLine($"  \"sourceFaction\": {WitchPregnancyState.SourceFaction},");
            sb.AppendLine($"  \"gestationElapsedSeconds\": {WitchPregnancyState.GestationElapsedSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
            sb.AppendLine($"  \"pendingFaction\": {WitchPregnancyState.PendingFaction}");
            sb.AppendLine("}");

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);

            if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
                Plugin.Log?.LogInfo($"[Pregnancy.Store] Saved current pregnancy to slot {slot}: active={WitchPregnancyState.IsActive}, faction={WitchPregnancyState.SourceFaction}, elapsed={WitchPregnancyState.GestationElapsedSeconds:F1}s");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[Pregnancy.Store] Failed to save current pregnancy for slot {slot}: {ex.Message}");
        }
    }

    private static void LoadCurrentPregnancy(int slot)
    {
        string path = GetCurrentPregnancyFilePath(slot);
        if (!File.Exists(path))
        {
            WitchPregnancyState.ClearAll();
            if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
                Plugin.Log?.LogInfo($"[Pregnancy.Store] No current pregnancy file for slot {slot}, starting fresh");
            return;
        }

        try
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            ParseCurrentPregnancyJson(json);

            if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
                Plugin.Log?.LogInfo($"[Pregnancy.Store] Loaded current pregnancy from slot {slot}: active={WitchPregnancyState.IsActive}, faction={WitchPregnancyState.SourceFaction}, elapsed={WitchPregnancyState.GestationElapsedSeconds:F1}s");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[Pregnancy.Store] Failed to load current pregnancy for slot {slot}: {ex.Message}");
            WitchPregnancyState.ClearAll();
        }
    }

    private static void ParseCurrentPregnancyJson(string json)
    {
        WitchPregnancyState.ClearAll();

        var lines = json.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        bool isActive = false;
        int sourceFaction = FactionIds.Neutral;
        float gestationElapsedSeconds = 0f;
        int pendingFaction = FactionIds.Neutral;

        foreach (var rawLine in lines)
        {
            string line = rawLine.Trim();
            if (!line.StartsWith("\""))
                continue;

            int colonIdx = line.IndexOf(':');
            if (colonIdx < 0)
                continue;

            string key = line.Substring(1, line.IndexOf('"', 1) - 1);
            string value = line.Substring(colonIdx + 1).Trim().TrimEnd(',', '"');

            switch (key)
            {
                case "isActive":
                    bool.TryParse(value, out isActive);
                    break;
                case "sourceFaction":
                    int.TryParse(value, out sourceFaction);
                    break;
                case "gestationElapsedSeconds":
                    float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out gestationElapsedSeconds);
                    break;
                case "pendingFaction":
                    int.TryParse(value, out pendingFaction);
                    break;
            }
        }

        if (isActive && sourceFaction != FactionIds.Neutral)
        {
            WitchPregnancyState.SourceFaction = sourceFaction;
            WitchPregnancyState.GestationElapsedSeconds = Mathf.Max(0f, gestationElapsedSeconds);
        }

        if (pendingFaction != FactionIds.Neutral)
        {
            WitchPregnancyState.PendingFaction = pendingFaction;
        }
    }

    private static void ParseJson(string json)
    {
        // Simple regex-free JSON parsing for our known structure
        var lines = json.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        ChildData current = null;

        foreach (var rawLine in lines)
        {
            string line = rawLine.Trim();
            if (line.StartsWith("{") && current == null)
            {
                current = new ChildData();
            }
            else if (line.StartsWith("}") && current != null)
            {
                _children.Add(current);
                current = null;
            }
            else if (current != null && line.StartsWith("\""))
            {
                int colonIdx = line.IndexOf(':');
                if (colonIdx < 0) continue;

                string key = line.Substring(1, line.IndexOf('"', 1) - 1);
                string value = line.Substring(colonIdx + 1).Trim().TrimEnd(',', '"');

                switch (key)
                {
                    case "guid": current.Guid = NormalizeJsonString(value); break;
                    case "factionSource": int.TryParse(value, out current.FactionSource); break;
                    case "spawnArchetype": current.SpawnArchetype = NormalizeJsonString(value); break;
                    case "growthStage": int.TryParse(value, out current.GrowthStage); break;
                    case "state": int.TryParse(value, out current.State); break;
                    case "hideoutNodeIndex": int.TryParse(value, out current.HideoutNodeIndex); break;
                    case "isAlive": current.IsAlive = value.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                    case "currentHp": float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out current.CurrentHp); break;
                    case "birthTimestamp": double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out current.BirthTimestamp); break;
                    case "name": current.Name = NormalizeJsonString(value); break;
                }
            }
        }
    }

    private static void CleanupInvalidChildren()
    {
        int removed = 0;
        for (int i = _children.Count - 1; i >= 0; i--)
        {
            var c = _children[i];
            if (!c.IsAlive || string.IsNullOrEmpty(c.Guid) || c.HideoutNodeIndex < 0)
            {
                _children.RemoveAt(i);
                removed++;
            }
        }
        if (removed > 0 && PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
            Plugin.Log?.LogInfo($"[Pregnancy.Store] Cleaned up {removed} invalid children");
    }

    private static int AssignFreeHideoutNode()
    {
        // Hideout has 9 nodes (0-8), find first free
        bool[] taken = new bool[9];
        foreach (var c in _children)
        {
            if (c.IsInHideout && c.HideoutNodeIndex >= 0 && c.HideoutNodeIndex < 9)
                taken[c.HideoutNodeIndex] = true;
        }
        for (int i = 0; i < 9; i++)
        {
            if (!taken[i])
                return i;
        }
        return -1; // All nodes full
    }

    private static string GenerateDefaultName(int factionSource)
    {
        string faction = factionSource switch
        {
            100 => "Bandit",
            200 => "Church",
            300 => "Demon",
            400 => "Mafia",
            500 => "Undead",
            600 => "Monster",
            _ => "Child"
        };
        return $"{faction}-{_children.Count + 1}";
    }

    private static string NormalizeJsonString(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return raw;

        string value = raw.Trim().Trim('"');
        while (value.StartsWith("\"", StringComparison.Ordinal))
            value = value.Substring(1);
        return value;
    }

    private static string EscapeJson(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }
}
