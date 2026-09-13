using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// Saved F11 spawn presets (any catalog).
/// File: HellGateJson/HellGateSpawnPoint/SPAWN_AUTHORING_FAVORITES.txt
/// New: Label|kind|Key|faction|elite|flip|count|random|chance|rot|sort|ec
/// Old enemy lines still load: Label|EnemyKey|faction|elite|flip|count|random|chance
/// </summary>
internal static class SpawnAuthoringFavorites
{
    private const string FileName = "SPAWN_AUTHORING_FAVORITES.txt";

    internal struct Entry
    {
        internal string Label;
        internal SpawnAuthoringState.CatalogKind Kind;
        internal string EnemyKey;
        internal string Faction;
        internal bool Elite;
        internal bool FlipX;
        internal int Count;
        internal bool Random;
        internal float Chance;
        internal float RotationZ;
        internal int SortOffset;
        internal string EventCore;
        internal float EventCoreChance;
    }

    private static readonly List<Entry> entries = new List<Entry>(32);
    private static bool loaded;
    private static float nextReloadUnscaled;

    internal static bool PanelOpen;

    internal static IList<Entry> GetEntries()
    {
        EnsureLoaded();
        return entries;
    }

    internal static void Invalidate()
    {
        loaded = false;
        nextReloadUnscaled = 0f;
    }

    internal static string GetFilePath()
    {
        string path = Path.Combine(Paths.PluginPath, "HellGateJson");
        path = Path.Combine(path, "HellGateSpawnPoint");
        return Path.Combine(path, FileName);
    }

    internal static bool TryAddFromPending(out string status)
    {
        status = null;
        string key = SpawnAuthoringState.PendingEnemyKey;
        if (string.IsNullOrEmpty(key))
        {
            status = SpawnAuthoringLoc.T("status.pickEnemy");
            return false;
        }

        Entry neo = CapturePending();
        EnsureLoaded();
        for (int i = 0; i < entries.Count; i++)
        {
            if (SamePreset(entries[i], neo))
            {
                status = SpawnAuthoringLoc.T("status.favAlready");
                return false;
            }
        }

        entries.Add(neo);
        if (!TrySave(out string err))
        {
            status = err ?? SpawnAuthoringLoc.T("status.saveFailed");
            return false;
        }

        status = SpawnAuthoringLoc.Tf("status.favAdded", neo.Label);
        return true;
    }

    internal static bool TryRemoveAt(int index, out string status)
    {
        status = null;
        EnsureLoaded();
        if (index < 0 || index >= entries.Count)
        {
            status = SpawnAuthoringLoc.T("status.favBadIndex");
            return false;
        }

        string label = entries[index].Label;
        entries.RemoveAt(index);
        if (!TrySave(out string err))
        {
            status = err ?? SpawnAuthoringLoc.T("status.saveFailed");
            return false;
        }

        status = SpawnAuthoringLoc.Tf("status.favRemoved", label);
        return true;
    }

