using UnityEngine;
using System;

namespace NoREroMod.Systems.Cache;

/// <summary>
/// Centralized playercon cache for all HellGate systems.
///
/// Problem:
/// 15+ dialogue patches called GameObject.FindGameObjectWithTag("Player")
/// on every Spine animation event (45-90 times per second during H-scenes).
///
/// Solution:
/// Single cache refreshed automatically every 0.5 seconds.
///
/// Usage:
/// // Instead of:
/// var player = GameObject.FindWithTag("Player")?.GetComponent&lt;playercon&gt;();
///
/// // Use:
/// var player = UnifiedPlayerCacheManager.GetPlayer();
///
/// Performance:
/// - Before: 45-90 FindGameObjectWithTag calls per second
/// - After: ~2 cache refreshes per second
/// - Gain: ~95%
/// </summary>
internal static class UnifiedPlayerCacheManager
{
    private static GameObject cachedPlayerObject = null;
    private static playercon cachedPlayerCon = null;
    private static PlayerStatus cachedPlayerStatus = null;
    
    private static float lastCacheUpdateTime = 0f;
    private const float CACHE_UPDATE_INTERVAL = 0.5f;
    
    private static bool cacheInitialized = false;

    /// <summary>
    /// Cached player GameObject.
    /// </summary>
    public static GameObject GetPlayerObject()
    {
        UpdateCacheIfNeeded();
        return cachedPlayerObject;
    }

    /// <summary>
    /// Cached playercon. Prefer this over FindGameObjectWithTag.
    /// </summary>
    public static playercon GetPlayer()
    {
        UpdateCacheIfNeeded();
        return cachedPlayerCon;
    }

    /// <summary>
    /// Cached PlayerStatus component.
    /// </summary>
    public static PlayerStatus GetPlayerStatus()
    {
        UpdateCacheIfNeeded();
        return cachedPlayerStatus;
    }

    /// <summary>
    /// Fast H-scene active check (no scene search).
    /// </summary>
    public static bool IsHSceneActive()
    {
        var player = GetPlayer();
        return player != null && player.eroflag && player.erodown != 0;
    }

    /// <summary>
    /// Basic H-scene active check.
    /// </summary>
    public static bool IsHSceneActiveBasic()
    {
        var player = GetPlayer();
        return player != null && player.eroflag && player.erodown != 0;
    }

    /// <summary>
    /// Refresh the cache when the interval elapsed or entries are missing.
    /// </summary>
    private static void UpdateCacheIfNeeded()
    {
        float currentTime = Time.time;
        
        // Refresh when:
        // 1. Not initialized
        // 2. Interval elapsed
        // 3. Cached object was destroyed
        if (!cacheInitialized || 
            cachedPlayerObject == null || 
            cachedPlayerCon == null ||
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
            cachedPlayerObject = GameObject.FindGameObjectWithTag("Player");
            
            if (cachedPlayerObject != null)
            {
                cachedPlayerCon = cachedPlayerObject.GetComponent<playercon>();
                cachedPlayerStatus = cachedPlayerObject.GetComponent<PlayerStatus>();
                if (cachedPlayerStatus == null)
                {
                    GameObject gameController = GameObject.FindGameObjectWithTag("GameController");
                    cachedPlayerStatus = gameController != null
                        ? gameController.GetComponent<PlayerStatus>()
                        : null;
                }
                cacheInitialized = true;
            }
            else
            {
                cachedPlayerCon = null;
                cachedPlayerStatus = null;
                cacheInitialized = false;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[PLAYER CACHE] Error refreshing cache: {ex.Message}");
            cachedPlayerObject = null;
            cachedPlayerCon = null;
            cachedPlayerStatus = null;
            cacheInitialized = false;
        }
    }

    /// <summary>
    /// Clear cache (call on scene changes or critical state resets).
    /// </summary>
    public static void ResetCache()
    {
        cachedPlayerObject = null;
        cachedPlayerCon = null;
        cachedPlayerStatus = null;
        lastCacheUpdateTime = 0f;
        cacheInitialized = false;
        
        Plugin.Log?.LogDebug("[PLAYER CACHE] Cache reset");
    }

    /// <summary>
    /// Force refresh for external systems.
    /// </summary>
    public static void ForceRefresh()
    {
        RefreshCache();
        Plugin.Log?.LogInfo("[PLAYER CACHE] Cache force refreshed");
    }
}
