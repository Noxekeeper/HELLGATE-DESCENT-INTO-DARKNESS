using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Systems.CombatAi.Factions.Patches;

/// <summary>
/// Faction passivity uses an explicit runtime flag plus optional fake <see cref="EnemyDate.distance"/>
/// for vanilla AI. NoREroMod Field-of-View uses the same fields for skeleton alpha — body fades out
/// while the faction bone marker stays visible. Restore real player offset only inside UpdateFOV.
/// </summary>
[HarmonyPatch]
internal static class EnemyDateFactionFovCompatPatch
{
    private struct DistanceSnapshot
    {
        internal bool WasPassive;
        internal float Distance;
        internal float DistanceY;
    }

    private static MethodBase TargetMethod()
    {
        var patchType = HellGateTypeResolver.Resolve("NoREroMod.EnemyDatePatch");
        return patchType == null ? null : AccessTools.Method(patchType, "UpdateFOV");
    }

    private static bool Prepare() => TargetMethod() != null;

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void Prefix(EnemyDate __instance, ref DistanceSnapshot __state)
    {
        __state = default;
        if (__instance == null)
            return;

        __state.Distance = __instance.distance;
        __state.DistanceY = __instance.distance_y;
        __state.WasPassive = EnemyFactionRuntime.IsInPassiveWaitState(__instance);
        if (!__state.WasPassive)
            return;

        RestoreRealPlayerDistance(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(EnemyDate __instance, DistanceSnapshot __state)
    {
        if (__instance == null || !__state.WasPassive)
            return;

        __instance.distance = __state.Distance;
        __instance.distance_y = __state.DistanceY;
    }

    private static void RestoreRealPlayerDistance(EnemyDate enemy)
    {
        if (enemy.com_player == null)
            return;

        Vector3 playerPos = enemy.com_player.transform.position;
        Vector3 selfPos = enemy.transform.position;
        enemy.distance = playerPos.x - selfPos.x;
        enemy.distance_y = playerPos.y - selfPos.y;
    }
}
