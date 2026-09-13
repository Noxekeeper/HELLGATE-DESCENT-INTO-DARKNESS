using UnityEngine;

namespace NoREroMod.Systems.EnemyFatality;

/// <summary>Preloads clip frames and fatality audio for all registered module-enabled profiles.</summary>
internal static class EnemyFatalityBootstrap
{
    private static bool _started;

    internal static void Initialize(MonoBehaviour host)
    {
        if (_started)
            return;

        _started = true;
        EnemyFatalitySession.EnsureSceneHook();

        if (!EnemyFatalityRegistry.AnyModuleEnabled())
        {
            EnemyFatalityConfig.LogDebug("[EnemyFatality] Bootstrap skipped — no profiles enabled");
            return;
        }

        try
        {
            var profiles = EnemyFatalityRegistry.All;
            for (int i = 0; i < profiles.Count; i++)
            {
                IEnemyFatalityProfile profile = profiles[i];
                if (profile == null || !profile.IsModuleEnabled)
                    continue;

                EnemyFatalitySpriteCache.Preload(profile);
            }

            EnemyFatalityAudio.Initialize(host);
            EnemyFatalityTaunts.Initialize(host);
            EnemyFatalityConfig.LogDebug("[EnemyFatality] Module initialized (" + profiles.Count + " profiles)");
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogWarning("[EnemyFatality] Initialize failed: " + ex.Message);
        }
    }
}
