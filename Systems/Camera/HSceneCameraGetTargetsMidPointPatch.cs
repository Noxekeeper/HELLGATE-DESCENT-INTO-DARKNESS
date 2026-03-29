using HarmonyLib;
using UnityEngine;
using Com.LuisPedroFonseca.ProCamera2D;
using NoREroMod;
using NoREroMod.Systems.Camera;
using System.Collections.Generic;
using System.Reflection;

namespace NoREroMod.Systems.Camera;

/// <summary>
/// Harmony patch for GetTargetsWeightedMidPoint(). Returns only pan target position when pan is active.
/// Prevents averaging with other targets (player, enemy) and eliminates jitter.
/// </summary>
[HarmonyPatch(typeof(ProCamera2D), "GetTargetsWeightedMidPoint")]
internal class HSceneCameraGetTargetsMidPointPatch
{
    [HarmonyPrefix]
    private static bool GetTargetsWeightedMidPoint_Prefix(ProCamera2D __instance, ref List<CameraTarget> targets, ref Vector3 __result)
    {
        if (!HSceneCameraDirectPanPatch.HasPanOffset())
        {
            return true;
        }
        
        if (!CameraCache.IsHSceneActive())
        {
            return true;
        }
        
        try
        {
            Transform panTargetTransform = HSceneCameraDirectPanPatch.GetPanTargetTransform();
            if (panTargetTransform != null)
            {
                // Return only pan target position, ignoring all other targets
                // Use world position directly
                // CRITICAL: Do not modify panTarget position here - it's already set in HSceneCameraDirectPanPatch
                Vector3 panTargetPos = panTargetTransform.position;
                
                // Convert to local position relative to parent
                Vector3 parentPos = __instance.ParentPosition;
                Vector3 localPanPos = panTargetPos - parentPos;
                
                __result = localPanPos;
                
                // Skip original method - prevents averaging with other targets
                return false;
            }
        }
        catch
        {
            // Ignore errors, continue with original method
        }
        
        return true;
    }
}

