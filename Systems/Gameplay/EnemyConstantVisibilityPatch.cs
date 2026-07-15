using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Systems.Gameplay;

/// <summary>
/// When HellGate <c>EnableFieldOfView</c> is off, keep enemy skeletons at full alpha.
/// NoREroMod FoV lerps alpha down but never restores it once FoV is disabled or after a fade.
/// </summary>
[HarmonyPatch]
internal static class EnemyConstantVisibilityPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var patchType = HellGateTypeResolver.Resolve("NoREroMod.EnemyDatePatch");
        if (patchType == null)
            yield break;

        MethodInfo enemyFov = AccessTools.Method(patchType, "EnemyFOV");
        if (enemyFov != null)
            yield return enemyFov;

        MethodInfo bossFov = AccessTools.Method(patchType, "BossEnemyFOV");
        if (bossFov != null)
            yield return bossFov;
    }

    private static bool Prepare() => HellGateTypeResolver.Resolve("NoREroMod.EnemyDatePatch") != null;

    [HarmonyPostfix]
    private static void EnsureVisibleWhenFoVOff(EnemyDate __instance, SkeletonAnimation ___mySpine)
    {
        if (Plugin.enableFoV?.Value ?? false)
            return;

        RestoreFullAlpha(___mySpine);
    }

    internal static void RestoreFullAlpha(SkeletonAnimation spine)
    {
        if (spine?.skeleton == null)
            return;

        Color color = spine.skeleton.GetColor();
        if (color.a >= 0.99f)
            return;

        spine.skeleton.SetColor(new Color(color.r, color.g, color.b, 1f));
    }
}