    internal static bool TryApplyAt(int index, out string status)
    {
        status = null;
        EnsureLoaded();
        if (index < 0 || index >= entries.Count)
        {
            status = SpawnAuthoringLoc.T("status.favBadIndex");
            return false;
        }

        Entry e = entries[index];
        SpawnAuthoringState.ActiveCatalog = e.Kind == SpawnAuthoringState.CatalogKind.None
            ? SpawnAuthoringState.CatalogKind.Enemies
            : e.Kind;
        SpawnAuthoringState.PendingEnemyKey = e.EnemyKey ?? string.Empty;
        SpawnAuthoringState.PendingFactionIndex = IndexOfFaction(e.Faction);
        SpawnAuthoringState.PendingForceElite = e.Elite;
        SpawnAuthoringState.PendingFlipX = e.FlipX;
        SpawnAuthoringState.PendingRandom = e.Random;
        SpawnAuthoringState.PendingChanceStr = e.Chance.ToString("0.##", CultureInfo.InvariantCulture);
        SpawnAuthoringState.PendingRotationZ = SpawnAuthoringState.KeyAllowsRotation(e.EnemyKey)
            ? e.RotationZ
            : 0f;
        SpawnAuthoringState.PendingSortOffset = e.SortOffset;
        SpawnAuthoringState.PendingEventCoreIndex = 0;
        SpawnAuthoringState.PendingEventCoreChanceStr = (e.EventCoreChance > 0f ? e.EventCoreChance : 1f)
            .ToString("0.##", CultureInfo.InvariantCulture);
        if (e.Kind == SpawnAuthoringState.CatalogKind.EventCore ||
            !string.IsNullOrEmpty(e.EventCore))
        {
            SpawnAuthoringState.ActiveCatalog = SpawnAuthoringState.CatalogKind.EventCore;
            SpawnAuthoringState.PendingEnemyKey = !string.IsNullOrEmpty(e.EventCore)
                ? e.EventCore
                : (e.EnemyKey ?? string.Empty);
            if (e.Kind == SpawnAuthoringState.CatalogKind.EventCore &&
                string.IsNullOrEmpty(SpawnAuthoringState.GetPendingFactionToken()))
                SpawnAuthoringState.PendingFactionIndex = SpawnAuthoringState.IndexOfEventCoreEncounterFaction();
        }
        status = SpawnAuthoringLoc.Tf("status.favLoaded", e.Label ?? e.EnemyKey);
        return true;
    }

    internal static string FormatRow(Entry e)
    {
        string label = string.IsNullOrEmpty(e.Label) ? e.EnemyKey : e.Label;
        string tag = KindTag(e.Kind);
        if (!string.IsNullOrEmpty(tag))
            label = tag + " " + label;
        if (e.Elite)
            label = label + " [E]";
        if (e.Random)
            label = label + " [RND]";
        if (e.FlipX)
            label = label + " [flip]";
        if (!string.IsNullOrEmpty(e.EventCore))
        {
            label = label + " [EC";
            if (e.EventCoreChance > 0f && e.EventCoreChance < 0.999f)
                label = label + " " + e.EventCoreChance.ToString("0.##", CultureInfo.InvariantCulture);
            label = label + "]";
        }
        return label ?? "?";
    }

    private static Entry CapturePending()
    {
        string key = SpawnAuthoringState.PendingEnemyKey;
        string faction = SpawnAuthoringState.GetPendingFactionToken() ?? string.Empty;
        bool elite = SpawnAuthoringState.PendingForceElite;
        bool flip = SpawnAuthoringState.PendingFlipX;
        bool random = SpawnAuthoringState.PendingRandom;
        float chance = SpawnAuthoringState.ResolvePendingChance();
        float rot = SpawnAuthoringState.KeyAllowsRotation(key)
            ? SpawnAuthoringState.PendingRotationZ
            : 0f;
        int sort = SpawnAuthoringState.PendingSortOffset;
        string ec = SpawnAuthoringState.GetPendingEventCoreId() ?? string.Empty;
        float ecChance = SpawnAuthoringState.ResolvePendingEventCoreChance();
        SpawnAuthoringState.CatalogKind kind = SpawnAuthoringState.ActiveCatalog;
        if (kind == SpawnAuthoringState.CatalogKind.None)
            kind = SpawnAuthoringState.CatalogKind.Enemies;
        if (kind == SpawnAuthoringState.CatalogKind.EventCore && string.IsNullOrEmpty(ec))
            ec = key ?? string.Empty;
        return new Entry
        {
            Label = BuildDefaultLabel(kind, key, faction, elite, flip, random, chance, ec),
            Kind = kind,
            EnemyKey = key,
            Faction = faction,
            Elite = elite,
            FlipX = flip,
            Count = 1,
            Random = random,
            Chance = chance,
            RotationZ = rot,
            SortOffset = sort,
            EventCore = ec,
            EventCoreChance = ecChance
        };
    }

