using UnityEngine;

namespace NoREroMod.Systems.LostSounds;

/// <summary>Bootstrap for the isolated LostSounds scaffold module.</summary>
internal static class LostSoundsBootstrap
{
    private static bool _started;

    internal static void Initialize(MonoBehaviour host)
    {
        if (_started)
            return;

        _started = true;

        if (!LostSoundsConfig.IsEnabled)
        {
            LostSoundsConfig.LogDebug("[LostSounds] Disabled by config");
            return;
        }

        try
        {
            LostSoundsAudio.Initialize(host);
            LostSoundsConfig.LogDebug("[LostSounds] Module initialized");
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogWarning("[LostSounds] Initialize failed: " + ex.Message);
        }
    }
}
