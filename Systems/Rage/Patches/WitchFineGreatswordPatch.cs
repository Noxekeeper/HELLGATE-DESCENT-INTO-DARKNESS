using System;
using GameDataEditor;
using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Systems.Rage.Patches;

/// <summary>
/// Witch fine greatsword uses light sword stats/animations. Heavy anims have fixed duration; WeaponKind=1 required.
/// </summary>
[HarmonyPatch]
internal static class WitchFineGreatswordPatch
{
    private const string LightSwordKey = "wp_witch";
    
    /// <summary>
    /// Returns true when the equipped weapon name belongs to a bigwitch variant.
    /// </summary>
    private static bool IsBigwitchWeapon(string weaponName)
    {
        if (string.IsNullOrEmpty(weaponName)) return false;
        return weaponName.IndexOf("bigwitch", StringComparison.OrdinalIgnoreCase) >= 0;
    }
    
    /// <summary>
    /// Copies light sword gameplay stats and animation sets to current player status.
    /// </summary>
    private static void ApplyLightSwordStats(PlayerStatus status, GDEweaponData lightSword)
    {
        if (status == null || lightSword == null) return;
        
        try
        {
            status.WeaponKind = lightSword.weaponkind;
            status.WeaponATK = lightSword.weaponatk;
            status.atkspeed = lightSword.atkspeed;
            status.ToughCut = lightSword.toughcut;
            status.GuardCut = lightSword.guardcut;
            status.Revenge = lightSword.revenge;
            status.weaponcount = lightSword.maxatkcount;
            status.atksp = lightSword.atksp;
            
            if (lightSword.Correction != null && lightSword.Correction.Count >= 4)
            {
                for (int i = 0; i < 4; i++)
                    status.correction[i] = lightSword.Correction[i];
            }
            
            status.guardusesp = lightSword.guardusesp;
            status.justguardrecovery = lightSword.justguradrecovery;
            status.fatalatkrecovery = lightSword.fatalatkrecovery;
            status._criticalhit = lightSword.CriticalHit;
            status._criticaldamage = lightSword.CreticalHitDamage;
            
            status._WeaponelementalATK = lightSword.wpelement.ToArray();
            status._WeaponelementalCorrection = lightSword.ElementCorrection.ToArray();
            status._Exuipstatus = lightSword.equipstatus.ToArray();
            status._AtkMotion = lightSword.atkmotion;
            status._SmashKind = lightSword.smashkind;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[WitchFineGreatsword] ApplyLightSwordStats error: {ex.Message}");
        }
    }
    
    [HarmonyPatch(typeof(PlayerStatus), "WPequip")]
    [HarmonyPostfix]
    private static void WPequip_Postfix(PlayerStatus __instance, int _newItemID)
    {
        try
        {
            if (__instance == null) return;
            if (!IsBigwitchWeapon(__instance.Exuip)) return;
            
            var lightSword = new GDEweaponData(LightSwordKey);
            ApplyLightSwordStats(__instance, lightSword);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[WitchFineGreatsword] WPequip_Postfix error: {ex.Message}");
        }
    }
    
    [HarmonyPatch(typeof(PlayerStatus), "GetStaticInventory")]
    [HarmonyPostfix]
    private static void GetStaticInventory_Postfix(PlayerStatus __instance)
    {
        try
        {
            if (__instance == null) return;
            if (!IsBigwitchWeapon(__instance.Exuip)) return;
            
            var lightSword = new GDEweaponData(LightSwordKey);
            ApplyLightSwordStats(__instance, lightSword);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[WitchFineGreatsword] GetStaticInventory_Postfix error: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Applies stat override when equipping via Item_Equipment menu flow.
    /// </summary>
    [HarmonyPatch(typeof(Item_Equipment), "fun_equip_set")]
    [HarmonyPostfix]
    private static void fun_equip_set_Postfix(Item_Equipment __instance)
    {
        try
        {
            if (__instance == null) return;
            var pl = Traverse.Create(__instance).Field("pl").GetValue<PlayerStatus>();
            if (pl == null) return;
            if (!IsBigwitchWeapon(pl.Exuip)) return;
            
            var lightSword = new GDEweaponData(LightSwordKey);
            ApplyLightSwordStats(pl, lightSword);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[WitchFineGreatsword] fun_equip_set_Postfix error: {ex.Message}");
        }
    }
    
    [HarmonyPatch(typeof(WpUpgreadenable), "EquipWp")]
    [HarmonyPostfix]
    private static void WpUpgreadenable_EquipWp_Postfix(WpUpgreadenable __instance)
    {
        try
        {
            if (__instance == null) return;
            var pl = Traverse.Create(__instance).Field("pl").GetValue<PlayerStatus>();
            if (pl == null || !IsBigwitchWeapon(pl.Exuip)) return;
            var lightSword = new GDEweaponData(LightSwordKey);
            ApplyLightSwordStats(pl, lightSword);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[WitchFineGreatsword] WpUpgreadenable_EquipWp_Postfix error: {ex.Message}");
        }
    }
    
    [HarmonyPatch(typeof(ReWpUpgradeenable), "EquipWp")]
    [HarmonyPostfix]
    private static void ReWpUpgradeenable_EquipWp_Postfix(ReWpUpgradeenable __instance)
    {
        try
        {
            if (__instance == null) return;
            var pl = Traverse.Create(__instance).Field("pl").GetValue<PlayerStatus>();
            if (pl == null || !IsBigwitchWeapon(pl.Exuip)) return;
            var lightSword = new GDEweaponData(LightSwordKey);
            ApplyLightSwordStats(pl, lightSword);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[WitchFineGreatsword] ReWpUpgradeenable_EquipWp_Postfix error: {ex.Message}");
        }
    }
    
    [HarmonyPatch(typeof(PlayerStatus), "Awake")]
    [HarmonyPostfix]
    private static void Awake_Postfix(PlayerStatus __instance)
    {
        try
        {
            if (__instance == null) return;
            if (!IsBigwitchWeapon(__instance.Exuip)) return;
            
            var lightSword = new GDEweaponData(LightSwordKey);
            ApplyLightSwordStats(__instance, lightSword);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[WitchFineGreatsword] Awake_Postfix error: {ex.Message}");
        }
    }
}
