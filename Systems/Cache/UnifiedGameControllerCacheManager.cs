using UnityEngine;
using System;

namespace NoREroMod.Systems.Cache;

/// <summary>
/// Централизованный менеджер кэширования GameController for всех систем
/// 
/// ПРОБЛЕМА:
/// 15+ мест вызывают GameObject.FindGameObjectWithTag("GameController") 
/// on каждом Update, спавне, change сцены etc.
/// 
/// SOLUTION:
/// Единый кэш with automaticallyм обновлением каждую second
/// 
/// ИСПОЛЬЗОВАНИЕ:
/// // Вместо:
/// var gc = GameObject.FindWithTag("GameController");
/// var fragMng = gc?.GetComponent<game_fragmng>();
/// var ps = gc?.GetComponent<PlayerStatus>();
/// 
/// // Использовать:
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
    /// Get кэшированный GameObject GameController
    /// </summary>
    public static GameObject GetGameController()
    {
        UpdateCacheIfNeeded();
        return cachedGameController;
    }

    /// <summary>
    /// Get кэшированный game_fragmng
    /// ОСНОВНОЙ МЕТОД - используйте instead of FindGameObjectWithTag!
    /// </summary>
    public static game_fragmng GetGameFragMng()
    {
        UpdateCacheIfNeeded();
        return cachedFragMng;
    }

    /// <summary>
    /// Get кэшированный PlayerStatus (with GameController)
    /// </summary>
    public static PlayerStatus GetPlayerStatus()
    {
        UpdateCacheIfNeeded();
        return cachedPlayerStatus;
    }

    /// <summary>
    /// Обновление кэша if прошло достаточbut времени
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
    /// Принудительное обновление кэша
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
    /// Reset cache (вызывать on change сцены or on Destroy GameController)
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
    /// Принудительное обновление кэша (for внешнtheir систем)
    /// </summary>
    public static void ForceRefresh()
    {
        RefreshCache();
        Plugin.Log?.LogInfo("[GAMECONTROLLER CACHE] Cache force refreshed");
    }
}
