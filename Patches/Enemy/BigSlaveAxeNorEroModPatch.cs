using System;
using System.Reflection;
using HarmonyLib;
using Spine.Unity;

namespace NoREroMod.Patches.Enemy;

/// <summary>
/// Fixes SlaveBigAxe girl-armor (<c>NikuArmor</c>) cleared by NoREroMod forks
/// (Hellachaz / Rebalance / HellGate-scaffold) on every <c>OnTriggerStay2D</c>.
///
/// Broken companion code in <c>NoREroMod.EnemyDatePatch.SlaveBigAxeGrab</c>:
/// <c>Field("NikuArmor").SetValue(false)</c> — drops the vanilla armor phase
/// without <c>SetSkin("Normal")</c>, so HP drains while the girl skin stays on.
///
/// Strategy:
/// 1) Prefix-skip the broken <c>SlaveBigAxeGrab</c> entirely (elite grab for this
///    enemy is already gated / disabled elsewhere; <c>OtherSlavebigAxeGrab</c> is fine).
/// 2) Safety: after <c>SlaveBigAxe.OnTriggerStay2D</c>, if Spine skin is still
///    <c>Niku</c>, force <c>NikuArmor = true</c> so any other clearer cannot desync.
/// </summary>
internal static class BigSlaveAxeNorEroModPatch
{
    private static bool _applied;
    private static FieldInfo _nikuArmorField;
    private static FieldInfo _mySpineField;

    public static void Apply(Harmony harmony)
    {
        if (_applied)
            return;

        try
        {
            _nikuArmorField = AccessTools.Field(typeof(SlaveBigAxe), "NikuArmor");
            _mySpineField = AccessTools.Field(typeof(SlaveBigAxe), "mySpine")
                ?? AccessTools.Field(typeof(EnemyDate), "mySpine");

            if (_nikuArmorField == null)
            {
                Plugin.Log?.LogWarning("[BigSlaveAxeNorEroMod] SlaveBigAxe.NikuArmor not found — skip");
                return;
            }

            int patched = 0;

            Assembly norAssembly = typeof(StruggleSystem).Assembly;
            Type enemyDatePatchType = norAssembly.GetType("NoREroMod.EnemyDatePatch");
            MethodInfo grab = enemyDatePatchType != null
                ? AccessTools.Method(enemyDatePatchType, "SlaveBigAxeGrab")
                : null;

            if (grab != null)
            {
                MethodInfo skipPrefix = typeof(BigSlaveAxeNorEroModPatch).GetMethod(
                    nameof(SlaveBigAxeGrab_SkipPrefix), BindingFlags.Static | BindingFlags.NonPublic);
                harmony.Patch(grab, prefix: new HarmonyMethod(skipPrefix) { priority = Priority.First });
                patched++;
                Plugin.Log?.LogInfo("[BigSlaveAxeNorEroMod] SlaveBigAxeGrab skipped (broken NikuArmor clear)");
            }
            else
            {
                Plugin.Log?.LogWarning("[BigSlaveAxeNorEroMod] SlaveBigAxeGrab not found — safety sync only");
            }

            MethodInfo stay = AccessTools.Method(typeof(SlaveBigAxe), "OnTriggerStay2D");
            if (stay != null)
            {
                MethodInfo stayPost = typeof(BigSlaveAxeNorEroModPatch).GetMethod(
                    nameof(OnTriggerStay2D_Postfix), BindingFlags.Static | BindingFlags.NonPublic);
                // Run after NoREroMod's OnTriggerStay2D postfixes.
                harmony.Patch(stay, postfix: new HarmonyMethod(stayPost) { priority = Priority.Last });
                patched++;
            }

            MethodInfo start = AccessTools.Method(typeof(SlaveBigAxe), "Start");
            if (start != null)
            {
                MethodInfo startPost = typeof(BigSlaveAxeNorEroModPatch).GetMethod(
                    nameof(Start_Postfix), BindingFlags.Static | BindingFlags.NonPublic);
                harmony.Patch(start, postfix: new HarmonyMethod(startPost) { priority = Priority.Last });
                patched++;
            }

            if (patched > 0)
            {
                _applied = true;
                Plugin.Log?.LogInfo($"[BigSlaveAxeNorEroMod] Applied ({patched} hooks) — NikuArmor protect");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[BigSlaveAxeNorEroMod] Apply failed: {ex.Message}");
        }
    }

    /// <summary>Do not run the companion method that clears NikuArmor.</summary>
    private static bool SlaveBigAxeGrab_SkipPrefix()
    {
        return false;
    }

    private static void Start_Postfix(SlaveBigAxe __instance)
    {
        SyncArmorToNikuSkin(__instance);
    }

    private static void OnTriggerStay2D_Postfix(SlaveBigAxe __instance)
    {
        SyncArmorToNikuSkin(__instance);
    }

    /// <summary>
    /// While the girl skin is still equipped, keep the armor flag on so weapon hits
    /// chip <c>Nikuarmorcount</c> instead of raw HP.
    /// </summary>
    private static void SyncArmorToNikuSkin(SlaveBigAxe instance)
    {
        if (instance == null || _nikuArmorField == null)
            return;

        try
        {
            if (!IsNikuSkinActive(instance))
                return;

            if (!(bool)_nikuArmorField.GetValue(instance))
                _nikuArmorField.SetValue(instance, true);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[BigSlaveAxeNorEroMod] SyncArmor failed: {ex.Message}");
        }
    }

    private static bool IsNikuSkinActive(SlaveBigAxe instance)
    {
        try
        {
            SkeletonAnimation spine = null;
            if (_mySpineField != null)
                spine = _mySpineField.GetValue(instance) as SkeletonAnimation;
            if (spine == null)
                spine = instance.GetComponent<SkeletonAnimation>();
            if (spine?.skeleton?.Skin == null)
                return false;

            string name = spine.skeleton.Skin.Name;
            return !string.IsNullOrEmpty(name)
                && name.IndexOf("Niku", StringComparison.OrdinalIgnoreCase) >= 0
                && name.IndexOf("JIGO", StringComparison.OrdinalIgnoreCase) < 0;
        }
        catch
        {
            return false;
        }
    }
}
