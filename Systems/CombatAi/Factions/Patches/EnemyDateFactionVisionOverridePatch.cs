using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Systems.CombatAi.Factions.Patches;

/// <summary>
/// Separate player-vision override module:
/// relation score linearly controls how far each faction "sees" the player.
/// -100 => far vision, +100 => short vision.
/// </summary>
[HarmonyPatch(typeof(EnemyDate), "Distance_fun")]
internal static class EnemyDateFactionVisionOverridePatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(EnemyDate __instance)
    {
        if (__instance == null || __instance.gameObject == null)
            return;
        if (!EnemyFactionsConfig.Enable || !EnemyFactionsConfig.EnableRelationVisionOverride)
            return;
        if (__instance.Hp <= 0f)
            return;
        if (EnemyFactionRuntime.IsBossEnemy(__instance.gameObject))
            return;
        if (__instance.com_player == null)
            return;
        if (__instance.com_player.erodown != 0)
            return;
        if (EnemyFactionsConfig.FreezeFactionAiDuringHScene && __instance.com_player.eroflag)
            return;
        if (EnemyFactionRuntime.ShouldRespectEventCorePassiveShell(__instance))
        {
            EnemyFactionRuntime.EnterPassiveWaitState(__instance);
            return;
        }
        if (EnemyFactionRuntime.IsHostileToPlayer(__instance.gameObject))
            return;

        if (EnemyFactionRuntime.IsFactionCombatCommitted(__instance.gameObject))
            return;

        // Do not override active faction-vs-faction engagements; this module controls
        // only player visibility, not inter-faction combat targeting.
        EnemyDate hostileTarget;
        if (EnemyFactionRuntime.TryGetNearestHostile(__instance, out hostileTarget))
            return;

        Transform player = __instance.com_player.transform;
        if (player == null)
            return;

        float dx = player.position.x - __instance.transform.position.x;
        float dy = player.position.y - __instance.transform.position.y;
        float relationVision = EnemyFactionRuntime.GetRelationVisionDistance(__instance.gameObject);

        bool outsideVision;
        if (EnemyFactionsConfig.ActivationDistanceHorizontalOnly)
            outsideVision = Mathf.Abs(dx) > relationVision;
        else
            outsideVision = (dx * dx + dy * dy) > (relationVision * relationVision);

        if (!outsideVision)
        {
            if (!FactionReputationBehavior.ShouldSuppressVanillaAggro(__instance.gameObject) &&
                !FactionReputationBehavior.ShouldBanditsIgnorePlayer(__instance.gameObject))
            {
                EnemyFactionRuntime.ExitPassiveWaitState(__instance);
            }

            return;
        }

        EnemyFactionRuntime.EnterPassiveWaitState(__instance);
    }
}
