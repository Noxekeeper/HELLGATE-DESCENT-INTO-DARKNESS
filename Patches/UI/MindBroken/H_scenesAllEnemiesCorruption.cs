using System;
using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Patches.UI.MindBroken;

/// <summary>
/// Base MindBroken growth during H-scenes for all enemies.
/// Adds +0.1% per second, stacks with custom effects (e.g., Mutude DRINK +1%/sec).
/// Additionally: enhances passive pleasure growth proportional to MindBroken:
///   pleasure += (0.01 * MindBrokenPercent * 100) per second (i.e., 100% MB = +1 unit/sec).
/// </summary>
internal static class H_scenesAllEnemiesCorruption
{
    private const float BaseMbPerSecond = 0.001f; // 0.1%/sec
    private const float PleasurePerSecondPerMbPercent = 0.01f; // Each 1% MB -> +0.01 pleasure/sec

    /// <summary>Invoked from PlayerConUpdateDispatcher</summary>
    internal static void Process(playercon __instance, PlayerStatus ___playerstatus)
    {
        if (__instance == null || ___playerstatus == null) return;

        if (__instance.erodown != 0 && __instance.eroflag && ___playerstatus.Hp > 0f)
            {
            MindBrokenSystem.AddPercent(BaseMbPerSecond * Time.deltaTime, "global-hscene");

            float mbPercent = MindBrokenSystem.Percent; // 0..1
            if (mbPercent > 0f)
            {
                float pleasureGainPerSec = PleasurePerSecondPerMbPercent * (mbPercent * 100f);
                ___playerstatus.BadstatusValPlus(pleasureGainPerSec * Time.deltaTime);
            }
        }
    }
}

