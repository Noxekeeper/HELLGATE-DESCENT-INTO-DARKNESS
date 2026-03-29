using HarmonyLib;
using UnityEngine;
using Com.LuisPedroFonseca.ProCamera2D;
using NoREroMod;
using NoREroMod.Systems.Camera;

namespace NoREroMod.Systems.Camera;

/// <summary>
/// Harmony patch to prevent camera position reset during H-scenes.
/// </summary>
[HarmonyPatch(typeof(ProCamera2D), "RemoveCameraTarget")]
internal class HSceneCameraPreventResetPatch
{
    [HarmonyPrefix]
    private static bool RemoveCameraTarget_Prefix(ProCamera2D __instance, Transform targetTransform, float duration)
    {
        if (!CameraCache.IsHSceneActive())
        {
            return true;
        }
        
        // Check if trying to remove our pan target
        if (targetTransform != null && targetTransform.name == "HScenePanTarget")
        {
            // Don't allow removal of our pan target during H-scene
            return false;
        }
        
        return true;
    }
}

