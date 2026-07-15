using HarmonyLib;
using UnityEngine;
using NoREroMod.Systems.GrabSystem;

namespace NoREroMod.Systems.GrabSystem.Patches;

/// <summary>
/// Sets GrabViaAttackContext.CurrentAttacker before fun_damage / fun_damage_Improvement
/// is called from melee damage sources. The attacker is resolved by walking up the
/// hierarchy to find the EnemyDate component.
/// </summary>
internal static class MeleeAttackerContextPatches
{
    private static void SetAttacker(EnemyDate enemy)
    {
        if (enemy != null)
            GrabViaAttackContext.CurrentAttacker = enemy;
    }

    private static void ClearAttacker()
    {
        GrabViaAttackContext.CurrentAttacker = null;
    }

    // --- playerDamage: main path (enemy hitbox enters playerDAMAGEcol) ---

    [HarmonyPatch(typeof(playerDamage), "OnTriggerEnter2D")]
    [HarmonyPrefix]
    private static void PlayerDamage_Prefix(playerDamage __instance, Collider2D attack)
    {
        if (attack != null && attack.gameObject != null && attack.gameObject.CompareTag("playerDAMAGEcol"))
        {
            var enemy = __instance.GetComponentInParent<EnemyDate>();
            if (enemy == null && __instance.transform.parent != null)
                enemy = __instance.transform.parent.GetComponentInParent<EnemyDate>();
            SetAttacker(enemy);
        }
    }

    [HarmonyPatch(typeof(playerDamage), "OnTriggerEnter2D")]
    [HarmonyPostfix]
    private static void PlayerDamage_Postfix()
    {
        ClearAttacker();
    }

    // --- EnemyDate.OndamageSend: called via ExecuteEvents from playerDamage ---

    [HarmonyPatch(typeof(EnemyDate), "OndamageSend")]
    [HarmonyPrefix]
    private static void EnemyDate_OndamageSend_Prefix(EnemyDate __instance, string tag)
    {
        if (tag == "playerDAMAGEcol")
            SetAttacker(__instance);
    }

    [HarmonyPatch(typeof(EnemyDate), "OndamageSend")]
    [HarmonyPostfix]
    private static void EnemyDate_OndamageSend_Postfix()
    {
        ClearAttacker();
    }

    // --- SlashDamage: melee weapon, calls fun_damage directly ---

    [HarmonyPatch(typeof(SlashDamage), "OnTriggerEnter2D")]
    [HarmonyPrefix]
    private static void SlashDamage_Prefix(SlashDamage __instance, Collider2D col)
    {
        if (col != null && col.gameObject != null && col.gameObject.CompareTag("playerDAMAGEcol"))
        {
            var enemy = __instance.GetComponentInParent<EnemyDate>();
            SetAttacker(enemy);
        }
    }

    [HarmonyPatch(typeof(SlashDamage), "OnTriggerEnter2D")]
    [HarmonyPostfix]
    private static void SlashDamage_Postfix()
    {
        ClearAttacker();
    }

    // --- ImpactDamage: melee impact, calls fun_damage_Improvement ---

    [HarmonyPatch(typeof(ImpactDamage), "OnTriggerEnter2D")]
    [HarmonyPrefix]
    private static void ImpactDamage_Prefix(ImpactDamage __instance, Collider2D col)
    {
        if (col != null && col.gameObject != null && col.gameObject.CompareTag("playerDAMAGEcol"))
        {
            var enemy = __instance.GetComponentInParent<EnemyDate>();
            SetAttacker(enemy);
        }
    }

    [HarmonyPatch(typeof(ImpactDamage), "OnTriggerEnter2D")]
    [HarmonyPostfix]
    private static void ImpactDamage_Postfix()
    {
        ClearAttacker();
    }

    // --- ImpactDamageBOX: melee area box ---

    [HarmonyPatch(typeof(ImpactDamageBOX), "OnTriggerEnter2D")]
    [HarmonyPrefix]
    private static void ImpactDamageBOX_Prefix(ImpactDamageBOX __instance, Collider2D col)
    {
        if (col != null && col.gameObject != null && col.gameObject.CompareTag("playerDAMAGEcol"))
        {
            var enemy = __instance.GetComponentInParent<EnemyDate>();
            SetAttacker(enemy);
        }
    }

    [HarmonyPatch(typeof(ImpactDamageBOX), "OnTriggerEnter2D")]
    [HarmonyPostfix]
    private static void ImpactDamageBOX_Postfix()
    {
        ClearAttacker();
    }

    // --- impactDamageConst: persistent melee hitbox ---

    [HarmonyPatch(typeof(impactDamageConst), "OnTriggerEnter2D")]
    [HarmonyPrefix]
    private static void ImpactDamageConst_Prefix(impactDamageConst __instance, Collider2D col)
    {
        if (col != null && col.gameObject != null && col.gameObject.CompareTag("playerDAMAGEcol"))
        {
            var enemy = __instance.GetComponentInParent<EnemyDate>();
            SetAttacker(enemy);
        }
    }

    [HarmonyPatch(typeof(impactDamageConst), "OnTriggerEnter2D")]
    [HarmonyPostfix]
    private static void ImpactDamageConst_Postfix()
    {
        ClearAttacker();
    }
}
