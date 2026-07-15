using System.Reflection;
using HarmonyLib;
using NoREroMod.Patches.UI.MindBroken;
using NoREroMod.Systems.Cache;
using UnityEngine;

namespace NoREroMod.Systems.EventCore.Core;

/// <summary>
/// Session-scoped gameplay freeze for modal EventCore UI (canvas only; no bone-attached dialogue).
/// Full restore runs once when the active session ends.
/// </summary>
internal static class EventCorePause
{
    private static bool _armed;
    private static float _storedTimeScale = 1f;
    private static bool _storedSousa = true;

    private static GameObject _recoveryRoot;
    private static bool _recoveryWasActive;

    private static GameObject _corruptionRoot;
    private static bool _corruptionWasActive;

    private static GameObject _mbVisualFxRoot;
    private static bool _mbVisualFxWasActive;

    private static GameObject _mbHudOverlayRoot;
    private static bool _mbHudWasActive;

    internal static bool IsFrozen => _armed;

    internal static void BeginSessionFreeze()
    {
        if (_armed)
            return;

        StoreAndHideMindBrokenOverlayRoots();
        EventCoreVanillaUiSuppressor.Begin();

        var ps = UnifiedPlayerCacheManager.GetPlayerStatus();
        _storedTimeScale = Time.timeScale;
        _storedSousa = ps == null || ps._SOUSA;
        _armed = true;

        Time.timeScale = 0f;
        if (ps != null)
            ps._SOUSA = false;
    }

    /// <summary>
    /// Invoked when the modal session fully closes. Restores time scale and player control flags.
    /// </summary>
    internal static void EndSessionFreeze()
    {
        if (!_armed)
            return;

        Time.timeScale = _storedTimeScale > 0f ? _storedTimeScale : 1f;

        var ps = UnifiedPlayerCacheManager.GetPlayerStatus();
        if (ps != null)
        {
            // Do not replay a false snapshot from mid-H-scene; modal must always return control.
            ps._SOUSA = true;
            ps._SOUSAMNG = true;
        }

        _armed = false;

        EventCoreVanillaUiSuppressor.End();
        RestoreMindBrokenOverlayRoots();
    }

    /// <summary>
    /// Hides MindBroken overlay roots for the duration of the session.
    /// Those overlays can be recreated at very high sorting orders and otherwise draw over the modal.
    /// </summary>
    private static void StoreAndHideMindBrokenOverlayRoots()
    {
        CaptureAndHide(MindBrokenRecoverySystem.OverlayCanvasObjectName, ref _recoveryRoot, ref _recoveryWasActive);
        CaptureAndHide(CorruptionCaptionsSystem.OverlayCanvasObjectName, ref _corruptionRoot, ref _corruptionWasActive);
        CaptureAndHide(MindBrokenVisualEffectsSystem.OverlayCanvasObjectName, ref _mbVisualFxRoot, ref _mbVisualFxWasActive);
        CaptureAndHide(MindBrokenUIPatch.OverlayCanvasObjectName, ref _mbHudOverlayRoot, ref _mbHudWasActive);
    }

    private static void CaptureAndHide(string objectName, ref GameObject root, ref bool wasActive)
    {
        root = string.IsNullOrEmpty(objectName) ? null : GameObject.Find(objectName);
        if (root == null)
            return;
        wasActive = root.activeSelf;
        root.SetActive(false);
    }

    private static void RestoreMindBrokenOverlayRoots()
    {
        RestoreOne(ref _recoveryRoot, ref _recoveryWasActive);
        RestoreOne(ref _corruptionRoot, ref _corruptionWasActive);
        RestoreOne(ref _mbVisualFxRoot, ref _mbVisualFxWasActive);
        RestoreOne(ref _mbHudOverlayRoot, ref _mbHudWasActive);
    }

    private static void RestoreOne(ref GameObject root, ref bool wasActive)
    {
        if (root == null)
            return;
        if (wasActive)
            root.SetActive(true);
        root = null;
        wasActive = false;
    }
}

/// <summary>
/// Blocks the player's attack input while EventCore freeze is active.
/// Time.timeScale = 0 does not stop playercon.Update(), so left mouse can still
/// leak into the vanilla Attack action for one frame unless we clear it here.
/// </summary>
[HarmonyPatch(typeof(playercon), "Getinput")]
internal static class EventCorePlayerAttackInputBlockPatch
{
    private static readonly FieldInfo KeyAtkField = AccessTools.Field(typeof(playercon), "key_atk");
    private static readonly FieldInfo KeyAtkPressField = AccessTools.Field(typeof(playercon), "key_atk_press");
    private static readonly FieldInfo KeyAtkUpField = AccessTools.Field(typeof(playercon), "key_atk_up");

    [HarmonyPostfix]
    private static void Postfix(playercon __instance)
    {
        if (__instance == null || !EventCorePause.IsFrozen)
            return;

        KeyAtkField?.SetValue(__instance, false);
        KeyAtkPressField?.SetValue(__instance, false);
        KeyAtkUpField?.SetValue(__instance, false);
    }
}
