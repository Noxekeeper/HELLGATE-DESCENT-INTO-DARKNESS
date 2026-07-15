using System;
using System.Reflection;
using HarmonyLib;
using NoREroMod;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Patches.Enemy.BossTouzokuCustom;

/// <summary>Blocks vanilla boss-arena intro / elite hooks for field-spawn BossTouzokuCustom.</summary>
internal static class BossTouzokuCustomIntroPatches
{
    [HarmonyPatch(typeof(BossTouzoku), nameof(BossTouzoku.flagCall))]
    internal static class BlockFlagCallPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(BossTouzoku __instance)
        {
            return !BossTouzokuCustomStats.IsCustom(__instance);
        }
    }

    [HarmonyPatch(typeof(BossTouzoku), nameof(BossTouzoku.flagCall_Dialog))]
    internal static class BlockFlagCallDialogPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(BossTouzoku __instance)
        {
            return !BossTouzokuCustomStats.IsCustom(__instance);
        }
    }

    [HarmonyPatch(typeof(BossTouzoku), nameof(BossTouzoku.flag_BossBattlestart))]
    internal static class BlockFlagBossBattleStartPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(BossTouzoku __instance)
        {
            return !BossTouzokuCustomStats.IsCustom(__instance);
        }
    }

    [HarmonyPatch(typeof(BossTouzoku), "next")]
    internal static class BlockNextPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(BossTouzoku __instance)
        {
            return !BossTouzokuCustomStats.IsCustom(__instance);
        }
    }

    [HarmonyPatch(typeof(BossTouzoku), "OnEvent")]
    internal static class CustomDeathEventPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(BossTouzoku __instance, Spine.Event e)
        {
            if (!BossTouzokuCustomStats.IsCustom(__instance))
                return true;

            if (e?.Data == null)
                return true;

            string eventName = e.Data.Name;
            SkeletonAnimation spine = __instance.GetComponent<SkeletonAnimation>();
            string animName = spine != null ? spine.AnimationName : string.Empty;

            if (eventName == "death")
            {
                if (__instance.Hp > 0f)
                {
                    Plugin.Log?.LogWarning(
                        "[BossTouzokuCustom] Blocked spurious death event while alive (anim="
                        + animName
                        + " hp="
                        + __instance.Hp.ToString("0.##")
                        + ").");
                    return false;
                }

                Plugin.Log?.LogInfo("[BossTouzokuCustom] Death event accepted (hp<=0).");
                UnityEngine.Object.Destroy(__instance.gameObject);
                return false;
            }

            if (!string.IsNullOrEmpty(animName)
                && animName.StartsWith("START", StringComparison.Ordinal))
            {
                return false;
            }

            if (animName == "START6" && eventName == "END")
                return false;

            if (animName == "START2" && eventName == "SE")
                return false;

            if (animName == "START6" && eventName == "SE")
                return false;

            return true;
        }
    }

    [HarmonyPatch]
    internal static class BlockNoREroModSuperBossSpawnPatch
    {
        private static MethodBase TargetMethod()
        {
            System.Type patchType = HellGateTypeResolver.Resolve("NoREroMod.EnemyDatePatch");
            return patchType == null ? null : AccessTools.Method(patchType, "SpawnSuperBossEnemy");
        }

        private static bool Prepare() => TargetMethod() != null;

        [HarmonyPrefix]
        private static bool Prefix(EnemyDate __instance)
        {
            return __instance is not BossTouzoku boss || !BossTouzokuCustomStats.IsCustom(boss);
        }
    }

    [HarmonyPatch]
    internal static class BlockNoREroModBossHpMultiPatch
    {
        private static MethodBase TargetMethod()
        {
            System.Type patchType = HellGateTypeResolver.Resolve("NoREroMod.EnemyDatePatch");
            return patchType == null ? null : AccessTools.Method(patchType, "BossHPAndEXPMulti");
        }

        private static bool Prepare() => TargetMethod() != null;

        [HarmonyPrefix]
        private static bool Prefix(EnemyDate __instance)
        {
            return __instance is not BossTouzoku boss || !BossTouzokuCustomStats.IsCustom(boss);
        }
    }

    [HarmonyPatch]
    internal static class BlockNoREroModSuperBossSpeedPatch
    {
        private static MethodBase TargetMethod()
        {
            System.Type patchType = HellGateTypeResolver.Resolve("NoREroMod.EnemyDatePatch");
            return patchType == null ? null : AccessTools.Method(patchType, "SuperBossEnemySpeed");
        }

        private static bool Prepare() => TargetMethod() != null;

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(EnemyDate __instance)
        {
            return __instance is not BossTouzoku boss || !BossTouzokuCustomStats.IsCustom(boss);
        }
    }

    [HarmonyPatch]
    internal static class BlockNoREroModSuperEnemySpeedPatch
    {
        private static MethodBase TargetMethod()
        {
            System.Type patchType = HellGateTypeResolver.Resolve("NoREroMod.EnemyDatePatch");
            return patchType == null ? null : AccessTools.Method(patchType, "SuperEnemySpeed");
        }

        private static bool Prepare() => TargetMethod() != null;

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(EnemyDate __instance)
        {
            return __instance is not BossTouzoku boss || !BossTouzokuCustomStats.IsCustom(boss);
        }
    }

    [HarmonyPatch]
    internal static class BlockNoREroModSuperResteColorPatch
    {
        private static MethodBase TargetMethod()
        {
            System.Type patchType = HellGateTypeResolver.Resolve("NoREroMod.EnemyDatePatch");
            return patchType == null ? null : AccessTools.Method(patchType, "SuperEnemyColor");
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
}
