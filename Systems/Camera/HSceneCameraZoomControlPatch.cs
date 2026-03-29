using HarmonyLib;
using Com.LuisPedroFonseca.ProCamera2D;
using NoREroMod;
using NoREroMod.Systems.Camera;
using UnityEngine;
using System.Reflection;

namespace NoREroMod.Systems.Camera;

/// <summary>
/// Harmony patch for spacebar zoom control during H-scenes.
/// Cycles through zoom levels: 1.5x → 3x → 8x → 1.5x.
/// Uses original game logic with custom zoom levels.
/// Resets zoom to default value when H-scene ends.
/// </summary>
[HarmonyPatch(typeof(playercon), "fun_cameraEROZOOM")]
internal class HSceneCameraZoomControlPatch
{
    private static bool _wasHSceneActive = false;
    
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
                float resetValue = Plugin.cameraZoomResetValue.Value;
                fitzoom.MaxZoomInAmount = resetValue;
                fitzoom.MaxZoomOutAmount = resetValue;
            }
        }
        catch { }
    }
    
    [HarmonyPrefix]
    private static bool fun_cameraEROZOOM_Prefix(playercon __instance)
    {
        try
        {
            CameraCache.InitializePlayerconReflection();
            
            bool isHSceneActive = __instance.eroflag && __instance.erodown != 0;
            
            if (CameraCache.FitzoomField == null || CameraCache.KeyJumpField == null)
            {
                _wasHSceneActive = isHSceneActive;
                return true;
            }
            
            ProCamera2DZoomToFitTargets fitzoom = CameraCache.FitzoomField.GetValue(__instance) as ProCamera2DZoomToFitTargets;
            
            // Reset zoom to default when H-scene ends
            if (_wasHSceneActive && !isHSceneActive && fitzoom != null)
            {
                float resetValue = Plugin.cameraZoomResetValue.Value;
                fitzoom.MaxZoomInAmount = resetValue;
                fitzoom.MaxZoomOutAmount = resetValue;
            }
            
            _wasHSceneActive = isHSceneActive;
            
            if (!isHSceneActive || fitzoom == null)
            {
                return true;
            }
            
            bool keyJump = (bool)CameraCache.KeyJumpField.GetValue(__instance);
            
            // Original game logic: change zoom when key is pressed (no edge detection)
            if (__instance.eroflag && keyJump)
            {
                float zoom1_5x = 1.5f;
                float zoom3x = 3f;
                float zoom8x = 8f;
                
                // Set faster zoom speed (1f for both in/out)
                fitzoom.ZoomInSmoothness = 1f;
                fitzoom.ZoomOutSmoothness = 1f;
                
                // Cycle: 1.5x → 3x → 8x → 1.5x
                if (fitzoom.MaxZoomInAmount == zoom1_5x)
                {
                    fitzoom.MaxZoomInAmount = zoom3x;
                }
                else if (fitzoom.MaxZoomInAmount == zoom3x)
                {
                    fitzoom.MaxZoomInAmount = zoom8x;
                }
                else if (fitzoom.MaxZoomInAmount == zoom8x)
                {
                    fitzoom.MaxZoomInAmount = zoom1_5x;
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

