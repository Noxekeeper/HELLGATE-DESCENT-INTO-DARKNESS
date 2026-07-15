using System.Collections;
using NoREroMod.Systems.Rage;
using UnityEngine;

namespace NoREroMod.Systems.Gameplay;

/// <summary>Optional real-time slow-mo window after <see cref="TryBeginStabPresentation"/> (config: SlowMoDuringStab).</summary>
internal static class VengeanceStrikeRuntime
{
    private static bool _presentationLoopRunning;

    internal static void TryBeginStabPresentation(playercon player)
    {
        if (player == null || !player._stabnow) return;

        bool wantSlowMo = Plugin.enableVengeanceStrikeSlowMo?.Value ?? true;
        if (!wantSlowMo) return;

        // Player T-key slow-mo owns timeScale; do not fight it.
        if (TimeSlowMoSystem.IsActive) return;

        if (_presentationLoopRunning) return;

        var host = Plugin.Instance;
        if (host == null) return;

        host.StartCoroutine(StabSlowMoLoop());
    }

    private static IEnumerator StabSlowMoLoop()
    {
        _presentationLoopRunning = true;
        float targetSlow = Mathf.Clamp(Plugin.vengeanceStrikeSlowMoTimeScale?.Value ?? 0.1f, 0.01f, 1f);
        float duration = Mathf.Clamp(Plugin.vengeanceStrikeSlowMoDurationSeconds?.Value ?? 2f, 0.05f, 60f);
        float endRealtime = Time.realtimeSinceStartup + duration;

        try
        {
            while (Time.realtimeSinceStartup < endRealtime)
            {
                if (TimeSlowMoSystem.IsActive)
                    yield break;

                Time.timeScale = targetSlow;
                yield return null;
            }
        }
        finally
        {
            if (!TimeSlowMoSystem.IsActive)
                Time.timeScale = 1f;
            _presentationLoopRunning = false;
        }
    }
}
