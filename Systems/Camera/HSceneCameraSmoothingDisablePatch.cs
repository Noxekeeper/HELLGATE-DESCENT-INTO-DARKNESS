using HarmonyLib;
using UnityEngine;
using Com.LuisPedroFonseca.ProCamera2D;
using NoREroMod;
using NoREroMod.Systems.Camera;
using System.Reflection;

namespace NoREroMod.Systems.Camera;

/// <summary>
/// Harmony patch to disable camera smoothing when pan is active.
/// Prevents smooth camera return.
/// </summary>
[HarmonyPatch(typeof(ProCamera2D), "Move")]
internal class HSceneCameraSmoothingDisablePatch
{
    [HarmonyPostfix]
    private static void Move_Postfix(ProCamera2D __instance, float deltaTime)
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
            
            // CRITICAL: After ProCamera2D calculates smoothed positions,
            // override them with exact pan target position
            // Disables smoothing and prevents camera return
            // Use already set position - don't recalculate
            Vector3 panTargetPos = panTargetTransform.position;
            Vector3 parentPos = __instance.ParentPosition;
            Vector3 localPanPos = panTargetPos - parentPos;
            
            // Get Vector3H and Vector3V through cached fields
            if (CameraCache.Vector3HField == null || CameraCache.Vector3VField == null)
            {
                return;
            }
            
            var vector3HFunc = CameraCache.Vector3HField.GetValue(__instance) as System.Func<Vector3, float>;
            var vector3VFunc = CameraCache.Vector3VField.GetValue(__instance) as System.Func<Vector3, float>;
            
            if (vector3HFunc == null || vector3VFunc == null)
            {
                return;
            }
            
            float h = vector3HFunc(localPanPos);
            float v = vector3VFunc(localPanPos);
            
            // CRITICAL: Override smoothed positions with exact pan target values EVERY FRAME
            // Completely disables smoothing and prevents jitter
            // Done AFTER ProCamera2D calculations (in Postfix)
            if (CameraCache.CameraTargetHorizontalPositionSmoothedField != null)
            {
                CameraCache.CameraTargetHorizontalPositionSmoothedField.SetValue(__instance, h);
            }
            if (CameraCache.CameraTargetVerticalPositionSmoothedField != null)
            {
                CameraCache.CameraTargetVerticalPositionSmoothedField.SetValue(__instance, v);
            }
            
            // CRITICAL: Update previous smoothed IMMEDIATELY after current, so smoothing is NOT applied
            // Critical for preventing jitter - previous must equal current
            if (CameraCache.PreviousCameraTargetHorizontalPositionSmoothedField != null)
            {
                CameraCache.PreviousCameraTargetHorizontalPositionSmoothedField.SetValue(__instance, h);
            }
            if (CameraCache.PreviousCameraTargetVerticalPositionSmoothedField != null)
            {
                CameraCache.PreviousCameraTargetVerticalPositionSmoothedField.SetValue(__instance, v);
            }
            
            // ADDITIONAL: Override _cameraTargetPosition for full control
            // Ensures camera follows panTarget exactly
            if (CameraCache.CameraTargetPositionField != null)
            {
                CameraCache.CameraTargetPositionField.SetValue(__instance, localPanPos);
            }
            
            // ADDITIONAL: Override _previousTargetsMidPoint to prevent difference calculation
            // May cause jitter if previous position differs from current
            if (CameraCache.PreviousTargetsMidPointField != null)
            {
                CameraCache.PreviousTargetsMidPointField.SetValue(__instance, panTargetPos);
            }
            
            // CRITICAL: Also override _targetsMidPoint in Postfix to ensure it hasn't changed
            // Prevents any position changes after calculations
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
