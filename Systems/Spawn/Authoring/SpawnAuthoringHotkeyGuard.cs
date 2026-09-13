using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// While F11 authoring UI is on, F1 belongs to the spawn help panel.
/// Blocks other plugins that poll <see cref="Input.GetKeyDown"/> / GetKeyUp.
/// Does not patch Event.keyCode or Input.GetKey — those broke the IMGUI overlay
/// (Use() on Layout/Repaint and a global keyCode hook made chrome vanish for seconds).
/// </summary>
internal static class SpawnAuthoringHotkeyGuard
{
    internal static bool OwnsF1()
    {
        if (!global::NoREroMod.SpawnPointAnalyzer.IsRecordingModeActive)
            return false;
        if (SpawnAuthoringConfig.UiEnable != null && !SpawnAuthoringConfig.UiEnable.Value)
            return false;
        return true;
    }

    private static bool IsF1Name(string name)
    {
        return !string.IsNullOrEmpty(name) &&
               string.Equals(name, "f1", System.StringComparison.OrdinalIgnoreCase);
    }

    [HarmonyPatch(typeof(Input), nameof(Input.GetKeyDown), typeof(KeyCode))]
    private static class GetKeyDownKeyCode
    {
        private static bool Prefix(KeyCode key, ref bool __result)
        {
            if (key != KeyCode.F1 || !OwnsF1())
                return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Input), nameof(Input.GetKeyUp), typeof(KeyCode))]
    private static class GetKeyUpKeyCode
    {
        private static bool Prefix(KeyCode key, ref bool __result)
        {
            if (key != KeyCode.F1 || !OwnsF1())
                return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Input), nameof(Input.GetKeyDown), typeof(string))]
    private static class GetKeyDownName
    {
        private static bool Prefix(string name, ref bool __result)
        {
            if (!IsF1Name(name) || !OwnsF1())
                return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Input), nameof(Input.GetKeyUp), typeof(string))]
    private static class GetKeyUpName
    {
        private static bool Prefix(string name, ref bool __result)
        {
            if (!IsF1Name(name) || !OwnsF1())
                return true;
            __result = false;
            return false;
        }
    }
}
