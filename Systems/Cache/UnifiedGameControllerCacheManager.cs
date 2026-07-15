using UnityEngine;
using System;

namespace NoREroMod.Systems.Cache;

/// <summary>
/// Centralized GameController cache for all HellGate systems.
///
/// Problem:
/// 15+ call sites used GameObject.FindGameObjectWithTag("GameController")
/// on Update, spawn, scene change, etc.
///
/// Solution:
/// Single cache refreshed automatically every second.
///
/// Usage:
/// // Instead of:
/// var gc = GameObject.FindWithTag("GameController");
/// var fragMng = gc?.GetComponent&lt;game_fragmng&gt;();
/// var ps = gc?.GetComponent&lt;PlayerStatus&gt;();
///
/// // Use:
/// var fragMng = UnifiedGameControllerCacheManager.GetGameFragMng();
/// var ps = UnifiedGameControllerCacheManager.GetPlayerStatus();
/// var gc = UnifiedGameControllerCacheManager.GetGameController();
/// </summary>
internal static class UnifiedGameControllerCacheManager
{
    private static GameObject cachedGameController = null;
    private static game_fragmng cachedFragMng = null;
    private static PlayerStatus cachedPlayerStatus = null;
    
    private static float lastCacheUpdateTime = 0f;
    private const float CACHE_UPDATE_INTERVAL = 1.0f;
    
    private static bool cacheInitialized = false;

    /// <summary>
    /// Cached GameController GameObject.
    /// </summary>
    public static GameObject GetGameController()
    {
        UpdateCacheIfNeeded();
        return cachedGameController;
    }

    /// <summary>
    /// Cached game_fragmng. Prefer this over FindGameObjectWithTag.
    /// </summary>
    public static game_fragmng GetGameFragMng()
    {
        UpdateCacheIfNeeded();
        return cachedFragMng;
    }

    /// <summary>
    /// Cached PlayerStatus from GameController.
    /// </summary>
    public static PlayerStatus GetPlayerStatus()
    {
        UpdateCacheIfNeeded();
        return cachedPlayerStatus;
    }

    /// <summary>
    /// Refresh the cache when the interval elapsed or entries are missing.
    /// </summary>
    private static void UpdateCacheIfNeeded()
    {
        float currentTime = Time.time;
        
        if (!cacheInitialized || 
            cachedGameController == null || 
            (currentTime - lastCacheUpdateTime) > CACHE_UPDATE_INTERVAL)
        {
            RefreshCache();
            lastCacheUpdateTime = currentTime;
        }
    }

    /// <summary>
    /// Force a full cache refresh.
    /// </summary>
    private static void RefreshCache()
    {
        try
        {
            cachedGameController = GameObject.FindGameObjectWithTag("GameController");
            
            if (cachedGameController != null)
            {
                cachedFragMng = cachedGameController.GetComponent<game_fragmng>();
                cachedPlayerStatus = cachedGameController.GetComponent<PlayerStatus>();
                cacheInitialized = true;
            }
            else
            {
                cachedFragMng = null;
                cachedPlayerStatus = null;
                cacheInitialized = false;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[GAMECONTROLLER CACHE] Error refreshing cache: {ex.Message}");
            cachedGameController = null;
            cachedFragMng = null;
            cachedPlayerStatus = null;
            cacheInitialized = false;
        }
    }

    /// <summary>
    /// Clear cache (call on scene change or when GameController is destroyed).
    /// </summary>
    public static void ResetCache()
    {
        cachedGameController = null;
        cachedFragMng = null;
        cachedPlayerStatus = null;
        lastCacheUpdateTime = 0f;
        cacheInitialized = false;
        
        Plugin.Log?.LogDebug("[GAMECONTROLLER CACHE] Cache reset");
    }

    /// <summary>
    /// Force refresh for external systems.
    /// </summary>
    public static void ForceRefresh()
    {
        RefreshCache();
        Plugin.Log?.LogInfo("[GAMECONTROLLER CACHE] Cache force refreshed");
    }
}
