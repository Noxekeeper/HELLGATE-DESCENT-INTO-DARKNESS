using UnityEngine;

namespace NoREroMod.Patches.UI.MindBroken;

/// <summary>
/// Base MindBroken growth during H-scenes for all enemies.
/// Rate comes from <c>[MindBroken] HScenePercentPerSecond</c> (default +0.1%/sec).
/// Stacks with custom effects (e.g., Mutude DRINK +1%/sec).
/// Additionally: enhances passive pleasure growth proportional to MindBroken:
///   pleasure += (0.01 * MindBrokenPercent * 100) per second (i.e., 100% MB = +1 unit/sec).
/// </summary>
internal static class H_scenesAllEnemiesCorruption
{
    private const float PleasurePerSecondPerMbPercent = 0.01f; // Each 1% MB -> +0.01 pleasure/sec

    /// <summary>Invoked from PlayerConUpdateDispatcher</summary>
    internal static void Process(playercon __instance, PlayerStatus ___playerstatus)
    {
        if (__instance == null || ___playerstatus == null) return;

        if (__instance.erodown != 0 && __instance.eroflag && ___playerstatus.Hp > 0f)
        {
            float perSecondPercent = Mathf.Max(0f, Plugin.mindBrokenHScenePercentPerSecond?.Value ?? 0.1f);
            if (perSecondPercent > 0f)
            {
                // Config is display-% per second (0.1 = +0.1%/sec), same convention as HighRagePassivePercentPerSecond.
                MindBrokenSystem.AddPercent((perSecondPercent / 100f) * Time.deltaTime, "global-hscene");
            }

            float mbPercent = MindBrokenSystem.Percent; // 0..1
            if (mbPercent > 0f)
            {
                float pleasureGainPerSec = PleasurePerSecondPerMbPercent * (mbPercent * 100f);
                ___playerstatus.BadstatusValPlus(pleasureGainPerSec * Time.deltaTime);
            }
        }
    }
}
