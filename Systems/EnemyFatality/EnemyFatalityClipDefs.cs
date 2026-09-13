using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace NoREroMod.Systems.EnemyFatality;

/// <summary>
/// Per-clip fatality gates shared across enemy profiles.
/// Sections: <c>[EnemyFatality.Clip.lost_leg]</c>, <c>lost_head</c>, <c>StillAlive</c>, …
/// Enable / Chance (weight) / HpThresholdPercent are independent per clip.
/// </summary>
internal static class EnemyFatalityClipDefs
{
    internal sealed class ClipDef
    {
        internal string Id;
        internal string RelativePath;
        internal ConfigEntry<bool> Enable;
        internal ConfigEntry<float> Chance;
        internal ConfigEntry<float> HpThresholdPercent;

        internal bool IsEnabled => Enable != null && Enable.Value;

        internal float ChanceWeight =>
            Chance != null ? Mathf.Max(0f, Chance.Value) : 0f;

        internal float HpThreshold =>
            HpThresholdPercent != null ? Mathf.Clamp01(HpThresholdPercent.Value) : 0.2f;
    }

    private static readonly Dictionary<string, ClipDef> ById =
        new Dictionary<string, ClipDef>(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, ClipDef> ByRelative =
        new Dictionary<string, ClipDef>(StringComparer.OrdinalIgnoreCase);

    private static bool _initialized;

    /// <summary>Production defaults: below 20% HP, equal relative weight among eligible clips.</summary>
    private const float DefaultHpThresholdPercent = 0.2f;
    private const float DefaultChanceWeight = 1f;

    public static void Initialize()
    {
        if (_initialized)
            return;
        _initialized = true;

        ConfigFile cfg = Plugin.Instance.Config;

        Register(
            cfg,
            "lost_leg",
            EnemyFatalityProfileCatalog.LostLegClipRelative,
            "Enable the lost_leg combat-fatality clip in the shared pool. Off = never selected.");

        Register(
            cfg,
            "lost_head",
            EnemyFatalityProfileCatalog.LostHeadClipRelative,
            "Enable the lost_head (Lost_head) combat-fatality clip in the shared pool. Off = never selected.");

        Register(
            cfg,
            "StillAlive",
            EnemyFatalityProfileCatalog.StillAliveClipRelative,
            "Enable the StillAlive combat-fatality clip (CrawlingCreatures). Off = that enemy never fatality via this clip.");

        Register(
            cfg,
            "HeavyCritical",
            EnemyFatalityProfileCatalog.HeavyCriticalClipRelative,
            "Enable the HeavyCritical combat-fatality clip (magic projectile killers only). Off = never selected.");
    }

    internal static ClipDef GetById(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        ClipDef def;
        return ById.TryGetValue(id.Trim(), out def) ? def : null;
    }

    internal static ClipDef GetByRelative(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
            return null;

        string key = NormalizeRelative(relativePath);
        ClipDef def;
        if (ByRelative.TryGetValue(key, out def))
            return def;

        // Folder-name fallback (lost_head, lost_leg, StillAlive).
        string name = GetFolderName(key);
        return GetById(name);
    }

    internal static bool PassesHpGate(float hpThresholdPercent, float hpRatio)
    {
        float threshold = Mathf.Clamp01(hpThresholdPercent);
        // threshold 1 (= 100%) = any HP (test / "below 100%" edge).
        if (threshold >= 1f)
            return true;
        return hpRatio < threshold;
    }

    private static void Register(
        ConfigFile cfg,
        string id,
        string relativePath,
        string enableDescription,
        float defaultHpThresholdPercent = DefaultHpThresholdPercent,
        float defaultChanceWeight = DefaultChanceWeight)
    {
        string section = "EnemyFatality.Clip." + id;
        var def = new ClipDef
        {
            Id = id,
            RelativePath = relativePath,
        };

        def.Enable = cfg.Bind(section, "Enable", true, enableDescription);

        def.HpThresholdPercent = cfg.Bind(section, "HpThresholdPercent", defaultHpThresholdPercent,
            "This clip is only eligible if player HP / max HP is below this value before the hit (0.2 = below 20%). Use 1 = any HP. Independent of other clips and of per-enemy HpThresholdPercent.");

        def.Chance = cfg.Bind(section, "Chance", defaultChanceWeight,
            "Relative weight when several clips pass their HP gate (higher = more often). Default 1 = equal odds in a pool. 0 = never picked. Fatality fire rate is per-enemy Chance, not this key.");

        ById[id] = def;
        ByRelative[NormalizeRelative(relativePath)] = def;

        EnemyFatalityConfig.LogDebug(
            "[EnemyFatality] Clip def registered: " + id + " → " + relativePath);
    }

    private static string NormalizeRelative(string relativePath)
    {
        return relativePath.Replace('\\', '/').Trim().Trim('/');
    }

    private static string GetFolderName(string normalizedRelative)
    {
        int slash = normalizedRelative.LastIndexOf('/');
        if (slash < 0 || slash >= normalizedRelative.Length - 1)
            return normalizedRelative;
        return normalizedRelative.Substring(slash + 1);
    }
}
