using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace NoREroMod.Systems.CombatAi.Factions.Patches;

/// <summary>
/// Faction retargeting runs in Distance_fun postfixes, but vanilla enemy Update resets
/// Choose/Look when |distance| &gt; 12 right after Distance_fun returns. Re-apply approach
/// fields at the end of Update so Mutude, SlaveBigAxe, Inquisition, etc. keep closing in.
/// </summary>
[HarmonyPatch]
internal static class EnemyDateFactionUpdateSustainPatch
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (Type type in AccessTools.GetTypesFromAssembly(typeof(EnemyDate).Assembly))
        {
            if (type == null || type.IsAbstract || type.IsInterface)
                continue;
            if (!typeof(EnemyDate).IsAssignableFrom(type) || type == typeof(EnemyDate))
                continue;

            MethodInfo update = AccessTools.DeclaredMethod(type, "Update");
            if (update != null && !update.IsStatic)
                yield return update;
        }
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(EnemyDate __instance)
    {
        if (__instance == null || __instance.gameObject == null)
            return;

        try
        {
            EnemyFactionRuntime.SustainFactionCombatApproach(__instance);
        }
        catch (Exception ex)
        {
            if (EnemyFactionsConfig.DebugLogging)
                Plugin.Log?.LogWarning("[EnemyFactions] Update sustain failed on " +
                    __instance.GetType().Name + ": " + ex.Message);
        }
    }
}
