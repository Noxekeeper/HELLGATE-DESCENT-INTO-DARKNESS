using System.Linq;
using System.Reflection;
using HarmonyLib;
using NoREroMod.Systems.GrabSystem;
using NoREroMod;

namespace NoREroMod.Systems.Rage.Patches;

/// <summary>
/// While <see cref="RageSystem.IsGrabKnockdownImmuneWhileRageActive"/>: blocks NoREroMod collision grab; grab-via-attack and knockdown power hits are handled elsewhere.
/// Prefixes <see cref="playercon.fun_damage"/> (ref kickback) and <see cref="playercon.fun_nowdamage_move"/> (ToKickbackkind), since knockdown is applied in the latter from stored kind.
/// </summary>
internal static class RageActiveImmunityPatch
{
    private const int KnockdownKickbackLightFlinch = 2;

    private static bool _collisionGrabPatched;

    /// <summary>
    /// Knockdown is applied in <c>fun_nowdamage_move</c> via <see cref="playercon.ToKickbackkind"/>; prefix ref on <c>fun_damage</c> is not always enough across builds.
    /// </summary>
    [HarmonyPatch(typeof(playercon), "fun_nowdamage_move")]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void FunNowdamageMove_KnockdownPrefix(playercon __instance)
    {
        if (!RageSystem.IsGrabKnockdownImmuneWhileRageActive) return;
        if (__instance == null || !__instance.nowdamage) return;
        int t = __instance.ToKickbackkind;
        if (!DamageSourceClassifier.IsPowerAttack(t)) return;
        __instance.ToKickbackkind = KnockdownKickbackLightFlinch;
    }

    /// <summary>Reflective prefix on <c>NoREroMod.EnemyDatePatch.CanEliteGrabPlayer</c> (all overloads).</summary>
    internal static void ApplyCollisionGrabBlock(Harmony harmony)
    {
        if (_collisionGrabPatched) return;
        try
        {
            var type = typeof(StruggleSystem).Assembly.GetType("NoREroMod.EnemyDatePatch");
            if (type == null)
            {
                Plugin.Log?.LogWarning("[RageImmunity] EnemyDatePatch not found; collision grab block skipped.");
                return;
            }

            var prefix = typeof(RageActiveImmunityPatch).GetMethod(
                nameof(CanEliteGrabPlayer_Prefix),
                BindingFlags.Static | BindingFlags.NonPublic);
            if (prefix == null) return;

            int patched = 0;
            foreach (var m in type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                         .Where(x => x.Name == "CanEliteGrabPlayer"))
            {
                try
                {
                    harmony.Patch(m, prefix: new HarmonyMethod(prefix) { priority = Priority.First });
                    patched++;
                }
                catch (System.Exception ex)
                {
                    Plugin.Log?.LogWarning($"[RageImmunity] CanEliteGrabPlayer patch failed: {ex.Message}");
                }
            }

            if (patched > 0)
            {
                _collisionGrabPatched = true;
                Plugin.Log?.LogInfo($"[RageImmunity] Patched {patched} CanEliteGrabPlayer overload(s) for active Rage.");
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogError($"[RageImmunity] ApplyCollisionGrabBlock failed: {ex.Message}");
        }
    }

    private static bool CanEliteGrabPlayer_Prefix(ref bool __result)
    {
        if (!RageSystem.IsGrabKnockdownImmuneWhileRageActive)
            return true;
        __result = false;
        return false;
    }

    [HarmonyPatch(typeof(playercon), nameof(playercon.fun_damage))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void FunDamage_KnockdownPrefix(playercon __instance, float getatk, float gettoughcut, ref int kickbackkind, int getdamedir, float damecount)
    {
        if (!RageSystem.IsGrabKnockdownImmuneWhileRageActive) return;
        if (__instance == null) return;
        if (!DamageSourceClassifier.IsPowerAttack(kickbackkind)) return;
        kickbackkind = KnockdownKickbackLightFlinch;
    }

    [HarmonyPatch(typeof(playercon), nameof(playercon.fun_damage_Improvement))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void FunDamageImprovement_KnockdownPrefix(playercon __instance, float getatk, float gettoughcut, ref int kickbackkind, int getdamedir, float damecount, bool Noguard, float spcut)
    {
        if (!RageSystem.IsGrabKnockdownImmuneWhileRageActive) return;
        if (__instance == null) return;
        if (!DamageSourceClassifier.IsPowerAttack(kickbackkind)) return;
        kickbackkind = KnockdownKickbackLightFlinch;
    }

    /// <summary>Vanilla may set <see cref="playercon.erodown"/> inside damage methods before <see cref="playercon.fun_nowdamage_move"/>; clear it when Rage immunity applies.</summary>
    [HarmonyPatch(typeof(playercon), nameof(playercon.fun_damage))]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void FunDamage_ClearKnockdownStatePostfix(playercon __instance)
    {
        if (!RageSystem.IsGrabKnockdownImmuneWhileRageActive) return;
        if (__instance == null || __instance.erodown == 0) return;
        if (PlayerEroContextUtility.ShouldPreserveKnockdownState(__instance)) return;
        Plugin.Log?.LogInfo("[OozeDiag][RageImmunity] fun_damage clears erodown (active Rage)");
        __instance.erodown = 0;
    }

    [HarmonyPatch(typeof(playercon), nameof(playercon.fun_damage_Improvement))]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void FunDamageImprovement_ClearKnockdownStatePostfix(playercon __instance)
    {
        if (!RageSystem.IsGrabKnockdownImmuneWhileRageActive) return;
        if (__instance == null || __instance.erodown == 0) return;
        if (PlayerEroContextUtility.ShouldPreserveKnockdownState(__instance)) return;
        Plugin.Log?.LogInfo("[OozeDiag][RageImmunity] fun_damage_Improvement clears erodown (active Rage)");
        __instance.erodown = 0;
    }

    [HarmonyPatch(typeof(playercon), "fun_nowdamage_move")]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void FunNowdamageMove_ClearErodownPostfix(playercon __instance)
    {
        if (!RageSystem.IsGrabKnockdownImmuneWhileRageActive) return;
        if (__instance == null || __instance.erodown == 0) return;
        if (PlayerEroContextUtility.ShouldPreserveKnockdownState(__instance)) return;
        Plugin.Log?.LogInfo("[OozeDiag][RageImmunity] fun_nowdamage_move clears erodown (active Rage)");
        __instance.erodown = 0;
    }

    /// <summary>End-of-frame cleanup: catches knockdown set outside <see cref="playercon.fun_damage"/> (e.g. animation / env).</summary>
    internal static void ProcessUpdateSuppression(playercon pc)
    {
        if (pc == null || !RageSystem.IsGrabKnockdownImmuneWhileRageActive) return;
        if (pc.erodown == 0) return;
        if (PlayerEroContextUtility.ShouldPreserveKnockdownState(pc)) return;
        Plugin.Log?.LogInfo("[OozeDiag][RageImmunity] Update suppression clears erodown (active Rage)");
        pc.erodown = 0;
    }

    /// <summary>Runs after other <c>Update</c> postfixes so nothing re-applies <c>erodown</c> after <see cref="ProcessUpdateSuppression"/>.</summary>
    [HarmonyPatch(typeof(playercon), "Update")]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Update_ClearErodownAfterOtherMods(playercon __instance)
    {
        ProcessUpdateSuppression(__instance);
    }
}
