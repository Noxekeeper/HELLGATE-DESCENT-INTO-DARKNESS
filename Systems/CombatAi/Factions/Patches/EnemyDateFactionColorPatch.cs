using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Systems.CombatAi.Factions.Patches;

[HarmonyPatch(typeof(EnemyDate), "start_fun")]
internal static class EnemyDateFactionColorBootstrapPatch
{
    private static readonly System.Collections.Generic.HashSet<int> _loggedApply = new System.Collections.Generic.HashSet<int>();

    [HarmonyPostfix]
    private static void Postfix(EnemyDate __instance)
    {
        ApplyFactionMarker(__instance);
    }

    internal static void ApplyFactionMarker(EnemyDate enemy)
    {
        if (enemy == null || enemy.gameObject == null)
            return;
        if (!EnemyFactionsConfig.Enable)
            return;
        if (FactionMarkerVisibility.ShouldSuppress(enemy))
        {
            FactionBoneMarkerAttachment.Remove(enemy);
            return;
        }
        if (FactionIds.IsPassiveNonCombat(EnemyFactionRuntime.GetFaction(enemy.gameObject)))
            return;
        // Keep faction visuals marker-only (no skeleton tinting).
        if (!EnemyFactionRuntime.TryGetFactionTintColor(enemy.gameObject, out Color color))
            color = Color.white;

        int applyId = enemy.gameObject.GetInstanceID();
        if (EnemyFactionsConfig.DebugLogging && _loggedApply.Add(applyId))
            Plugin.Log?.LogInfo("[EnemyFactions] Marker applied to " + enemy.GetType().Name + " faction=" + EnemyFactionRuntime.GetFaction(enemy.gameObject) + " rgb=(" + color.r.ToString("0.00") + "," + color.g.ToString("0.00") + "," + color.b.ToString("0.00") + ")");

        FactionBoneMarkerAttachment.Ensure(enemy, color);
    }
}

internal static class EnemyDateFactionColorAnimePatch
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo method = typeof(EnemyDate).GetMethod(
            "fun_animekind",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method != null)
            yield return method;
    }

    [HarmonyPostfix]
    private static void Postfix(EnemyDate __instance)
    {
        EnemyDateFactionColorBootstrapPatch.ApplyFactionMarker(__instance);
    }
}

internal static class EnemyDateFactionColorResetPatch
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo method = typeof(EnemyDate).GetMethod(
            "reste",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method != null)
            yield return method;
    }

    [HarmonyPostfix]
    private static void Postfix(EnemyDate __instance)
    {
        EnemyDateFactionColorBootstrapPatch.ApplyFactionMarker(__instance);
    }
}
