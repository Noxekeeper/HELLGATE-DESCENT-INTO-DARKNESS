using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using NoREroMod.Patches.UI.MindBroken;
using NoREroMod.Systems.Rage;
using AccessTools = HarmonyLib.AccessTools;

namespace NoREroMod.Systems.Rage.Patches;

/// <summary>
/// Universal kill tracker for enemies via EnemyDate damage methods.
/// Works for all EnemyDate-derived enemies without per-class patches.
/// </summary>
[HarmonyPatch]
internal static class RageUniversalKillTrackerPatch
{
    // Tracks already registered enemies to prevent duplicate kill registration.
    private static HashSet<int> _registeredEnemies = new HashSet<int>();
    
    // Mapping from enemy type to canonical enemy key.
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
    /// Resolves canonical enemy key by runtime type.
    /// </summary>
    private static string GetEnemyName(EnemyDate enemy)
    {
        if (enemy == null) return "unknown";
        
        Type enemyType = enemy.GetType();
        
        // Prefer mapped key when available.
        if (_enemyTypeToName.TryGetValue(enemyType, out string mappedName))
        {
            return mappedName;
        }
        
        // Fallback: normalize runtime type name to lowercase.
        string typeName = enemyType.Name;
        return typeName.ToLowerInvariant();
    }
    
    /// <summary>
    /// Returns true if this enemy instance has already been registered.
    /// </summary>
    private static bool IsEnemyRegistered(EnemyDate enemy)
    {
        if (enemy == null) return true;
        int instanceId = enemy.GetInstanceID();
        return _registeredEnemies.Contains(instanceId);
    }
    
    /// <summary>
    /// Tries to detect death state via reflection.
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
            
            // Match enum string value containing "DEATH".
            string stateString = stateValue.ToString();
            return stateString.Contains("DEATH");
        }
        catch
        {
            // Reflection fallback: rely on HP.
            return enemy.Hp <= 0f;
        }
    }
    
    /// <summary>
    /// Registers an enemy kill once.
    /// </summary>
    private static void RegisterEnemyKill(EnemyDate enemy)
    {
        if (enemy == null) return;
        if (!RageSystem.Enabled) return;
        if (IsEnemyRegistered(enemy)) return;
        
        // Register only dead enemies.
        if (enemy.Hp > 0f) return;
        IsEnemyInDeathState(enemy); // Optional probe for diagnostics compatibility.
        
        int instanceId = enemy.GetInstanceID();
        _registeredEnemies.Add(instanceId);
        
        string enemyName = GetEnemyName(enemy);
        bool isBoss = MindBrokenRecoverySystem.IsBoss(enemyName);

        RageSystem.RegisterKill(enemyName, isBoss);
    }
    
    /// <summary>
    /// Clears registered enemy set (e.g., on stage reset).
    /// </summary>
    internal static void ClearRegisteredEnemies()
    {
        _registeredEnemies.Clear();
        // Logging intentionally disabled to reduce noise.
        // Plugin.Log?.LogInfo("[Rage Universal KillTracker] Cleared registered enemies list");
    }
    
    // ===== Damage Method Patches =====
    
    /// <summary>
    /// Patch for EnemyDate.WeaponDamage() (weapon damage).
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
            Plugin.Log?.LogError($"[Rage Universal KillTracker] WeaponDamage error: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Patch for EnemyDate.DPSWeaponDamage() (DPS weapon damage).
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
            Plugin.Log?.LogError($"[Rage Universal KillTracker] DPSWeaponDamage error: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Patch for EnemyDate.MagicDamage() (magic damage).
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
            Plugin.Log?.LogError($"[Rage Universal KillTracker] MagicDamage error: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Patch for EnemyDate.DPSMagicDamage() (DPS magic damage).
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
            Plugin.Log?.LogError($"[Rage Universal KillTracker] DPSMagicDamage error: {ex.Message}");
        }
    }
}
