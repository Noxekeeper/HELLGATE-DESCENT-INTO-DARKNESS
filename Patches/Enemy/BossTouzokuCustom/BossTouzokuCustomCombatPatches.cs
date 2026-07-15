using System;
using System.Reflection;
using HarmonyLib;
using NoREroMod;

namespace NoREroMod.Patches.Enemy.BossTouzokuCustom;

/// <summary>Intro/visibility/death-time overrides; weapon hits use custom damage path.</summary>
internal static class BossTouzokuCustomCombatPatches
{
    private static FieldInfo _jumpAtkField;

    [HarmonyPatch(typeof(BossTouzoku), "fun_enedamage")]
    internal static class ForceDamageOnHitPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(BossTouzoku __instance)
        {
            if (!BossTouzokuCustomStats.IsCustom(__instance))
                return true;

            if (BossTouzokuCustomRuntime.IsWeaponHitReactionGuard(__instance))
                return true;

            FieldInfo jumpField = _jumpAtkField ??= AccessTools.Field(typeof(BossTouzoku), "jumpatk");
            if (jumpField?.GetValue(__instance) is bool jump && jump)
                return true;

            BossTouzokuCustomRuntime.TryApplyCustomWeaponHit(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(BossTouzoku), "fun_enedamage_mg")]
    internal static class ForceMagicDamageOnHitPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(
            BossTouzoku __instance,
            float[] damage,
            float dir,
            int attribute,
            float cut,
            float FalterDIR)
        {
            if (!BossTouzokuCustomStats.IsCustom(__instance))
                return true;

            FieldInfo jumpField = _jumpAtkField ??= AccessTools.Field(typeof(BossTouzoku), "jumpatk");
            if (jumpField?.GetValue(__instance) is bool jump && jump)
                return true;

            BossTouzokuCustomRuntime.TryApplyCustomMagicHit(
                __instance, damage, dir, attribute, cut, FalterDIR);
            return false;
        }
    }

    [HarmonyPatch(typeof(BossTouzoku), "setanimation")]
    internal static class BlockIntroAnimationPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(BossTouzoku __instance, ref string name, ref bool loop)
        {
            if (!BossTouzokuCustomStats.IsCustom(__instance))
                return true;

            if (__instance.eroflag)
                return false;

            if (string.IsNullOrEmpty(name))
                return true;

            if (name.StartsWith("START", StringComparison.Ordinal)
                || name == "EVENT"
                || name == "EVENT2"
                || name == "EVENT3")
            {
                name = "IDLE";
                loop = true;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(BossTouzoku), "BattleStart")]
    internal static class BlockBattleStartPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(BossTouzoku __instance)
        {
            if (!BossTouzokuCustomStats.IsCustom(__instance))
                return true;

            BossTouzokuCustomRuntime.RunBattleStartBootstrap(__instance, hideBossUi: true);
            return false;
        }
    }

    [HarmonyPatch]
    internal static class BlockBossEnemyFovPatch
    {
        private static MethodBase TargetMethod()
        {
            System.Type patchType = HellGateTypeResolver.Resolve("NoREroMod.EnemyDatePatch");
            return patchType == null
                ? null
                : AccessTools.Method(patchType, "BossEnemyFOV");
        }

        private static bool Prepare() => TargetMethod() != null;

        [HarmonyPrefix]
        private static bool Prefix(EnemyDate __instance)
        {
            return __instance is not BossTouzoku boss || !BossTouzokuCustomStats.IsCustom(boss);
        }
    }

    [HarmonyPatch(typeof(BossTouzoku), "State")]
    internal static class GuardFieldMobStatePatch
    {
        [HarmonyPrefix]
        private static void Prefix(BossTouzoku __instance, ref BossTouzoku.enemystate val)
        {
            if (!BossTouzokuCustomStats.IsCustom(__instance))
                return;

            val = BossTouzokuCustomRuntime.NormalizeFieldMobState(val);
        }
    }

    [HarmonyPatch(typeof(BossTouzoku), "REtimescale")]
    internal static class BlockDeathSlowMoPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(BossTouzoku __instance)
        {
            return !BossTouzokuCustomStats.IsCustom(__instance);
        }
    }

    [HarmonyPatch]
    internal static class BlockUpdateFovDirectPatch
    {
        private static MethodBase TargetMethod()
        {
            System.Type patchType = HellGateTypeResolver.Resolve("NoREroMod.EnemyDatePatch");
            return patchType == null
                ? null
                : AccessTools.Method(patchType, "UpdateFOV");
        }

        private static bool Prepare() => TargetMethod() != null;

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(EnemyDate __instance)
        {
            if (__instance is BossTouzoku boss && BossTouzokuCustomStats.IsCustom(boss))
                return false;

            return true;
        }
    }

    [HarmonyPatch(typeof(BossTouzoku), "fun_animekind")]
    internal static class BlockAnimeKindDuringEroPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(BossTouzoku __instance)
        {
            if (!BossTouzokuCustomStats.IsCustom(__instance))
                return true;

            if (__instance.eroflag)
                return false;

            return true;
        }
    }
}
