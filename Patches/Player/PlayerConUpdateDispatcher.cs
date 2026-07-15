using HarmonyLib;
using UnityEngine;
using NoREroMod.Systems.Camera;
using NoREroMod.Systems.HSceneEffects;
using NoREroMod.Systems.Rage.Patches;
using NoREroMod.Systems.CombatAi.Factions.Patches;
using NoREroMod.Patches.UI.MindBroken;
using NoREroMod.Systems.UI.Portrait;
using NoREroMod.Systems.Economy;
using Spine.Unity;

namespace NoREroMod.Patches.Player;

/// <summary>
/// Single dispatcher for all <see cref="playercon.Update"/> Harmony postfixes.
/// Replaces many separate Update patches with one postfix that invokes handlers in order.
/// Each handler is wrapped in try/catch so one failure does not break the rest.
/// </summary>
[HarmonyPatch(typeof(playercon), "Update")]
internal static class PlayerConUpdateDispatcher
{
    [HarmonyPostfix]
    private static void Dispatch(playercon __instance, bool ___eroflag, int ___erodown, PlayerStatus ___playerstatus)
    {
        // 0. Downed at 0 HP without a registered death -> force clean SpDeath (game over).
        // Prevents "rise while dead" on the vanilla knockdown recovery path (erodown, no eroflag).
        try { DownedDeathGuard.Process(__instance, ___playerstatus); }
        catch (System.Exception ex) { Plugin.Log?.LogWarning($"[PlayerConUpdate] DownedDeathGuard: {ex.Message}"); }

        // 0. Active Rage knockdown immunity: clear erodown before grab/down penalties (avoids false MB interrupt + one-frame DOWN)
        try { RageActiveImmunityPatch.ProcessUpdateSuppression(__instance); }
        catch (System.Exception ex) { Plugin.Log?.LogWarning($"[PlayerConUpdate] RageKnockdownSuppress: {ex.Message}"); }

        // 1. TimeScale reset when leaving grab / H-scene
        try { TimeScaleResetOnEscapePatch.Process(___eroflag); }
        catch (System.Exception ex) { Plugin.Log?.LogWarning($"[PlayerConUpdate] TimeScaleReset: {ex.Message}"); }

        // 1.1 Restore attack/movement if struggle or trap left _SOUSA / state stuck
        try { PlayerCombatControlRecovery.Process(__instance, ___playerstatus, ___eroflag); }
        catch (System.Exception ex) { Plugin.Log?.LogWarning($"[PlayerConUpdate] CombatControlRecovery: {ex.Message}"); }

        // 1.15 Stop FIN black screen if it stayed on outside H-scene (passive MindBroken tick)
        try { HSceneEscapeStateCleanup.ProcessStuckBlackBackgroundSafetyNet(__instance); }
        catch (System.Exception ex) { Plugin.Log?.LogWarning($"[PlayerConUpdate] BlackBgSafetyNet: {ex.Message}"); }

        // 1.2 Enemy grab H-scenes need _SOUSA for QTE / vanilla struggle exit
        try { PlayerEnemyGrabStruggleSupport.Process(__instance, ___playerstatus); }
        catch (System.Exception ex) { Plugin.Log?.LogWarning($"[PlayerConUpdate] GrabStruggleSupport: {ex.Message}"); }

        // 2. Rage reset on grab/down
        try { RageResetOnGrabDownPatch.Process(__instance); }
        catch (System.Exception ex) { Plugin.Log?.LogWarning($"[PlayerConUpdate] RageReset: {ex.Message}"); }

        // 3. Combat camera presets (V key)
        try { CombatCameraPresetSystem.Process(__instance); }
        catch (System.Exception ex) { Plugin.Log?.LogWarning($"[PlayerConUpdate] CombatCamera: {ex.Message}"); }

        // 4. H-scene start zoom effect
        try { HSceneStartZoomEffect.CheckHSceneStart(__instance); }
        catch (System.Exception ex) { Plugin.Log?.LogWarning($"[PlayerConUpdate] HSceneZoom: {ex.Message}"); }

        // 5. QTE 3.0
        try { QTESystem.Update(___playerstatus, __instance); }
        catch (System.Exception ex) { Plugin.Log?.LogError($"[PlayerConUpdate] QTE: {ex.Message}"); }

        // 6. MindBroken global H-scene growth
        try { H_scenesAllEnemiesCorruption.Process(__instance, ___playerstatus); }
        catch (System.Exception ex) { Plugin.Log?.LogWarning($"[PlayerConUpdate] MindBroken: {ex.Message}"); }

        // 7. Faction reputation bridge: reward on H-scene completion
        try { FactionHSceneReputationBridge.Process(__instance); }
        catch (System.Exception ex) { Plugin.Log?.LogWarning($"[PlayerConUpdate] FactionHSceneRep: {ex.Message}"); }

        // 7.0a Pregnancy: capture ml from the native NakadashiValue counter (all sources)
        try { NoREroMod.Systems.Pregnancy.WombMeterNakadashiPoller.Process(__instance, ___playerstatus); }
        catch (System.Exception ex) { Plugin.Log?.LogWarning($"[PlayerConUpdate] WombMeterPoll: {ex.Message}"); }

        // 7.0b Pregnancy: apply queued womb-meter conception once safely out of the H-scene
        try { NoREroMod.Systems.Pregnancy.PregnancyConceptionApplier.Process(__instance, ___playerstatus, ___eroflag, ___erodown); }
        catch (System.Exception ex) { Plugin.Log?.LogWarning($"[PlayerConUpdate] PregnancyConception: {ex.Message}"); }

        // 7.0c Pregnancy: advance the real-time trimester timer and trigger birth at term
        try { NoREroMod.Systems.Pregnancy.TrimesterProgression.Process(__instance, ___playerstatus, ___eroflag, ___erodown); }
        catch (System.Exception ex) { Plugin.Log?.LogWarning($"[PlayerConUpdate] TrimesterProgression: {ex.Message}"); }

        // 7.0d Pregnancy: passive Rage growth from hideout children
        try { NoREroMod.Systems.Pregnancy.BloodlineRageBonus.Process(Time.deltaTime); }
        catch (System.Exception ex) { Plugin.Log?.LogWarning($"[PlayerConUpdate] BloodlineRageBonus: {ex.Message}"); }

        // 7.0e Pregnancy: periodic trimester visual effects (II and III trimesters)
        try { NoREroMod.Systems.Pregnancy.TrimesterVisualEffects.Process(__instance, ___playerstatus); }
        catch (System.Exception ex) { Plugin.Log?.LogWarning($"[PlayerConUpdate] TrimesterVisualEffects: {ex.Message}"); }

        // 7.0 Gold H-scene earnings bridge: pay player when H-scene cycle completes
        try { GoldHSceneEarningsBridge.Process(__instance); }
        catch (System.Exception ex) { Plugin.Log?.LogWarning($"[PlayerConUpdate] GoldHScene: {ex.Message}"); }

        // 7.0b Knockdown gold loss (erodown edge)
        try { CombatGoldLossRuntime.ProcessKnockdownEdge(__instance, ___erodown, ___eroflag); }
        catch (System.Exception ex) { Plugin.Log?.LogWarning($"[PlayerConUpdate] KnockdownGold: {ex.Message}"); }

        try { NoREroMod.Patches.Enemy.Kakash.KakasiHandoffHide.ProcessPlayerStandCheck(__instance); }
        catch (System.Exception ex) { Plugin.Log?.LogWarning($"[PlayerConUpdate] KakasiHandoffHide: {ex.Message}"); }

        try { NoREroMod.Patches.Enemy.MummyManHandoffHide.ProcessPlayerStandCheck(__instance); }
        catch (System.Exception ex) { Plugin.Log?.LogWarning($"[PlayerConUpdate] MummyManHandoffHide: {ex.Message}"); }

        // 7.1 De-escalation relation roll runtime
        try { NoREroMod.Systems.CombatAi.Factions.FactionDeescalationRuntime.Process(__instance); }
        catch (System.Exception ex) { Plugin.Log?.LogWarning($"[PlayerConUpdate] FactionDeescalation: {ex.Message}"); }
        try { NoREroMod.Systems.CombatAi.Factions.MercyEventUISystem.Process(__instance); }
        catch (System.Exception ex) { Plugin.Log?.LogWarning($"[PlayerConUpdate] MercyUI: {ex.Message}"); }

        // 8. Spawn point analyzer (F11/F12 recording)
        try { NoREroMod.SpawnPointAnalyzer.Process(); }
        catch (System.Exception ex) { Plugin.Log?.LogWarning($"[PlayerConUpdate] SpawnAnalyzer: {ex.Message}"); }

        // 9. Safety recovery: if H-scene already ended but player renderers stayed disabled, restore visuals.
        try { RecoverPlayerRendererAfterHScene(__instance); }
        catch (System.Exception ex) { Plugin.Log?.LogWarning($"[PlayerConUpdate] RendererRecovery: {ex.Message}"); }

        // 10. Custom PNG portrait over UIface (Portrait_mod)
        try { PortraitModSystem.Process(__instance, ___playerstatus, ___eroflag); }
        catch (System.Exception ex) { Plugin.Log?.LogWarning($"[PlayerConUpdate] PortraitMod: {ex.Message}"); }

    }

