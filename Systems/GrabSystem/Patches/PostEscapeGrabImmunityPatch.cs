using System.Linq;
using System.Reflection;
using HarmonyLib;
using NoREroMod.Systems.GrabSystem;
using UnityEngine;

namespace NoREroMod.Systems.GrabSystem.Patches;

/// <summary>
/// Same grab + knockdown gates as <c>RageActiveImmunityPatch</c>, keyed off
/// <see cref="PostEscapeGrabImmunity.IsActive"/> instead of Rage.
/// </summary>
internal static class PostEscapeGrabImmunityPatch
{
    private const int KnockdownKickbackLightFlinch = 2;
    private static bool _collisionGrabPatched;

    internal static void ApplyCollisionGrabBlock(Harmony harmony)
    {
        if (_collisionGrabPatched)
            return;
        try
        {
            var type = typeof(StruggleSystem).Assembly.GetType("NoREroMod.EnemyDatePatch");
            if (type == null)
            {
                Plugin.Log?.LogWarning("[GrabSystemNG] EnemyDatePatch not found; post-escape collision grab block skipped.");
                return;
            }

            MethodInfo prefix = typeof(PostEscapeGrabImmunityPatch).GetMethod(
                nameof(CanEliteGrabPlayer_Prefix),
                BindingFlags.Static | BindingFlags.NonPublic);
            if (prefix == null)
                return;

            int patched = 0;
            foreach (MethodInfo m in type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                         .Where(x => x.Name == "CanEliteGrabPlayer"))
            {
                try
                {
                    harmony.Patch(m, prefix: new HarmonyMethod(prefix) { priority = Priority.First });
                    patched++;
                }
                catch (System.Exception ex)
                {
                    Plugin.Log?.LogWarning("[GrabSystemNG] CanEliteGrabPlayer post-escape patch failed: " + ex.Message);
                }
            }

            if (patched > 0)
            {
                _collisionGrabPatched = true;
                Plugin.Log?.LogInfo(
                    "[GrabSystemNG] Patched " + patched
                    + " CanEliteGrabPlayer overload(s) for post-escape immunity.");
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogError("[GrabSystemNG] ApplyCollisionGrabBlock (post-escape) failed: " + ex.Message);
        }
    }

    private static bool CanEliteGrabPlayer_Prefix(ref bool __result)
    {
        if (!PostEscapeGrabImmunity.IsActive)
            return true;
        __result = false;
        return false;
    }

    [HarmonyPatch(typeof(playercon), "fun_nowdamage_move")]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void FunNowdamageMove_KnockdownPrefix(playercon __instance)
    {
        if (!PostEscapeGrabImmunity.IsActive) return;
        if (__instance == null || !__instance.nowdamage) return;
        int t = __instance.ToKickbackkind;
        if (!DamageSourceClassifier.IsPowerAttack(t)) return;
        __instance.ToKickbackkind = KnockdownKickbackLightFlinch;
    }

    [HarmonyPatch(typeof(playercon), nameof(playercon.fun_damage))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void FunDamage_KnockdownPrefix(playercon __instance, float getatk, float gettoughcut, ref int kickbackkind, int getdamedir, float damecount)
    {
        if (!PostEscapeGrabImmunity.IsActive) return;
        if (__instance == null) return;
        if (!DamageSourceClassifier.IsPowerAttack(kickbackkind)) return;
        kickbackkind = KnockdownKickbackLightFlinch;
    }

    [HarmonyPatch(typeof(playercon), nameof(playercon.fun_damage_Improvement))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void FunDamageImprovement_KnockdownPrefix(playercon __instance, float getatk, float gettoughcut, ref int kickbackkind, int getdamedir, float damecount, bool Noguard, float spcut)
    {
        if (!PostEscapeGrabImmunity.IsActive) return;
        if (__instance == null) return;
        if (!DamageSourceClassifier.IsPowerAttack(kickbackkind)) return;
        kickbackkind = KnockdownKickbackLightFlinch;
    }

    [HarmonyPatch(typeof(playercon), nameof(playercon.fun_damage))]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void FunDamage_ClearKnockdownStatePostfix(playercon __instance)
    {
        ClearErodownIfImmune(__instance);
    }

    [HarmonyPatch(typeof(playercon), nameof(playercon.fun_damage_Improvement))]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void FunDamageImprovement_ClearKnockdownStatePostfix(playercon __instance)
    {
        ClearErodownIfImmune(__instance);
    }

    [HarmonyPatch(typeof(playercon), "fun_nowdamage_move")]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void FunNowdamageMove_ClearErodownPostfix(playercon __instance)
    {
        ClearErodownIfImmune(__instance);
    }

    [HarmonyPatch(typeof(playercon), "Update")]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Update_ClearErodownAfterOtherMods(playercon __instance)
    {
        ClearErodownIfImmune(__instance);
    }

    private static void ClearErodownIfImmune(playercon pc)
    {
        if (pc == null || !PostEscapeGrabImmunity.IsActive) return;
        if (pc.erodown == 0) return;
        if (PlayerEroContextUtility.ShouldPreserveKnockdownState(pc)) return;
        pc.erodown = 0;
    }
}
