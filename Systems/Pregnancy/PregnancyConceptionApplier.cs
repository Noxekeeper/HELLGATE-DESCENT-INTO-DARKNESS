using System;
using HarmonyLib;
using NoREroMod.Systems.CombatAi.Factions;
using NoREroMod.Systems.Economy;

namespace NoREroMod.Systems.Pregnancy;

/// <summary>
/// Applies a queued conception once the player is safely out of the H-scene.
/// Driven each frame from <c>PlayerConUpdateDispatcher</c>.
///
/// The womb meter only QUEUES a conception (<see cref="WitchPregnancyState.PendingFaction"/>);
/// here we wait for <c>!eroflag &amp;&amp; erodown==0</c> and no active gestation, then start the
/// vanilla pregnancy (costume + <c>Buff.Pregnancystart</c>) tagged with the dominant faction.
/// Vanilla <c>Buff.PregnancyTime</c> still drives gestation to birth in this milestone.
/// </summary>
internal static class PregnancyConceptionApplier
{
    private static bool _wasPregnant;

    public static void Process(playercon player, PlayerStatus ps, bool eroflag, int erodown)
    {
        if (PregnancyConfig.Enable == null || !PregnancyConfig.Enable.Value)
            return;
        if (player == null || ps == null)
            return;

        Buff buff = null;
        try { buff = Traverse.Create(ps).Field("Buff").GetValue<Buff>(); }
        catch { }
        if (buff == null)
            return;

        bool nowPregnant = false;
        try { nowPregnant = buff._Pregnancy; }
        catch { }

        // A pregnancy that was active just ended (birth / reset) -> clear our source tag
        // and empty the womb so it can begin filling again for the next cycle.
        if (_wasPregnant && !nowPregnant && WitchPregnancyState.IsActive)
        {
            if (IsDebug)
                Plugin.Log?.LogInfo($"[Pregnancy] Gestation ended (source was {Describe(WitchPregnancyState.SourceFaction)}).");
            WitchPregnancyState.SourceFaction = FactionIds.Neutral;
            WitchWombMeter.Reset();
        }
        _wasPregnant = nowPregnant;

        if (!WitchPregnancyState.HasPending)
            return;
        if (nowPregnant)
            return; // wait for the current gestation to finish
        if (eroflag || erodown != 0)
            return; // apply only when out of the H-scene

        ApplyConception(player, ps, buff);
    }

    private static void ApplyConception(playercon player, PlayerStatus ps, Buff buff)
    {
        int faction = WitchPregnancyState.PendingFaction;

        // Phase 2 will choose the birth scene / offspring prefab by faction.
        try { player._BirthNumber = 1; }
        catch { }

        try { player.fun_costumePregnant(); }
        catch (Exception ex) { Plugin.Log?.LogWarning($"[Pregnancy] fun_costumePregnant failed: {ex.Message}"); }

        try { buff.Pregnancystart(); }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[Pregnancy] Pregnancystart failed: {ex.Message}");
            return; // keep pending; retry next frame
        }

        // Mirror vanilla's conception counter bump (setter accumulates).
        try { ps._harami = 1; }
        catch { }

        WitchPregnancyState.SourceFaction = faction;
        WitchPregnancyState.PendingFaction = FactionIds.Neutral;
        WitchPregnancyState.ResetGestation();
        _wasPregnant = true;

        // Pass faction to birth spawn override for offspring creation
        try { Patches.BirthSpawnOverridePatch.SetConceptionFaction(faction); }
        catch (Exception ex) { Plugin.Log?.LogWarning($"[Pregnancy] Failed to set conception faction for birth: {ex.Message}"); }

        Plugin.Log?.LogInfo($"[Pregnancy] *** PREGNANCY STARTED from {Describe(faction)}. Vanilla gestation now runs to birth.");
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
