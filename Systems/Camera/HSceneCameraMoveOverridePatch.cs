using HarmonyLib;
using UnityEngine;
using Com.LuisPedroFonseca.ProCamera2D;
using NoREroMod;
using NoREroMod.Systems.Camera;
using System.Reflection;

namespace NoREroMod.Systems.Camera;

/// <summary>
/// Harmony patch for Move() method. Overrides camera position calculation when pan is active.
/// Uses only pan target instead of averaging all targets.
/// </summary>
[HarmonyPatch(typeof(ProCamera2D), "Move")]
internal class HSceneCameraMoveOverridePatch
{
    [HarmonyPrefix]
    private static void Move_Prefix(ProCamera2D __instance, float deltaTime, ref Vector3 __state)
    {
        if (!HSceneCameraDirectPanPatch.HasPanOffset())
        {
            return;
        }
        
        if (!CameraCache.IsHSceneActive())
        {
            return;
        }
        
        try
        {
            CameraCache.InitializeProCamera2DReflection();
            
            var panTargetTransform = HSceneCameraDirectPanPatch.GetPanTargetTransform();
            if (panTargetTransform == null)
            {
                return;
            }
            
            // CRITICAL: Use already set pan target position
            // Don't recalculate - position already set in HSceneCameraDirectPanPatch
            Vector3 panTargetPos = panTargetTransform.position;
            
            // CRITICAL: Sync _previousTargetsMidPoint with _targetsMidPoint BEFORE update
            // Prevents difference calculation that may cause jitter
            if (CameraCache.PreviousTargetsMidPointField != null && CameraCache.TargetsMidPointField != null)
            {
                Vector3 currentTargetsMidPoint = (Vector3)CameraCache.TargetsMidPointField.GetValue(__instance);
                CameraCache.PreviousTargetsMidPointField.SetValue(__instance, currentTargetsMidPoint);
            }
            
            // Override _targetsMidPoint to pan target position (use cached FieldInfo)
            // Done BEFORE ProCamera2D calculates position to prevent averaging
            if (CameraCache.TargetsMidPointField != null)
            {
                CameraCache.TargetsMidPointField.SetValue(__instance, panTargetPos);
            }
        }
        catch
        {
            // Ignore errors
        }
    }
    
}

