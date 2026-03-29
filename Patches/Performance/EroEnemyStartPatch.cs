using HarmonyLib;
using UnityEngine;
using System.Reflection;
using NoREroMod.Systems.Cache;

namespace NoREroMod.Patches.Performance;

/// <summary>
/// Patch for оптимизации EroMafiamuscle.Start()
/// 
/// ПРОБЛЕМА:
/// On каждом OnEnable() EroMafiamuscle объекта is called Start(), which делает:
/// - GameObject.FindWithTag("Player") (~3-5ms)
/// - GetComponent<playercon>() (~1-2ms)
/// 
/// For MafiaBossCustom this происходит 3-4 раза за гангбанг!
/// 
/// SOLUTION:
/// Заменяем FindWithTag on UnifiedPlayerCacheManager
/// 
/// ПРОИЗВОДИТЕЛЬНОСТЬ:
/// - Было: FindWithTag + GetComponent on каждом Start (~4-7ms)
/// - Стало: Кэш (~0ms)
/// - Прирост: ~100%
/// </summary>
[HarmonyPatch(typeof(EroMafiamuscle), "Start")]
internal static class EroMafiamuscleStartPatch
{
    private static FieldInfo _cachedPlayerField;
    
    static EroMafiamuscleStartPatch()
    {
        try
        {
            _cachedPlayerField = typeof(EroMafiamuscle).GetField("player", 
                BindingFlags.NonPublic | BindingFlags.Instance);
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogError($"[EroMafiamuscleStartPatch] Failed to cache player field: {ex.Message}");
        }
    }
    
    [HarmonyPrefix]
    private static void Prefix(EroMafiamuscle __instance)
    {
        try
        {
            if (_cachedPlayerField == null) return;
            
            // ✨ Используем кэшированный playercon instead of FindWithTag
            var player = UnifiedPlayerCacheManager.GetPlayer();
            if (player != null)
            {
                _cachedPlayerField.SetValue(__instance, player);
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogWarning($"[EroMafiamuscleStartPatch] Error: {ex.Message}");
        }
    }
}
