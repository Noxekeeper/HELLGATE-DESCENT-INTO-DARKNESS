using System;
using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Systems.Pregnancy;

/// <summary>
/// Shared runtime reset for extended pregnancy state (gestation, queued conception, womb meter, vanilla flags).
/// Used by death respawn and altar reset flows.
/// </summary>
internal static class PregnancyRuntimeCleanup
{
    internal static void ClearGestation(string reason)
    {
        bool hadGestation = WitchPregnancyState.IsActive || WitchPregnancyState.HasPending;
        if (!hadGestation)
            return;

        WitchPregnancyState.ClearAll();
        TrimesterProgression.ResetForDeath();
        ClearVanillaPregnancyIndicators();

        if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
            Plugin.Log?.LogInfo($"[Pregnancy] Cleared gestation ({reason}).");
    }

    internal static void ClearWombMeter(string reason)
    {
        if (WitchWombMeter.TotalMl <= 0f)
            return;

        WitchWombMeter.Reset();

        if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
            Plugin.Log?.LogInfo($"[Pregnancy] Cleared womb meter ({reason}).");
    }

    internal static void ClearVanillaPregnancyIndicators()
    {
        try
        {
            var ps = GameObject.FindWithTag("GameController")?.GetComponent<PlayerStatus>();
            if (ps == null)
                return;

            try { ps._BadstatusVal[3] = 0f; }
            catch { }

            Buff buff = null;
            try { buff = Traverse.Create(ps).Field("Buff").GetValue<Buff>(); }
            catch { }

            if (buff == null)
                return;

            try { buff._Pregnancy = false; }
            catch { }

            try { buff.StopPregnancy(); }
            catch { }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[Pregnancy] Vanilla pregnancy indicator clear failed: {ex.Message}");
        }
    }
}
