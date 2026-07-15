using System;
using HarmonyLib;
using NoREroMod.Systems.Gameplay.WeaponAnimations.Profiles;
using UnityEngine;

namespace NoREroMod.Systems.Gameplay.WeaponAnimations;

/// <summary>
/// Post-equip hook: extends <see cref="PlayerStatus._AtkMotion"/> / <see cref="PlayerStatus._SmashKind"/> for eligible WeaponKind 1 swords via <see cref="WitchGreatswordComboSequences"/>.
/// <see cref="WitchFineGreatswordPatch"/> covers <c>wp_bigwitch*</c> separately.
/// </summary>
[HarmonyPatch]
internal static class LightOneHand3HitExtendedComboEquipPatch
{
    private static void ApplyOnEquip(PlayerStatus pl, string hook)
    {
        try
        {
            if (pl == null) return;
            if (!LightSwordExtendedComboProfile.IsVanillaThreeHitLightSword(pl)) return;
            WitchGreatswordComboSequences.ApplyConfiguredSequence(pl);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[LightOneHand3HitExtendedCombo] {hook}: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(PlayerStatus), "WPequip")]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void WPequip_Postfix(PlayerStatus __instance, int _newItemID)
    {
        ApplyOnEquip(__instance, nameof(WPequip_Postfix));
    }

    [HarmonyPatch(typeof(PlayerStatus), "GetStaticInventory")]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void GetStaticInventory_Postfix(PlayerStatus __instance)
    {
        ApplyOnEquip(__instance, nameof(GetStaticInventory_Postfix));
    }

    [HarmonyPatch(typeof(Item_Equipment), "fun_equip_set")]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void fun_equip_set_Postfix(Item_Equipment __instance)
    {
        var pl = __instance != null ? Traverse.Create(__instance).Field("pl").GetValue<PlayerStatus>() : null;
        ApplyOnEquip(pl, nameof(fun_equip_set_Postfix));
    }

    [HarmonyPatch(typeof(WpUpgreadenable), "EquipWp")]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void WpUpgreadenable_EquipWp_Postfix(WpUpgreadenable __instance)
    {
        var pl = __instance != null ? Traverse.Create(__instance).Field("pl").GetValue<PlayerStatus>() : null;
        ApplyOnEquip(pl, nameof(WpUpgreadenable_EquipWp_Postfix));
    }

    [HarmonyPatch(typeof(ReWpUpgradeenable), "EquipWp")]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void ReWpUpgradeenable_EquipWp_Postfix(ReWpUpgradeenable __instance)
    {
        var pl = __instance != null ? Traverse.Create(__instance).Field("pl").GetValue<PlayerStatus>() : null;
        ApplyOnEquip(pl, nameof(ReWpUpgradeenable_EquipWp_Postfix));
    }

    [HarmonyPatch(typeof(PlayerStatus), "Awake")]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Awake_Postfix(PlayerStatus __instance)
    {
        ApplyOnEquip(__instance, nameof(Awake_Postfix));
    }
}
