using NoREroMod.Systems.Cache;
using UnityEngine;

namespace NoREroMod.Systems.Spawn;

/// <summary>Freezes gameplay while F11 authoring UI is open (keeps IMGUI / Input responsive).</summary>
internal static class SpawnAuthoringPause
{
    private static bool armed;
    private static float storedTimeScale = 1f;
    private static bool storedSousa = true;

    internal static bool IsFrozen => armed;

    internal static void Begin()
    {
        if (armed)
            return;

        try
        {
            NoREroMod.Systems.Rage.TimeSlowMoSystem.Reset();
        }
        catch
        {
        }

        if (SpawnAuthoringConfig.PauseGameplay != null && !SpawnAuthoringConfig.PauseGameplay.Value)
            return;

        var ps = UnifiedPlayerCacheManager.GetPlayerStatus();
        storedTimeScale = Time.timeScale;
        storedSousa = ps == null || ps._SOUSA;
        armed = true;

        Time.timeScale = 0f;
        if (ps != null)
        {
            ps._SOUSA = false;
            ps._SOUSAMNG = false;
        }
    }

    internal static void End()
    {
        if (!armed)
            return;

        Time.timeScale = storedTimeScale > 0.0001f ? storedTimeScale : 1f;

        var ps = UnifiedPlayerCacheManager.GetPlayerStatus();
        if (ps != null)
        {
            ps._SOUSA = true;
            ps._SOUSAMNG = true;
        }

        armed = false;
    }
}
