using System.IO;
using UnityEngine;

namespace NoREroMod.Patches.Enemy.RickEnemyModShared;

/// <summary>
/// Shared disk root for RickEnemyMod assets (per-enemy fatality folders + common Fatality Logo overlay).
/// </summary>
internal static class RickEnemyModPaths
{
    internal static string GetBasePath()
    {
        try
        {
            string gameRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(gameRoot))
                return null;

            string customPath = Plugin.rickEnemyModAssetsPath?.Value?.Trim();
            if (string.IsNullOrEmpty(customPath))
                customPath = Plugin.butcherModAssetsPath?.Value?.Trim();

            if (!string.IsNullOrEmpty(customPath))
            {
                return Path.IsPathRooted(customPath)
                    ? customPath
                    : Path.Combine(gameRoot, customPath);
            }

            return Path.Combine(Path.Combine(gameRoot, "sources"), Path.Combine("HellGate_sources", "RickEnemyMod"));
        }
        catch
        {
            return null;
        }
    }

    internal static string GetFatalityLogoFolder()
    {
        string basePath = GetBasePath();
        return string.IsNullOrEmpty(basePath) ? null : Path.Combine(basePath, "Fatality Logo");
    }
}
