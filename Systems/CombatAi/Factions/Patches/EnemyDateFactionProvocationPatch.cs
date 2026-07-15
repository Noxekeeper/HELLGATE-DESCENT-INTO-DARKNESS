using System;
using HarmonyLib;
using NoREroMod.Systems.EventCore.Host;
using NoREroMod.Systems.Pregnancy.Patches;
using UnityEngine;

namespace NoREroMod.Systems.CombatAi.Factions.Patches;

[HarmonyPatch(typeof(ChildColliderTrigger), "OnTriggerEnter2D")]
internal static class EnemyDateFactionPlayerProvocationPatch
{
    [HarmonyPostfix]
    private static void Postfix(ChildColliderTrigger __instance, Collider2D collider)
    {
        if (__instance == null || collider == null || collider.gameObject == null)
            return;
        if (!EnemyFactionsConfig.Enable || !EnemyFactionsConfig.EnablePlayerProvocation)
            return;

        string tag = collider.gameObject.tag;
        if (tag != "ATKweapon" && tag != "ATKstab")
        {
            if (!(EnemyFactionsConfig.PlayerProvocationFromMagic && tag == "ATKmagic"))
                return;
        }

        Transform enemyRoot = __instance.transform.parent;
        if (enemyRoot == null)
            return;

        EnemyDate enemy = enemyRoot.GetComponent<EnemyDate>();
        if (enemy == null)
            return;
        if (WitchOffspringCombatRules.IsOffspring(enemy))
            return;
        playercon player = enemy.com_player;
        if (player == null)
            return;

        if (tag == "ATKmagic")
        {
            // Projectiles often land after cooltime clears magicnow; vanilla marks
            // trap/enemy shots with mgname "enemy", player spells use Magicdata names.
            if (!IsPlayerOwnedMagic(collider.GetComponent<magic>()))
                return;
        }
        else if (!player.Attacknow)
        {
            return;
        }

        if (EnemyFactionsConfig.PlayerProvocationBanditsOnly &&
            !EnemyFactionRuntime.IsBanditFamily(enemy.gameObject))
            return;

        EnemyFactionRuntime.MarkProvokedByPlayer(enemy);

        EventCoreHost eventCoreHost = enemy.GetComponent<EventCoreHost>();
        if (eventCoreHost != null)
            eventCoreHost.HandlePlayerProvoked();
    }

    private static bool IsPlayerOwnedMagic(magic mg)
    {
        if (mg == null || string.IsNullOrEmpty(mg.mgname))
            return false;
        return !string.Equals(mg.mgname, "enemy", StringComparison.OrdinalIgnoreCase);
    }
}
