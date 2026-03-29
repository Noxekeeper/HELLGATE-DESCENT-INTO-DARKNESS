using HarmonyLib;
using UnityEngine;
using NoREroMod.Systems.Camera;
using NoREroMod.Systems.HSceneEffects;
using NoREroMod.Systems.Rage.Patches;
using NoREroMod.Patches.UI.MindBroken;

namespace NoREroMod.Patches.Player;

/// <summary>
/// Единый диспетчер for всех патчей playercon.Update.
/// Вместо 7 separate Harmony-постфиксоin — один постфикс, вызывающий все обработчики by порядку.
/// Каждый обработчик обёрнут in try/catch for изоляции ошибок.
/// </summary>
[HarmonyPatch(typeof(playercon), "Update")]
internal static class PlayerConUpdateDispatcher
{
    [HarmonyPostfix]
    private static void Dispatch(playercon __instance, bool ___eroflag, int ___erodown, PlayerStatus ___playerstatus)
    {
        // 1. TimeScale reset on выходе from захвата
        try { TimeScaleResetOnEscapePatch.Process(___eroflag); }
        catch (System.Exception ex) { Plugin.Log?.LogWarning($"[PlayerConUpdate] TimeScaleReset: {ex.Message}"); }

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

        // 7. Spawn point analyzer (F11/F12 recording)
        try { NoREroMod.SpawnPointAnalyzer.Process(); }
        catch (System.Exception ex) { Plugin.Log?.LogWarning($"[PlayerConUpdate] SpawnAnalyzer: {ex.Message}"); }

    }
}
