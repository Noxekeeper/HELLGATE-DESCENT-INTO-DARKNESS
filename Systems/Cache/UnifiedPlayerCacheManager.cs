using UnityEngine;
using System;

namespace NoREroMod.Systems.Cache;

/// <summary>
/// Централизованный менеджер кэширования playercon for всех систем
/// 
/// ПРОБЛЕМА:
/// 15+ диаlogsых патчей вызывают GameObject.FindGameObjectWithTag("Player") 
/// on КАЖДОМ Spine animation event (45-90 раз in second during H-scene!)
/// 
/// SOLUTION:
/// Единый кэш with automaticallyм обновлением каждые 0.5 seconds
/// 
/// ИСПОЛЬЗОВАНИЕ:
/// // Вместо:
/// var player = GameObject.FindWithTag("Player")?.GetComponent<playercon>();
/// 
/// // Использовать:
/// var player = UnifiedPlayerCacheManager.GetPlayer();
/// 
/// ПРОИЗВОДИТЕЛЬНОСТЬ:
/// - Было: 45-90 операций FindGameObjectWithTag in second
/// - Стало: 2 операции in second (обновление кэша)
/// - Прирост: ~95%
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
    /// Get кэшированный GameObject игрока
    /// </summary>
    public static GameObject GetPlayerObject()
    {
        UpdateCacheIfNeeded();
        return cachedPlayerObject;
    }

    /// <summary>
    /// Get кэшированный component playercon
    /// ОСНОВНОЙ МЕТОД - используйте its instead of FindGameObjectWithTag!
    /// </summary>
    public static playercon GetPlayer()
    {
        UpdateCacheIfNeeded();
        return cachedPlayerCon;
    }

    /// <summary>
    /// Get кэшированный component PlayerStatus
    /// </summary>
    public static PlayerStatus GetPlayerStatus()
    {
        UpdateCacheIfNeeded();
        return cachedPlayerStatus;
    }

    /// <summary>
    /// Check activeсти H-scene (быстрая проверка without поиска)
    /// </summary>
    public static bool IsHSceneActive()
    {
        var player = GetPlayer();
        return player != null && player.eroflag && player.erodown != 0;
    }

    /// <summary>
    /// Check activeсти H-scene (базовая проверка)
    /// </summary>
    public static bool IsHSceneActiveBasic()
    {
        var player = GetPlayer();
        return player != null && player.eroflag && player.erodown != 0;
    }

    /// <summary>
    /// Обновление кэша if прошло достаточbut времени
    /// </summary>
    private static void UpdateCacheIfNeeded()
    {
        float currentTime = Time.time;
        
        // Update кэш если:
        // 1. Не инициализирован
        // 2. Прошло достаточbut времени
        // 3. Кэшированный объект был уничтожен
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
    /// Принудительное обновление кэша
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
    /// Reset cache (вызывать on change сцены or critical изменениях)
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
    /// Принудительное обновление кэша (for внешнtheir систем)
    /// </summary>
    public static void ForceRefresh()
    {
        RefreshCache();
        Plugin.Log?.LogInfo("[PLAYER CACHE] Cache force refreshed");
    }
}
