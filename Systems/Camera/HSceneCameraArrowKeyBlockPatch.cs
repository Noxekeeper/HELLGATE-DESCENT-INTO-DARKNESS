using HarmonyLib;
using UnityEngine;
using Com.LuisPedroFonseca.ProCamera2D;
using NoREroMod;
using NoREroMod.Systems.Camera;

namespace NoREroMod.Systems.Camera;

/// <summary>
/// Harmony patch to block built-in arrow key handling during H-scenes.
/// Blocks Cameramove.Update (arrow key panning) and GAmng.fun_cameraEROZOOM (arrow key zoom)
/// to prevent conflicts with our pan patch and spacebar zoom (playercon.fun_cameraEROZOOM).
/// </summary>
[HarmonyPatch(typeof(Cameramove), "Update")]
internal class HSceneCameraArrowKeyBlockPatch1
{
    [HarmonyPrefix]
    private static bool Update_Prefix(Cameramove __instance)
    {
        if (CameraCache.IsHSceneActive())
        {
            // Block built-in arrow key handling during H-scenes
            return false;
        }
        
        return true;
    }
}

[HarmonyPatch(typeof(GAmng), "fun_cameraEROZOOM")]
internal class HSceneCameraArrowKeyBlockPatch2
{
    [HarmonyPrefix]
    private static bool fun_cameraEROZOOM_Prefix(GAmng __instance)
    {
        if (CameraCache.IsHSceneActive())
        {
            // Block built-in arrow key zoom (GAmng uses key_camera_vertical)
            // Our spacebar zoom works through playercon.fun_cameraEROZOOM patch
            return false;
        }
        
        return true;
    }
}

