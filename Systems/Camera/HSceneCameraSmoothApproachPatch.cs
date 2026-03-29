using HarmonyLib;
using UnityEngine;
using Com.LuisPedroFonseca.ProCamera2D;
using NoREroMod;
using NoREroMod.Systems.Camera;

namespace NoREroMod.Systems.Camera;

/// <summary>
/// Harmony patch for SmoothApproach. Disables smoothing when pan is active.
/// Prevents smoothing application even when HorizontalFollowSmoothness = 0.
/// </summary>
[HarmonyPatch(typeof(Utils), "SmoothApproach")]
internal class HSceneCameraSmoothApproachPatch
{
    [HarmonyPrefix]
    private static bool SmoothApproach_Prefix(float pastPosition, float pastTargetPosition, float targetPosition, float speed, float deltaTime, ref float __result)
    {
        if (!HSceneCameraDirectPanPatch.HasPanOffset())
        {
            return true;
        }
        
        if (!CameraCache.IsHSceneActive())
        {
            return true;
        }
        
        // CRITICAL: When pan is active, return targetPosition directly without smoothing
        // Completely disables smoothing and prevents jitter
        __result = targetPosition;
        return false;
    }
}
