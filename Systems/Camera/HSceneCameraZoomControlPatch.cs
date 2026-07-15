using HarmonyLib;
using Com.LuisPedroFonseca.ProCamera2D;
using NoREroMod;
using NoREroMod.Systems.Camera;
using UnityEngine;

namespace NoREroMod.Systems.Camera;

/// <summary>
/// Harmony patch for spacebar zoom control during H-scenes.
/// Cycles through zoom levels: 1.5x → 3x → 5x → 1.5x (see [HSceneCameraZoom] in config).
/// Resets zoom to <see cref="Plugin.cameraZoomResetValue"/> when H-scene ends.
/// </summary>
[HarmonyPatch(typeof(playercon), "fun_cameraEROZOOM")]
internal class HSceneCameraZoomControlPatch
{
    private static bool _wasHSceneActive = false;

    private static float ZoomLow => Plugin.cameraZoomResetValue?.Value ?? 1.5f;
    private static float ZoomMid => Plugin.cameraZoomLevel3x?.Value ?? 3f;
    private static float ZoomHigh => Plugin.cameraZoomLevel5x?.Value ?? 5f;

    /// <summary>
    /// Resets zoom to configured default value. Called from fun_cameraReset patch.
    /// </summary>
    internal static void ResetZoom(playercon playerCon)
    {
        try
        {
            CameraCache.InitializePlayerconReflection();
            if (CameraCache.FitzoomField == null)
            {
                return;
            }

            ProCamera2DZoomToFitTargets fitzoom = CameraCache.FitzoomField.GetValue(playerCon) as ProCamera2DZoomToFitTargets;
            if (fitzoom != null)
            {
                ApplyZoomLevel(fitzoom, ZoomLow);
            }
        }
        catch { }
    }

    private static void ApplyZoomLevel(ProCamera2DZoomToFitTargets fitzoom, float level)
    {
        fitzoom.MaxZoomInAmount = level;
        fitzoom.MaxZoomOutAmount = level;
    }

    private static bool IsZoomLevel(float current, float level)
    {
        return Mathf.Approximately(current, level);
    }

    [HarmonyPrefix]
    private static bool fun_cameraEROZOOM_Prefix(playercon __instance)
    {
        try
        {
            CameraCache.InitializePlayerconReflection();

            bool isHSceneActive = PlayerEroContextUtility.IsHellGateManagedGrabHScene(__instance);

            if (CameraCache.FitzoomField == null || CameraCache.KeyJumpField == null)
            {
                _wasHSceneActive = isHSceneActive;
                return true;
            }

            ProCamera2DZoomToFitTargets fitzoom = CameraCache.FitzoomField.GetValue(__instance) as ProCamera2DZoomToFitTargets;

            if (_wasHSceneActive && !isHSceneActive && fitzoom != null)
            {
                ApplyZoomLevel(fitzoom, ZoomLow);
            }

            _wasHSceneActive = isHSceneActive;

            if (!isHSceneActive || fitzoom == null)
            {
                return true;
            }

            bool keyJump = (bool)CameraCache.KeyJumpField.GetValue(__instance);

            if (__instance.eroflag && keyJump)
            {
                float zoomLow = ZoomLow;
                float zoomMid = ZoomMid;
                float zoomHigh = ZoomHigh;

                fitzoom.ZoomInSmoothness = 1f;
                fitzoom.ZoomOutSmoothness = 1f;

                // Cycle: 1.5x → 3x → 5x → 1.5x
                if (IsZoomLevel(fitzoom.MaxZoomInAmount, zoomLow))
                {
                    ApplyZoomLevel(fitzoom, zoomMid);
                }
                else if (IsZoomLevel(fitzoom.MaxZoomInAmount, zoomMid))
                {
                    ApplyZoomLevel(fitzoom, zoomHigh);
                }
                else if (IsZoomLevel(fitzoom.MaxZoomInAmount, zoomHigh))
                {
                    ApplyZoomLevel(fitzoom, zoomLow);
                }
                else
                {
                    ApplyZoomLevel(fitzoom, zoomLow);
                }
            }

            return false;
        }
        catch
        {
            // Fallback to original method on error
        }

        return true;
    }
}
