using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Patches.Player;

/// <summary>
/// Restores <see cref="Time.timeScale"/> to 1f when leaving grab / H-scene.
/// Enemies set timeScale to 0.2f at H-scene start and schedule Invoke on their GameObject.
/// On a fast escape the ERO object is deactivated and Invoke never runs, leaving timeScale at 0.2.
/// This patch detects eroflag true to false and forces timeScale back to normal.
/// </summary>
internal static class TimeScaleResetOnEscapePatch
{
    private static bool _wasInGrabLastFrame;

    /// <summary>Called from <see cref="PlayerConUpdateDispatcher"/>.</summary>
    internal static void Process(bool eroflag)
    {
        try
        {
            bool isInGrab = eroflag;
            if (_wasInGrabLastFrame && !isInGrab)
            {
                if (Time.timeScale != 1f && Time.timeScale != 0f)
                {
                    Time.timeScale = 1f;
                }

                NoREroMod.Systems.EventCore.Host.EventCoreHost.NotifyPlayerHSceneEnded();
            }
            _wasInGrabLastFrame = isInGrab;
        }
        catch
        {
            _wasInGrabLastFrame = false;
        }
    }
}
