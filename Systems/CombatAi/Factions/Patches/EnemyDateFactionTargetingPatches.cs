using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace NoREroMod.Systems.CombatAi.Factions.Patches;

[HarmonyPatch(typeof(EnemyDate), "Distance_fun")]
internal static class EnemyDateFactionDistancePatch
{
    private static float _lastErrorLogAt = -999f;

    [HarmonyPostfix]
    private static void Postfix(EnemyDate __instance)
    {
        try
        {
            if (__instance == null || __instance.gameObject == null)
                return;
            if (!EnemyFactionsConfig.Enable)
                return;

            bool playerIsDowned = __instance.com_player != null && __instance.com_player.erodown != 0;
            if (playerIsDowned)
            {
                if (!EnemyFactionRuntime.IsBossEnemy(__instance.gameObject))
                {
                    EnemyFactionRuntime.RestoreVanillaPlayerApproach(__instance);
                    EnemyFactionRuntime.ApplyRelationMoveSpeed(__instance);
                }
                return;
            }

            if (!EnemyFactionRuntime.IsBossEnemy(__instance.gameObject))
                EnemyFactionRuntime.ApplyRelationMoveSpeed(__instance);
            if (EnemyFactionsConfig.FreezeFactionAiDuringHScene &&
                __instance.com_player != null && __instance.com_player.eroflag)
            {
                EnemyFactionRuntime.EnterPassiveWaitState(__instance);
                return;
            }

            bool hostileToPlayer = EnemyFactionRuntime.IsHostileToPlayer(__instance.gameObject);
            if (EnemyFactionRuntime.ShouldRespectEventCorePassiveShell(__instance))
            {
                EnemyFactionRuntime.ClearFactionCombatCommitted(__instance.gameObject);
                EnemyFactionRuntime.EnterPassiveWaitState(__instance);
                return;
            }

            bool suppressPlayerAggro =
                FactionReputationBehavior.ShouldSuppressVanillaAggro(__instance.gameObject);

            float activationDistance = EnemyFactionsConfig.ActivationDistanceFromPlayer;

            // Reputation Hostile: auto-provoke when the player is inside the activation radius.
            if (!hostileToPlayer && FactionReputationBehavior.ShouldAutoProvoke(__instance.gameObject))
            {
                if (EnemyFactionRuntime.IsInPassiveWaitState(__instance))
                    EnemyFactionRuntime.PreparePlayerCombatEngagement(__instance);

                if (EnemyFactionRuntime.IsPlayerWithinActivationZone(__instance, activationDistance) &&
                    WithinActivationVertical(__instance))
                {
                    EnemyFactionRuntime.MarkPermanentlyHostileToPlayer(__instance);
                    EnemyFactionRuntime.PreparePlayerCombatEngagement(__instance);
                    hostileToPlayer = true;
                }
            }

            bool allowFactionEngage = !hostileToPlayer ||
                EnemyFactionRuntime.ShouldEngageHostileFactionOverPlayer(__instance, activationDistance);

            EnemyDate nearbyHostile;
            bool hasNearbyHostile = EnemyFactionRuntime.TryGetNearestHostile(__instance, out nearbyHostile);

            // Inter-faction brawl before player-only combat and before passive suppression.
            if (!EnemyFactionRuntime.IsBossEnemy(__instance.gameObject) &&
                allowFactionEngage &&
                EnemyFactionRuntime.TryEngageNearestHostileFaction(__instance))
            {
                return;
            }

            if (hostileToPlayer)
            {
                if (!EnemyFactionRuntime.IsBossEnemy(__instance.gameObject))
                    EnemyFactionRuntime.PreparePlayerCombatEngagement(__instance);

                EnemyFactionRuntime.TryApplyPulseDamage(__instance);
                return;
            }

            if (activationDistance > 0f)
            {
                bool outsideActivation = !EnemyFactionRuntime.IsPlayerWithinActivationZone(__instance, activationDistance);

                if (!outsideActivation && !WithinActivationVertical(__instance))
                    outsideActivation = true;

                if (outsideActivation)
                {
                    if (!EnemyFactionRuntime.IsFactionCombatCommitted(__instance.gameObject))
                    {
                        if (FactionReputationBehavior.ShouldBanditsIgnorePlayer(__instance.gameObject))
                            EnemyFactionRuntime.EnterPassiveWaitState(__instance);
                        return;
                    }
                }
            }

            if (EnemyFactionRuntime.IsFactionCombatCommitted(__instance.gameObject) && !hasNearbyHostile)
                EnemyFactionRuntime.ClearFactionCombatCommitted(__instance.gameObject);

            if (!suppressPlayerAggro && !FactionReputationBehavior.ShouldBanditsIgnorePlayer(__instance.gameObject) &&
                EnemyFactionRuntime.IsInPassiveWaitState(__instance) &&
                !EnemyFactionRuntime.ShouldRespectEventCorePassiveShell(__instance))
            {
                EnemyFactionRuntime.RestoreVanillaPlayerApproach(__instance);
            }

            if ((suppressPlayerAggro || FactionReputationBehavior.ShouldBanditsIgnorePlayer(__instance.gameObject)) &&
                !hasNearbyHostile &&
                !EnemyFactionRuntime.IsFactionCombatCommitted(__instance.gameObject))
            {
                EnemyFactionRuntime.EnterPassiveWaitState(__instance);
            }
        }
        catch (System.Exception ex)
        {
            float now = Time.realtimeSinceStartup;
            if (now - _lastErrorLogAt > 1.5f)
            {
                _lastErrorLogAt = now;
                Plugin.Log?.LogError("[EnemyFactions] Distance_fun postfix exception on " +
                    (__instance != null ? __instance.GetType().Name : "null") + ": " + ex);
            }
        }
    }

    private static bool WithinActivationVertical(EnemyDate self)
    {
        if (self == null)
            return true;

        float cap = EnemyFactionsConfig.ActivationMaxVerticalDelta;
        if (cap <= 0f)
            return true;

        float dy;
        if (EnemyFactionRuntime.TryGetRealPlayerOffset(self, out _, out dy))
            return Mathf.Abs(dy) <= cap;

        dy = self.playerPos.y - self.transform.position.y;
        return Mathf.Abs(dy) <= cap;
    }
}

[HarmonyPatch(typeof(EnemyDate), "OndamageSend")]
internal static class EnemyDateFactionIgnorePlayerDamageColPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnemyDate __instance, string tag)
    {
        if (__instance == null || __instance.gameObject == null)
            return true;
        if (!EnemyFactionsConfig.Enable || tag != "playerDAMAGEcol")
            return true;
        if (EnemyFactionRuntime.IsBossEnemy(__instance.gameObject))
            return true;
        if (!FactionReputationBehavior.ShouldBanditsIgnorePlayer(__instance.gameObject))
            return true;

        return false;
    }
}

internal static class EnemyDateFactionCleanupPatch
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo method = typeof(EnemyDate).GetMethod(
            "OnDestroy",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method != null)
            yield return method;
    }

    [HarmonyPrefix]
    private static void Prefix(EnemyDate __instance)
    {
        if (__instance == null || __instance.gameObject == null)
            return;
        EnemyFactionRuntime.RemoveFaction(__instance.gameObject);
    }
}
