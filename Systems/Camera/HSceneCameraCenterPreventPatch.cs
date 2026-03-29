using HarmonyLib;
using UnityEngine;
using Com.LuisPedroFonseca.ProCamera2D;
using NoREroMod;
using NoREroMod.Systems.Camera;

namespace NoREroMod.Systems.Camera;

/// <summary>
/// Harmony patch to prevent camera centering during H-scenes.
/// Blocks CenterOnTargets if pan was used via arrow keys.
/// </summary>
[HarmonyPatch(typeof(ProCamera2D), "CenterOnTargets")]
internal class HSceneCameraCenterPreventPatch
{
    [HarmonyPrefix]
    private static bool CenterOnTargets_Prefix(ProCamera2D __instance)
    {
        if (!CameraCache.IsHSceneActive())
        {
            return true;
        }
        
        if (HSceneCameraDirectPanPatch.HasPanOffset())
        {
            // Skip centering if pan was used
            return false;
        }
        
        return true;
    }
}

