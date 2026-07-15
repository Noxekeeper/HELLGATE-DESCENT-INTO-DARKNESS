using System;
using System.Collections.Generic;
using NoREroMod.Systems.CombatAi.Factions;
using NoREroMod.Systems.Economy;
using UnityEngine;

namespace NoREroMod.Systems.Pregnancy;

/// <summary>
/// The "milliliters" model that replaces the vanilla creampie scale.
/// Each creampie contributes a faction-typed volume of seed into a buffer (the womb).
/// While the buffer is below <see cref="Capacity"/> the witch is "safe"; once it fills,
/// conception is guaranteed and the dominant faction (by volume) becomes the source.
///
/// Milestone 1: this meter only OBSERVES — it accumulates ml, drives the HUD, logs the
/// conception event and resets. It does not yet take over the vanilla pregnancy pipeline.
/// </summary>
internal static class WitchWombMeter
{
    private static readonly Dictionary<int, float> _seedByFaction = new Dictionary<int, float>();
    private static float _totalMl;

    /// <summary>Raised when the womb reaches capacity. Argument is the dominant source faction id.</summary>
    public static event Action<int> OnConception;

    public static float TotalMl => _totalMl;

    public static float Capacity
    {
        get
        {
            float c = PregnancyConfig.WombCapacityMl != null ? PregnancyConfig.WombCapacityMl.Value : 500f;
            return c <= 1f ? 500f : c;
        }
    }

    public static float FillRatio => Mathf.Clamp01(_totalMl / Capacity);

    /// <summary>
    /// Record a creampie of <paramref name="rawFactionId"/> worth <paramref name="ml"/> milliliters.
    /// Returns true if this contact filled the womb and triggered conception.
    /// </summary>
    public static bool AddSeed(int rawFactionId, float ml)
    {
        if (PregnancyConfig.Enable == null || !PregnancyConfig.Enable.Value)
            return false;
        if (ml <= 0f)
            return false;

        // Womb is occupied while a conception is queued or a gestation is in progress.
        // New seed is ignored until the current pregnancy ends (meter stays empty).
        if (WitchPregnancyState.IsActive || WitchPregnancyState.HasPending)
        {
            if (IsDebug)
                Plugin.Log?.LogInfo("[Pregnancy] Seed ignored (womb occupied by current pregnancy).");
            return false;
        }

        if (PregnancyConfig.IsFactionBlocked(rawFactionId))
        {
            if (IsDebug)
                Plugin.Log?.LogInfo($"[Pregnancy] Seed ignored (blocked/invalid source faction={rawFactionId}).");
            return false;
        }

        int faction = PregnancyConfig.NormalizeSourceFaction(rawFactionId);

        float current;
        _seedByFaction.TryGetValue(faction, out current);
        _seedByFaction[faction] = current + ml;
        _totalMl += ml;

        if (IsDebug)
            Plugin.Log?.LogInfo($"[Pregnancy] +{ml:0.#}ml from {Describe(faction)} | total={_totalMl:0.#}/{Capacity:0}ml ({FillRatio * 100f:0}%)");

        if (_totalMl >= Capacity)
        {
            TriggerConception();
            return true;
        }
        return false;
    }

    public static int GetDominantFaction()
    {
        int best = FactionIds.Neutral;
        float bestMl = -1f;
        foreach (var kv in _seedByFaction)
        {
            if (kv.Value > bestMl)
            {
                bestMl = kv.Value;
                best = kv.Key;
            }
        }
        return best;
    }

    public static void Reset()
    {
        _seedByFaction.Clear();
        _totalMl = 0f;
    }

    private static void TriggerConception()
    {
        int dominant = GetDominantFaction();

        // A purely neutral/factionless womb (traps, slimes, unaffiliated creatures) still
        // produces offspring — treat it as a generic Monster pregnancy.
        if (dominant == FactionIds.Neutral)
            dominant = FactionIds.Monsters;

        Plugin.Log?.LogInfo($"[Pregnancy] >>> WOMB FULL at {_totalMl:0.#}ml. Dominant source = {Describe(dominant)}. Queuing conception.");

        WitchPregnancyState.QueueConception(dominant);

        // Show the conception clip once, here (we are inside the active H-scene at womb-full).
        try { NoREroMod.Patches.Effects.PregnancyClipTrigger.ShowConceptionClip(); }
        catch (Exception ex) { Plugin.Log?.LogWarning($"[Pregnancy] conception clip failed: {ex.Message}"); }

        try { OnConception?.Invoke(dominant); }
        catch (Exception ex) { Plugin.Log?.LogWarning($"[Pregnancy] OnConception handler threw: {ex.Message}"); }

        Reset();
    }

    private static bool IsDebug => PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value;

    private static string Describe(int factionId)
    {
        string key;
        try { key = EconomicFactionUtil.FactionIdToKey(factionId); }
        catch { key = null; }
        if (string.IsNullOrEmpty(key))
            key = "faction" + factionId;
        return key + "(" + factionId + ")";
    }
}