    private static void RecoverPlayerRendererAfterHScene(playercon player)
    {
        if (player == null) return;
        if (player.eroflag || player.erodown != 0) return;

        GameObject playerObj = NoREroMod.Systems.Cache.UnifiedPlayerCacheManager.GetPlayerObject();
        if (playerObj == null) return;

        SpriteRenderer rootSprite = playerObj.GetComponent<SpriteRenderer>();
        if (rootSprite != null && !rootSprite.enabled && rootSprite.gameObject.activeInHierarchy)
        {
            rootSprite.enabled = true;
        }

        foreach (SpriteRenderer sr in playerObj.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr == null || sr.enabled || !sr.gameObject.activeInHierarchy) continue;
            if (sr.gameObject.name.IndexOf("ero", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (PlayerHitBloodCleanupPatch.IsUnderPlayerBloodHierarchy(sr.transform)) continue;
            sr.enabled = true;
        }

        foreach (MeshRenderer mr in playerObj.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (mr == null || mr.enabled || !mr.gameObject.activeInHierarchy) continue;
            if (mr.gameObject.name.IndexOf("ero", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (PlayerHitBloodCleanupPatch.IsUnderPlayerBloodHierarchy(mr.transform)) continue;
            mr.enabled = true;
        }

        SkeletonAnimation[] spines = playerObj.GetComponentsInChildren<SkeletonAnimation>(true);
        for (int i = 0; i < spines.Length; i++)
            HSceneEscapeStateCleanup.RestoreSkeleton(spines[i]);
    }
}
