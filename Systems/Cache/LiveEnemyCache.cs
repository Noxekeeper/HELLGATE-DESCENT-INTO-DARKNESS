using UnityEngine;

namespace NoREroMod.Systems.Cache;

/// <summary>
/// Shared <see cref="EnemyDate"/> snapshot for periodic scanners (Fear aura, gimmick guards).
/// One <c>FindObjectsOfType</c> per refresh window instead of one per scanner; callers must
/// null-check entries because enemies can be destroyed between refreshes.
/// </summary>
internal static class LiveEnemyCache
{
    private const float RefreshInterval = 0.25f;

    private static EnemyDate[] enemies = new EnemyDate[0];
    private static float nextRefresh;
    private static int lastSceneHandle;

    internal static EnemyDate[] GetEnemies()
    {
        int sceneHandle = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetHashCode();
        if (Time.unscaledTime >= nextRefresh || sceneHandle != lastSceneHandle)
        {
            nextRefresh = Time.unscaledTime + RefreshInterval;
            lastSceneHandle = sceneHandle;
            enemies = Object.FindObjectsOfType<EnemyDate>() ?? new EnemyDate[0];
        }

        return enemies;
    }
}
