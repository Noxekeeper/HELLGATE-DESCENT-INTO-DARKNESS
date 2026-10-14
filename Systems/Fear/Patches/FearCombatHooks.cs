using System;
using System.Collections.Generic;
using HarmonyLib;
using NoREroMod.Systems.CombatAi.Factions;
using NoREroMod.Systems.GrabSystem;
using UnityEngine;

namespace NoREroMod.Systems.Fear.Patches;

/// <summary>
/// Thin Harmony hooks → FearSystem. Active only when FearConfig.Enable.
/// </summary>
internal static class FearCombatHooks
{
    private static float _guradBeforeDamage = -1f;
    private static float _guradBeforeImprovement = -1f;
    private static int _kickbackBeforeDamage;
    private static int _kickbackBeforeImprovement;
    private static bool _wasAvoidDamage;
    private static bool _wasAvoidImprovement;
    private static readonly HashSet<int> _kills = new HashSet<int>();

    [HarmonyPatch(typeof(playercon), "Acttext")]
    [HarmonyPostfix]
    private static void OnActtext(string text)
    {
        if (!FearConfig.IsEnabled) return;
        if (text == "PARRY!!")
            FearSystem.NotifyPerfectParry(GrabViaAttackContext.CurrentAttacker);
        else if (text == "Avoided")
            FearSystem.NotifyAvoid();
    }

    [HarmonyPatch(typeof(playercon), "fun_damage")]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void FunDamage_Prefix(playercon __instance, float ___guradcount, int kickbackkind)
    {
        if (!FearConfig.IsEnabled) return;
        _wasAvoidDamage = __instance != null && __instance.stepfrag;
        _guradBeforeDamage = ___guradcount;
        _kickbackBeforeDamage = kickbackkind;
    }

    [HarmonyPatch(typeof(playercon), "fun_damage")]
    [HarmonyPostfix]
    private static void FunDamage_Postfix(playercon __instance, float ___guradcount, int kickbackkind)
    {
        if (!FearConfig.IsEnabled) return;
        if (_wasAvoidDamage)
        {
            _wasAvoidDamage = false;
            _guradBeforeDamage = -1f;
            return;
        }
        HandleIncomingDamage(__instance, _guradBeforeDamage, ___guradcount, _kickbackBeforeDamage, kickbackkind);
        _guradBeforeDamage = -1f;
    }

    [HarmonyPatch(typeof(playercon), "fun_damage_Improvement")]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void FunDamageImprovement_Prefix(playercon __instance, float ___guradcount, int kickbackkind)
    {
        if (!FearConfig.IsEnabled) return;
        _wasAvoidImprovement = __instance != null && __instance.stepfrag;
        _guradBeforeImprovement = ___guradcount;
        _kickbackBeforeImprovement = kickbackkind;
    }

    [HarmonyPatch(typeof(playercon), "fun_damage_Improvement")]
    [HarmonyPostfix]
    private static void FunDamageImprovement_Postfix(playercon __instance, float ___guradcount, int kickbackkind)
    {
        if (!FearConfig.IsEnabled) return;
        if (_wasAvoidImprovement)
        {
            _wasAvoidImprovement = false;
            _guradBeforeImprovement = -1f;
            return;
        }
        HandleIncomingDamage(__instance, _guradBeforeImprovement, ___guradcount, _kickbackBeforeImprovement, kickbackkind);
        _guradBeforeImprovement = -1f;
    }

    [HarmonyPatch(typeof(EnemyDate), "OndamageSend")]
    [HarmonyPostfix]
    private static void OndamageSend_Postfix(EnemyDate __instance, string tag)
    {
        if (!FearConfig.IsEnabled) return;
        if (tag == "ATKweapon")
            FearSystem.NotifyPlayerHitOnEnemy();
    }

    [HarmonyPatch(typeof(EnemyDate), "OndamageSendMagic")]
    [HarmonyPostfix]
    private static void OndamageSendMagic_Postfix(EnemyDate __instance, string tag)
    {
        if (!FearConfig.IsEnabled) return;
        if (tag == "ATKmagic")
            FearSystem.NotifyPlayerHitOnEnemy();
    }

    [HarmonyPatch(typeof(EnemyDate), "WeaponDamage")]
    [HarmonyPostfix]
    private static void WeaponDamage_Postfix(EnemyDate __instance)
    {
        TryRegisterKill(__instance);
    }

    [HarmonyPatch(typeof(EnemyDate), "DPSWeaponDamage")]
    [HarmonyPostfix]
    private static void DPSWeaponDamage_Postfix(EnemyDate __instance)
    {
        TryRegisterKill(__instance);
    }

    [HarmonyPatch(typeof(EnemyDate), "MagicDamage")]
    [HarmonyPostfix]
    private static void MagicDamage_Postfix(EnemyDate __instance)
    {
        TryRegisterKill(__instance);
    }

    [HarmonyPatch(typeof(EnemyDate), "DPSMagicDamage")]
    [HarmonyPostfix]
    private static void DPSMagicDamage_Postfix(EnemyDate __instance)
    {
        TryRegisterKill(__instance);
    }

    private static void TryRegisterKill(EnemyDate enemy)
    {
        if (!FearConfig.IsEnabled || enemy == null) return;
        if (enemy.Hp > 0f) return;
        int id = enemy.GetInstanceID();
        if (!_kills.Add(id)) return;
        bool boss = FactionBossDetection.IsBossEnemy(enemy);
        FearSystem.NotifyEnemyKill(enemy, boss);
    }

    private static void HandleIncomingDamage(
        playercon player,
        float guradBefore,
        float guradAfter,
        int kickbackBefore,
        int kickbackAfter)
    {
        if (player == null) return;

        // Perfect block window opened.
        if (guradBefore == 0f && guradAfter == 0.2f)
        {
            bool heavy = DamageSourceClassifier.IsPowerAttack(kickbackBefore);
            FearSystem.NotifySuccessfulBlock(heavy);
            return;
        }

        // Took a hit (not a fresh perfect block).
        if (DamageSourceClassifier.IsRanged)
        {
            FearSystem.NotifyProjectileHit(GrabViaAttackContext.CurrentAttacker);
            return;
        }

        if (DamageSourceClassifier.IsPowerAttack(kickbackBefore) || DamageSourceClassifier.IsPowerAttack(kickbackAfter))
            FearSystem.NotifyHeavyHitOrKnockdown(GrabViaAttackContext.CurrentAttacker);
    }
}