    private static bool SamePreset(Entry a, Entry b)
    {
        return a.Kind == b.Kind &&
               string.Equals(a.EnemyKey, b.EnemyKey, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(a.Faction ?? string.Empty, b.Faction ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(a.EventCore ?? string.Empty, b.EventCore ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
               Mathf.Abs(a.EventCoreChance - b.EventCoreChance) < 0.001f &&
               a.Elite == b.Elite &&
               a.FlipX == b.FlipX &&
               a.Random == b.Random &&
               Mathf.Abs(a.Chance - b.Chance) < 0.001f &&
               Mathf.Abs(a.RotationZ - b.RotationZ) < 0.01f &&
               a.SortOffset == b.SortOffset;
    }

    private static string BuildDefaultLabel(
        SpawnAuthoringState.CatalogKind kind,
        string key,
        string faction,
        bool elite,
        bool flip,
        bool random,
        float chance,
        string eventCore)
    {
        string name = key ?? "?";
        if (!string.IsNullOrEmpty(faction) &&
            (kind == SpawnAuthoringState.CatalogKind.Enemies ||
             kind == SpawnAuthoringState.CatalogKind.Hostage ||
             kind == SpawnAuthoringState.CatalogKind.EventCore))
            name = name + " / " + SpawnAuthoringFactionUi.DisplayName(faction);
        if (elite && (kind == SpawnAuthoringState.CatalogKind.Enemies ||
                      kind == SpawnAuthoringState.CatalogKind.EventCore))
            name = name + " ★";
        if (random)
            name = name + " RND" + chance.ToString("0.##", CultureInfo.InvariantCulture);
        if (flip)
            name = name + " flip";
        if (!string.IsNullOrEmpty(eventCore))
            name = name + " EC";
        return name;
    }

    private static string KindTag(SpawnAuthoringState.CatalogKind kind)
    {
        switch (kind)
        {
            case SpawnAuthoringState.CatalogKind.Trap:
                return "[T]";
            case SpawnAuthoringState.CatalogKind.LethalTrap:
                return "[L]";
            case SpawnAuthoringState.CatalogKind.Decor:
                return "[D]";
            case SpawnAuthoringState.CatalogKind.Hostage:
                return "[H]";
            case SpawnAuthoringState.CatalogKind.Gold:
                return "[G]";
            case SpawnAuthoringState.CatalogKind.EventTrap:
                return "[ET]";
            case SpawnAuthoringState.CatalogKind.EventCore:
                return "[EC]";
            default:
                return string.Empty;
        }
    }

    private static string KindToken(SpawnAuthoringState.CatalogKind kind)
    {
        switch (kind)
        {
            case SpawnAuthoringState.CatalogKind.Trap:
                return "trap";
            case SpawnAuthoringState.CatalogKind.LethalTrap:
                return "lethal";
            case SpawnAuthoringState.CatalogKind.Decor:
                return "decor";
            case SpawnAuthoringState.CatalogKind.Hostage:
                return "hostage";
            case SpawnAuthoringState.CatalogKind.Gold:
                return "gold";
            case SpawnAuthoringState.CatalogKind.EventTrap:
                return "eventtrap";
            case SpawnAuthoringState.CatalogKind.EventCore:
                return "eventcore";
            default:
                return "enemy";
        }
    }

    private static bool IsKindToken(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return false;
        raw = raw.Trim();
        return string.Equals(raw, "enemy", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(raw, "trap", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(raw, "lethal", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(raw, "decor", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(raw, "hostage", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(raw, "gold", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(raw, "eventtrap", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(raw, "eventcore", StringComparison.OrdinalIgnoreCase);
    }

    private static SpawnAuthoringState.CatalogKind ParseKind(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return SpawnAuthoringState.CatalogKind.Enemies;
        raw = raw.Trim();
        if (string.Equals(raw, "trap", StringComparison.OrdinalIgnoreCase))
            return SpawnAuthoringState.CatalogKind.Trap;
        if (string.Equals(raw, "lethal", StringComparison.OrdinalIgnoreCase))
            return SpawnAuthoringState.CatalogKind.LethalTrap;
        if (string.Equals(raw, "decor", StringComparison.OrdinalIgnoreCase))
            return SpawnAuthoringState.CatalogKind.Decor;
        if (string.Equals(raw, "hostage", StringComparison.OrdinalIgnoreCase))
            return SpawnAuthoringState.CatalogKind.Hostage;
        if (string.Equals(raw, "gold", StringComparison.OrdinalIgnoreCase))
            return SpawnAuthoringState.CatalogKind.Gold;
        if (string.Equals(raw, "eventtrap", StringComparison.OrdinalIgnoreCase))
            return SpawnAuthoringState.CatalogKind.EventTrap;
        if (string.Equals(raw, "eventcore", StringComparison.OrdinalIgnoreCase))
            return SpawnAuthoringState.CatalogKind.EventCore;
        return SpawnAuthoringState.CatalogKind.Enemies;
    }

    private static int IndexOfFaction(string raw)
    {
        string[] tokens = SpawnAuthoringPackEdit.FactionTokens;
        if (string.IsNullOrEmpty(raw))
            return 0;
        for (int i = 0; i < tokens.Length; i++)
        {
            if (string.Equals(tokens[i], raw.Trim(), StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return 0;
    }

    private static void EnsureLoaded()
    {
        if (loaded && Time.unscaledTime < nextReloadUnscaled)
            return;
        Reload();
        loaded = true;
        nextReloadUnscaled = Time.unscaledTime + 2f;
    }

    private static void Reload()
    {
        entries.Clear();
        string path = GetFilePath();
        if (!File.Exists(path))
        {
            TryWriteStarterFile(path);
            if (!File.Exists(path))
                return;
        }

        try
        {
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line[0] == '#')
                    continue;
                int hash = line.IndexOf('#');
                if (hash >= 0)
                    line = line.Substring(0, hash).Trim();
                if (line.Length == 0)
                    continue;

                if (!TryParseLine(line, out Entry entry))
                    continue;
                entries.Add(entry);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[SPAWN AUTHORING] Favorites load failed: " + ex.Message);
        }
    }

    private static bool TryParseLine(string line, out Entry entry)
    {
        entry = default;
        string[] parts = line.Split('|');
        if (parts.Length < 3)
            return false;

        bool newFormat = parts.Length >= 8 && IsKindToken(parts[1]);
        if (newFormat)
        {
            entry.Label = parts[0].Trim();
            entry.Kind = ParseKind(parts[1]);
            entry.EnemyKey = parts[2].Trim();
            entry.Faction = parts.Length > 3 ? parts[3].Trim() : string.Empty;
            entry.Elite = parts.Length > 4 && IsOne(parts[4]);
            entry.FlipX = parts.Length > 5 && IsOne(parts[5]);
            entry.Count = 1;
            entry.Random = parts.Length > 7 && IsOne(parts[7]);
            entry.Chance = 0.5f;
            if (parts.Length > 8)
            {
                float.TryParse(parts[8].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out entry.Chance);
                entry.Chance = Mathf.Clamp01(entry.Chance);
            }

            if (parts.Length > 9)
                float.TryParse(parts[9].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out entry.RotationZ);
            if (parts.Length > 10)
                int.TryParse(parts[10].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out entry.SortOffset);
            entry.EventCore = parts.Length > 11 ? parts[11].Trim() : string.Empty;
            entry.EventCoreChance = 1f;
            if (parts.Length > 12)
            {
                float.TryParse(parts[12].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out entry.EventCoreChance);
                entry.EventCoreChance = Mathf.Clamp01(entry.EventCoreChance);
            }
            else if (!string.IsNullOrEmpty(entry.EventCore))
                entry.EventCoreChance = 0.5f;
        }
        else
        {
            int keyIndex = parts.Length >= 4 ? 1 : 0;
            if (parts.Length >= 4)
                entry.Label = parts[0].Trim();
            entry.Kind = SpawnAuthoringState.CatalogKind.Enemies;
            entry.EnemyKey = parts[keyIndex].Trim();
            entry.Faction = parts[keyIndex + 1].Trim();
            entry.Elite = IsOne(parts[keyIndex + 2]);
            entry.FlipX = false;
            entry.Count = 1;
            entry.Random = false;
            entry.Chance = 0.5f;
            entry.EventCore = string.Empty;

            int baseIdx = keyIndex + 3;
            if (parts.Length > baseIdx)
                entry.FlipX = IsOne(parts[baseIdx]);
            if (parts.Length > baseIdx + 2)
                entry.Random = IsOne(parts[baseIdx + 2]);
            if (parts.Length > baseIdx + 3)
            {
                float.TryParse(parts[baseIdx + 3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out entry.Chance);
                entry.Chance = Mathf.Clamp01(entry.Chance);
            }
        }

        if (string.IsNullOrEmpty(entry.Label))
        {
            entry.Label = BuildDefaultLabel(
                entry.Kind, entry.EnemyKey, entry.Faction, entry.Elite, entry.FlipX,
                entry.Random, entry.Chance, entry.EventCore);
        }

        return !string.IsNullOrEmpty(entry.EnemyKey);
    }

    private static bool IsOne(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return false;
        raw = raw.Trim();
        return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TrySave(out string error)
    {
        error = null;
        string path = GetFilePath();
        try
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var sb = new StringBuilder();
            sb.AppendLine("# F11 Favorites — one preset per line:");
            sb.AppendLine("# Label|kind|Key|faction|elite|flip|count|random|chance|rot|sort|ec|ecChance");
            sb.AppendLine("# kind: enemy|trap|lethal|decor|hostage|gold|eventtrap|eventcore");
            sb.AppendLine("# elite/flip/random: 0 or 1. faction and ec may be empty. ecChance is EventCore attach p.");
            sb.AppendLine();
            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                string label = (e.Label ?? string.Empty).Replace("|", "/");
                string key = (e.EnemyKey ?? string.Empty).Replace("|", "");
                string faction = (e.Faction ?? string.Empty).Replace("|", "");
                string ec = (e.EventCore ?? string.Empty).Replace("|", "");
                sb.Append(label);
                sb.Append('|');
                sb.Append(KindToken(e.Kind));
                sb.Append('|');
                sb.Append(key);
                sb.Append('|');
                sb.Append(faction);
                sb.Append('|');
                sb.Append(e.Elite ? "1" : "0");
                sb.Append('|');
                sb.Append(e.FlipX ? "1" : "0");
                sb.Append('|');
                sb.Append("1");
                sb.Append('|');
                sb.Append(e.Random ? "1" : "0");
                sb.Append('|');
                sb.Append(e.Chance.ToString("0.##", CultureInfo.InvariantCulture));
                sb.Append('|');
                sb.Append(Mathf.RoundToInt(e.RotationZ).ToString(CultureInfo.InvariantCulture));
                sb.Append('|');
                sb.Append(e.SortOffset.ToString(CultureInfo.InvariantCulture));
                sb.Append('|');
                sb.Append(ec);
                sb.Append('|');
                sb.Append(e.EventCoreChance.ToString("0.##", CultureInfo.InvariantCulture));
                sb.AppendLine();
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            nextReloadUnscaled = Time.unscaledTime + 2f;
            return true;
        }
        catch (Exception ex)
        {
            error = "Favorites save failed: " + ex.Message;
            return false;
        }
    }

    private static void TryWriteStarterFile(string path)
    {
        try
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var sb = new StringBuilder();
            sb.AppendLine("# F11 Favorites — one preset per line:");
            sb.AppendLine("# Label|kind|Key|faction|elite|flip|count|random|chance|rot|sort|ec");
            sb.AppendLine("# kind: enemy|trap|lethal|decor|hostage|gold|eventtrap|eventcore");
            sb.AppendLine("# Example:");
            sb.AppendLine("# Church White Elite|enemy|InquisitionWhite|church|1|0|1|0|1|0|0|");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }
        catch
        {
        }
    }
}
