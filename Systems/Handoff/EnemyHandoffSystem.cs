using System;
using NoREroMod.Patches.Enemy;
using NoREroMod.Patches.Enemy.CrowInquisition;
using NoREroMod.Patches.Enemy.Six_hand;
using NoREroMod.Patches.Enemy.Kakash;
using NoREroMod.Patches.Enemy.MafiaBossCustom;
using NoREroMod.Systems.Effects;
using NoREroMod.Patches.UI.MindBroken;
using NoREroMod.Systems.CombatAi.Factions;
using NoREroMod.Systems.Rage.Patches;

namespace NoREroMod;

internal static class EnemyHandoffSystem
{
    /// <summary>
    /// Shared handoff counter for ALL enemy types.
    /// If &gt; 0, the next enemy (any type) must start from forced mid, not from the beginning.
    /// Reset on protagonist escape and scene change.
    /// </summary>
    internal static int GlobalHandoffCount;

    internal static void ResetAllData()
    {
        GlobalHandoffCount = 0;

        SafeInvoke(() => TouzokuNormalPassPatch.ResetAll(), "TouzokuNormalPassPatch");
        SafeInvoke(() => TouzokuAxePassPatch.ResetAll(), "TouzokuAxePassPatch");
        SafeInvoke(() => InquisitionBlackPassPatch.ResetAll(), "InquisitionBlackPassPatch");
        SafeInvoke(() => InquisitionWhitePassPatch.ResetAll(), "InquisitionWhitePassPatch");
        SafeInvoke(() => InquisitionRedPassPatch.ResetAll(), "InquisitionRedPassPatch");
        SafeInvoke(() => CrowInquisitionPassLogic.ResetAll(), "CrowInquisitionPassLogic");
        SafeInvoke(() => PrisonOfficerPassPatch.ResetAll(), "PrisonOfficerPassPatch");
        SafeInvoke(() => LibrarianPassPatch.ResetAll(), "LibrarianPassPatch");
        SafeInvoke(() => MummyDogPassPatch.ResetAll(), "MummyDogPassPatch");
        SafeInvoke(() => PilgrimPassPatch.ResetAll(), "PilgrimPassPatch");
        SafeInvoke(() => MummyManPassPatch.ResetAll(), "MummyManPassPatch");
        SafeInvoke(() => UndeadPassPatch.ResetAll(), "UndeadPassPatch");
        SafeInvoke(() => VagrantPassPatch.ResetAll(), "VagrantPassPatch");
        SafeInvoke(() => HSceneBlackBackgroundTriggerPatch.ResetWhiteInqState(), "HSceneBlackBackgroundTriggerPatch");
        SafeInvoke(() => GoblinPassLogic.ResetAll(), "GoblinPassLogic");
        SafeInvoke(() => BigoniBrotherPassLogic.ResetAll(), "BigoniBrotherPassLogic");
        SafeInvoke(() => DoreiPassLogic.ResetAll(), "DoreiPassLogic");
        SafeInvoke(() => MutudePassLogic.ResetAll(), "MutudePassLogic");
        SafeInvoke(() => KakasiPassLogic.ResetAll(), "KakasiPassLogic");
        SafeInvoke(() => KakasiCrossPatch.ResetCrossState(), "KakasiCrossPatch");
        SafeInvoke(() => MafiaBossCustomPassLogic.ResetAll(), "MafiaBossCustomPassLogic");
        SafeInvoke(() => SpawnPointAnalyzer.Reset(), "SpawnPointAnalyzer");
        
        // Clear registered enemies for kill tracking systems
        SafeInvoke(() => RageUniversalKillTrackerPatch.ClearRegisteredEnemies(), "RageUniversalKillTrackerPatch");
        SafeInvoke(() => MindBrokenUniversalKillRecoveryPatch.ClearRegisteredEnemies(), "MindBrokenUniversalKillRecoveryPatch");
        SafeInvoke(() => FactionReputationDynamics.ResetEpisodeData(), "FactionReputationDynamics");
    }

    private static void SafeInvoke(Action action, string context)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
        }
    }
}

