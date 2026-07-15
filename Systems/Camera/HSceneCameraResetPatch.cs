using HarmonyLib;
using Com.LuisPedroFonseca.ProCamera2D;
using NoREroMod;
using NoREroMod.Systems.Camera;

namespace NoREroMod.Systems.Camera;

/// <summary>
/// Harmony patch for resetting zoom when fun_cameraReset is called (H-scene end).
/// </summary>
[HarmonyPatch(typeof(playercon), "fun_cameraReset")]
internal class HSceneCameraResetPatch
{
    [HarmonyPostfix]
    private static void fun_cameraReset_Postfix(playercon __instance)
    {
        HSceneCameraZoomControlPatch.ResetZoom(__instance);
        CombatCameraPresetSystem.ResetToStandard();
    }
}
