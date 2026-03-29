using System;
using HarmonyLib;
using UnityEngine;
using NoREroMod;
using NoREroMod.Patches.UI.MindBroken;
using NoREroMod.Systems.Rage;

namespace NoREroMod.Systems.PlayerRespawn;

/// <summary>
/// On Take Vengeance (respawn at altar after death):
/// - Reduces MindBroken by configurable fraction of current value
/// - Rage: optional drain, flat bonus, then optional max cap (default 10%)
/// </summary>
internal static class VengeanceRespawnEffectPatch
{
    /// <summary>Prevents double application when both REgame.pl_REstrat Postfix and PlayerStatus.REstrat fire in the same frame.</summary>
    private static int _lastApplyFrame = -1;

    /// <summary>
    /// Apply vengeance effects. Call from death menu patch or BadEnd Take Vengeance.
    /// </summary>
    internal static void ApplyVengeanceEffects()
    {
        if (Time.frameCount == _lastApplyFrame)
        {
            return;
        }

        _lastApplyFrame = Time.frameCount;

        try
        {
            if (MindBrokenSystem.Enabled)
            {
                float frac = Mathf.Clamp01(Plugin.vengeanceMindBrokenReduceFraction?.Value ?? 0.9f);
                float current = MindBrokenSystem.Percent;
                if (current > 0.001f && frac > 0f)
                {
                    float reduction = current * frac;
                    MindBrokenSystem.AddPercent(-reduction, "vengeance_respawn");
                    Plugin.Log?.LogInfo($"[PlayerRespawn] MindBroken reduced by {reduction * 100f:F1}% (was {current * 100f:F1}%)");
                }
            }

            if (RageSystem.Enabled)
            {
                float drainFrac = Mathf.Clamp01(Plugin.vengeanceRageDrainFractionOfCurrent?.Value ?? 0f);
                if (drainFrac > 0.0001f)
                {
                    float currentRage = RageSystem.Percent;
                    float loss = currentRage * drainFrac;
                    if (loss > 0.0001f)
                    {
                        RageSystem.AddRage(-loss, "vengeance_respawn_drain");
                        Plugin.Log?.LogInfo($"[PlayerRespawn] Rage -{loss:F1}% (drain {drainFrac * 100f:F0}% of current) before Take Vengeance bonus");
                    }
                }

                float rageBonus = Mathf.Max(0f, Plugin.vengeanceRageBonusPercent?.Value ?? 10f);
                RageSystem.AddRage(rageBonus, "vengeance_respawn");
                Plugin.Log?.LogInfo($"[PlayerRespawn] Rage +{rageBonus}% on Take Vengeance");

                float maxAfter = Plugin.vengeanceRageMaxPercentAfter?.Value ?? 10f;
                if (maxAfter >= 0f)
                {
                    float rageUpper = Plugin.rageTier3OverflowThreshold?.Value ?? 103f;
                    maxAfter = Mathf.Clamp(maxAfter, 0f, rageUpper);
                    float now = RageSystem.Percent;
                    if (now > maxAfter + 0.001f)
                    {
                        RageSystem.AddRage(maxAfter - now, "vengeance_rage_cap");
                        Plugin.Log?.LogInfo($"[PlayerRespawn] Rage capped to {maxAfter:F1}% (was {now:F1}%)");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[PlayerRespawn] ApplyVengeanceEffects: {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// Run after vanilla restart logic so Rage/MindBroken are not overwritten by pl_REstrat body.
    /// </summary>
    [HarmonyPatch(typeof(REgame), nameof(REgame.pl_REstrat))]
    [HarmonyPostfix]
    private static void Pl_REstrat_Postfix()
    {
        try
        {
            ApplyVengeanceEffects();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[PlayerRespawn] Error in pl_REstrat postfix: {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// BadEnd Take Vengeance calls PlayerStatus.REstrat(), which may not go through REgame.pl_REstrat.
    /// </summary>
    [HarmonyPatch(typeof(PlayerStatus), nameof(PlayerStatus.REstrat))]
    [HarmonyPostfix]
    private static void PlayerStatus_REstrat_Postfix()
    {
        try
        {
            ApplyVengeanceEffects();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[PlayerRespawn] Error in PlayerStatus.REstrat postfix: {ex.Message}\n{ex.StackTrace}");
        }
    }

}
