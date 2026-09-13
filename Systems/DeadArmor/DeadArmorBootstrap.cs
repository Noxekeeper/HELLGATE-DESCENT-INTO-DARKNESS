using UnityEngine;

namespace NoREroMod.Systems.DeadArmor;

/// <summary>Bootstrap for the isolated DeadArmor module.</summary>
internal static class DeadArmorBootstrap
{
    private static bool _started;

    internal static void Initialize(MonoBehaviour host)
    {
        if (_started)
            return;

        _started = true;

        if (!DeadArmorConfig.IsModuleEnabled)
        {
            DeadArmorConfig.LogDebug("[DeadArmor] Disabled by config");
            return;
        }

        try
        {
            DeadArmorSpriteCache.Preload();
            DeadArmorAudio.Initialize(host);
            DeadArmorConfig.LogDebug("[DeadArmor] Module initialized");
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogWarning("[DeadArmor] Initialize failed: " + ex.Message);
        }
    }
}
