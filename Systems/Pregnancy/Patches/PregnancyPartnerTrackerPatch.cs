using System;
using HarmonyLib;
using NoREroMod.Systems.CombatAi.Factions;
using UnityEngine;

namespace NoREroMod.Systems.Pregnancy.Patches;

/// <summary>
/// Records the faction of the most recent creampie partner. The ml itself is captured by
/// <see cref="WombMeterNakadashiPoller"/> (polling the native counter, which catches every
/// source), but the precise faction is only reliably known from the <c>EnemyDate</c> instance
/// that calls <c>Nakadasi</c> — <c>QTESystem.GetCurrentEnemyInstance()</c> is null for many
/// non-QTE H-scenes (e.g. Touzoku grabs). The poller consumes <see cref="LastFaction"/> when
/// it is fresh, otherwise falls back to QTE / neutral fill.
///
/// Uses <see cref="Time.unscaledTime"/> so accelerated H-animations (4x via Dash/Step key)
/// do not expire the "freshness" window prematurely.
/// </summary>
[HarmonyPatch(typeof(EnemyDate), "Nakadasi")]
internal static class PregnancyPartnerTrackerPatch
{
    internal static int LastFaction = FactionIds.Neutral;
    internal static float LastUnscaledTime = -999f;

    /// <summary>Freshness window in seconds (unscaled time). Covers up to ~4x animation speed.</summary>
    internal const float FreshnessWindowSeconds = 0.75f;

    [HarmonyPostfix]
    private static void Postfix(EnemyDate __instance)
    {
        try
        {
            if (PregnancyConfig.Enable == null || !PregnancyConfig.Enable.Value)
                return;

            string instanceType = __instance?.GetType()?.Name ?? "null";
            string instanceObjName = __instance?.gameObject?.name ?? "null";
            
            int faction = PregnancySourceResolver.Resolve(__instance, out string diag);
            LastFaction = faction;
            LastUnscaledTime = Time.unscaledTime;

            if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
            {
                string factionName = faction == FactionIds.Neutral ? "NEUTRAL" : $"{faction}";
                Plugin.Log?.LogInfo($"[Pregnancy.Tracker] Nakadasi from {instanceType}({instanceObjName}) -> faction={factionName} (resolver={diag})");
            }
        }
        catch (Exception ex)
        {
            if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
                Plugin.Log?.LogWarning($"[Pregnancy.Tracker] Error: {ex.Message}");
        }
    }
}
