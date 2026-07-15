using System;
using HarmonyLib;
using NoREroMod.Systems.CombatAi.Factions;
using NoREroMod.Systems.Economy;
using UnityEngine;

namespace NoREroMod.Systems.Pregnancy;

/// <summary>
/// Drives the extended pregnancy's real-time trimester progression. It replaces the
/// vanilla <see cref="Buff.PregnancyTime"/> timer with a configurable duration and
/// maps the elapsed time to <c>PlayerStatus._BadstatusVal[3]</c> so the vanilla gauge
/// still acts as a visual progress bar. Birth is triggered when the configured duration
/// elapses and the player is in a safe state.
/// </summary>
internal static class TrimesterProgression
{
    private static bool _birthPending;

    /// <summary>Drop pending birth after death respawn; gestation state is cleared separately.</summary>
    internal static void ResetForDeath()
    {
        _birthPending = false;
    }

    public static void Process(playercon player, PlayerStatus ps, bool eroflag, int erodown)
    {
        if (!PregnancyConfig.IsEnabled)
            return;
        if (player == null || ps == null)
            return;

        if (!WitchPregnancyState.IsActive)
        {
            _birthPending = false;
            return;
        }

        Buff buff = null;
        try { buff = Traverse.Create(ps).Field("Buff").GetValue<Buff>(); }
        catch { }
        if (buff == null)
            return;

        bool nowPregnant = false;
        try { nowPregnant = buff._Pregnancy; }
        catch { }

        // Keep the vanilla pregnancy flag set while we are gestating. Vanilla resets
        // (e.g. BADstatusReset) may clear it; restoring it keeps the birth pipeline intact.
        if (!nowPregnant)
        {
            try { buff._Pregnancy = true; }
            catch { }
        }

        float delta = CanAdvance(player, ps, eroflag, erodown) ? Time.deltaTime : 0f;
        WitchPregnancyState.GestationElapsedSeconds += delta;

        float ratio = WitchPregnancyState.ProgressRatio;
        try { ps._BadstatusVal[3] = ratio * 100f; }
        catch (Exception ex) { Plugin.Log?.LogWarning($"[Pregnancy.Trimester] Failed to set BadstatusVal[3]: {ex.Message}"); }

        if (ratio >= 1f && !_birthPending)
        {
            _birthPending = true;
            if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
                Plugin.Log?.LogInfo("[Pregnancy.Trimester] Pregnancy duration reached, waiting for a safe birth window");
        }

        if (_birthPending && CanBirth(player, ps, eroflag, erodown))
            TriggerBirth(player, ps, buff);
    }

    private static bool CanAdvance(playercon player, PlayerStatus ps, bool eroflag, int erodown)
    {
        if (eroflag || erodown != 0)
            return false;
        if (player._stabnow)
            return false;
        if (player._Death)
            return false;
        if (!ps._SOUSA)
            return false;
        return true;
    }

    private static bool CanBirth(playercon player, PlayerStatus ps, bool eroflag, int erodown)
    {
        if (eroflag || erodown != 0)
            return false;
        if (player._stabnow)
            return false;
        if (player._Death)
            return false;
        if (!ps._SOUSA)
            return false;
        return true;
    }

    private static void TriggerBirth(playercon player, PlayerStatus ps, Buff buff)
    {
        try
        {
            int faction = WitchPregnancyState.SourceFaction;
            Plugin.Log?.LogInfo($"[Pregnancy.Trimester] Triggering birth (source was {Describe(faction)})");

            ps.BirthAction();

            try { buff._Pregnancy = false; }
            catch { }
            try { buff.StopPregnancy(); }
            catch { }
            try { player.fun_costumeRe(); }
            catch { }

            WitchPregnancyState.ClearAll();
            _birthPending = false;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[Pregnancy.Trimester] Birth trigger failed: {ex.Message}");
        }
    }

    private static string Describe(int factionId)
    {
        if (factionId == FactionIds.Neutral)
            return "neutral";
        try { return EconomicFactionUtil.FactionIdToKey(factionId); }
        catch { return $"faction{factionId}"; }
    }
}
