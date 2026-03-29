using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using NoREroMod.Patches.UI.MindBroken;

namespace NoREroMod.Patches.UI.MindBroken;

/// <summary>
/// Universal patch for tracking ALL enemy kills for MindBroken recovery
/// Works for all enemies inheriting EnemyDate, without needing to patch each class separately
/// Uses the same approach as RageUniversalKillTrackerPatch
/// </summary>
[HarmonyPatch]
internal static class MindBrokenUniversalKillRecoveryPatch
{
    // Track already registered enemies to avoid duplicate registration
    private static HashSet<int> _registeredEnemies = new HashSet<int>();
    
    // Mapping of enemy types to their names (for compatibility with existing system)
    private static Dictionary<Type, string> _enemyTypeToName = new Dictionary<Type, string>
    {
        { typeof(TouzokuNormal), "touzokunormal" },
        { typeof(TouzokuAxe), "touzokuaxe" },
        { typeof(Bigoni), "bigonibrother" },
        { typeof(suraimu), "dorei" },
        { typeof(Mutude), "mutude" },
        { typeof(SinnerslaveCrossbow), "dorei" },
        { typeof(Kakash), "kakasi" },
        { typeof(goblin), "goblin" },
        { typeof(Inquisition), "inquisitionblack" },
        { typeof(InquisitionWhite), "inquisitionwhite" },
        { typeof(InquisitionRED), "inquisitionred" },
        { typeof(CrowInquisition), "crowinguisition" },
        { typeof(HighInquisition_famale), "highinquisition" },
    };
    
    /// <summary>
    /// Gets enemy name by its type
    /// </summary>
    private static string GetEnemyName(EnemyDate enemy)
    {
        if (enemy == null) return "unknown";
        
        Type enemyType = enemy.GetType();
        
        // Check mapping
        if (_enemyTypeToName.TryGetValue(enemyType, out string mappedName))
        {
            return mappedName;
        }
        
        // If not in mapping, convert type name to lowercase
        // Example: "TouzokuNormal" -> "touzokunormal"
        string typeName = enemyType.Name;
        return typeName.ToLowerInvariant();
    }
    
    /// <summary>
    /// Checks if enemy was already registered
    /// </summary>
    private static bool IsEnemyRegistered(EnemyDate enemy)
    {
        if (enemy == null) return true;
        int instanceId = enemy.GetInstanceID();
        return _registeredEnemies.Contains(instanceId);
    }
    
    /// <summary>
    /// Checks if enemy is in death state via reflection
    /// </summary>
    private static bool IsEnemyInDeathState(EnemyDate enemy)
    {
        if (enemy == null) return false;
        
        try
        {
            Type enemyType = enemy.GetType();
            FieldInfo stateField = enemyType.GetField("state", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (stateField == null) return false;
            
            object stateValue = stateField.GetValue(enemy);
            if (stateValue == null) return false;
            
            // Check if enum contains "DEATH" value
            string stateString = stateValue.ToString();
            return stateString.Contains("DEATH");
        }
        catch
        {
            // If reflection fails, rely on Hp
            return enemy.Hp <= 0f;
        }
    }
    
    /// <summary>
    /// Registers enemy as killed for MindBroken recovery
    /// </summary>
    private static void RegisterEnemyKill(EnemyDate enemy)
    {
        if (enemy == null) return;
        if (!MindBrokenRecoverySystem.IsEnabled) return;
        if (IsEnemyRegistered(enemy)) return;
        
        // Check that enemy is actually dead (Hp <= 0)
        if (enemy.Hp > 0f) return;
        
        // Additional check via state (if possible)
        // But don't block if reflection doesn't work - rely on Hp
        bool isInDeathState = IsEnemyInDeathState(enemy);
        
        int instanceId = enemy.GetInstanceID();
        _registeredEnemies.Add(instanceId);
        
        string enemyName = GetEnemyName(enemy);
        
        // Register kill for MindBroken recovery
        MindBrokenRecoverySystem.RegisterKill(enemyName);
    }
    
    /// <summary>
    /// Clears registered enemies list (called on level reset)
    /// </summary>
    internal static void ClearRegisteredEnemies()
    {
        _registeredEnemies.Clear();
    }
    
    // ========== DAMAGE METHOD PATCHES ==========
    
    /// <summary>
    /// Patch on EnemyDate.WeaponDamage() - weapon damage
    /// </summary>
    [HarmonyPatch(typeof(EnemyDate), "WeaponDamage")]
    [HarmonyPostfix]
    private static void WeaponDamage_Postfix(EnemyDate __instance)
    {
        try
        {
            if (__instance == null) return;
            if (__instance.Hp <= 0f)
            {
                RegisterEnemyKill(__instance);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[MindBroken Universal KillRecovery] WeaponDamage error: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Patch on EnemyDate.DPSWeaponDamage() - DPS weapon damage
    /// </summary>
    [HarmonyPatch(typeof(EnemyDate), "DPSWeaponDamage")]
    [HarmonyPostfix]
    private static void DPSWeaponDamage_Postfix(EnemyDate __instance)
    {
        try
        {
            if (__instance == null) return;
            if (__instance.Hp <= 0f)
            {
                RegisterEnemyKill(__instance);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[MindBroken Universal KillRecovery] DPSWeaponDamage error: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Patch on EnemyDate.MagicDamage() - magic damage
    /// </summary>
    [HarmonyPatch(typeof(EnemyDate), "MagicDamage")]
    [HarmonyPostfix]
    private static void MagicDamage_Postfix(EnemyDate __instance, float[] attribute)
    {
        try
        {
            if (__instance == null) return;
            if (__instance.Hp <= 0f)
            {
                RegisterEnemyKill(__instance);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[MindBroken Universal KillRecovery] MagicDamage error: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Patch on EnemyDate.DPSMagicDamage() - DPS magic damage
    /// </summary>
    [HarmonyPatch(typeof(EnemyDate), "DPSMagicDamage")]
    [HarmonyPostfix]
    private static void DPSMagicDamage_Postfix(EnemyDate __instance, float[] attribute)
    {
        try
        {
            if (__instance == null) return;
            if (__instance.Hp <= 0f)
            {
                RegisterEnemyKill(__instance);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[MindBroken Universal KillRecovery] DPSMagicDamage error: {ex.Message}");
        }
    }
}
