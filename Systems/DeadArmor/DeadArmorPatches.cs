using System;
using System.Reflection;
using HarmonyLib;
using Spine;
using Spine.Unity;

namespace NoREroMod.Systems.DeadArmor;

/// <summary>
/// Detects NikuArmor break on SlaveBigAxe / OtherSlavebigAxe and plays DeadArmor clips.
/// Uses typed prefix snapshots (no Harmony __state) so detection is reliable on HarmonyX.
/// </summary>
internal static class DeadArmorPatches
{
    private static bool _applied;
    private static FieldInfo _slaveNikuArmor;
    private static FieldInfo _otherNikuArmor;
    private static FieldInfo _slaveSpine;
    private static FieldInfo _otherSpine;

    private static bool _slaveWasArmored;
    private static bool _otherWasArmored;
    private static string _slaveSkinBefore;
    private static string _otherSkinBefore;

    public static void Apply(Harmony harmony)
    {
        if (_applied || harmony == null)
            return;

        try
        {
            _slaveNikuArmor = AccessTools.Field(typeof(SlaveBigAxe), "NikuArmor");
            _otherNikuArmor = AccessTools.Field(typeof(OtherSlavebigAxe), "NikuArmor");
            _slaveSpine = AccessTools.Field(typeof(SlaveBigAxe), "mySpine");
            _otherSpine = AccessTools.Field(typeof(OtherSlavebigAxe), "mySpine");

            PatchEnemy(harmony, typeof(SlaveBigAxe));
            PatchEnemy(harmony, typeof(OtherSlavebigAxe));

            _applied = true;
            DeadArmorConfig.LogDebug("[DeadArmor] Patches applied (SlaveBigAxe + OtherSlavebigAxe)");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError("[DeadArmor] Apply failed: " + ex.Message);
        }
    }

    private static void PatchEnemy(Harmony harmony, Type enemyType)
    {
        bool isSlave = enemyType == typeof(SlaveBigAxe);

        MethodInfo getdame = AccessTools.Method(enemyType, "getdame_fun");
        if (getdame != null)
        {
            harmony.Patch(
                getdame,
                prefix: new HarmonyMethod(typeof(DeadArmorPatches), isSlave ? nameof(Slave_Physical_Prefix) : nameof(Other_Physical_Prefix)),
                postfix: new HarmonyMethod(typeof(DeadArmorPatches), isSlave ? nameof(Slave_Physical_Postfix) : nameof(Other_Physical_Postfix)));
        }

        MethodInfo stab = AccessTools.Method(enemyType, "fun_enedamage_stab");
        if (stab != null)
        {
            harmony.Patch(
                stab,
                prefix: new HarmonyMethod(typeof(DeadArmorPatches), isSlave ? nameof(Slave_Physical_Prefix) : nameof(Other_Physical_Prefix)),
                postfix: new HarmonyMethod(typeof(DeadArmorPatches), isSlave ? nameof(Slave_Physical_Postfix) : nameof(Other_Physical_Postfix)));
        }

        MethodInfo magic = AccessTools.Method(enemyType, "fun_enedamage_mg");
        if (magic != null)
        {
            harmony.Patch(
                magic,
                prefix: new HarmonyMethod(typeof(DeadArmorPatches), isSlave ? nameof(Slave_Magic_Prefix) : nameof(Other_Magic_Prefix)),
                postfix: new HarmonyMethod(typeof(DeadArmorPatches), isSlave ? nameof(Slave_Magic_Postfix) : nameof(Other_Magic_Postfix)));
        }
    }

    private static void Slave_Physical_Prefix(SlaveBigAxe __instance)
    {
        SnapshotSlave(__instance);
    }

    private static void Slave_Physical_Postfix(SlaveBigAxe __instance)
    {
        TryPlaySlave(__instance, DeadArmorBreakKind.Physical);
    }

    private static void Slave_Magic_Prefix(SlaveBigAxe __instance)
    {
        SnapshotSlave(__instance);
    }

    private static void Slave_Magic_Postfix(SlaveBigAxe __instance)
    {
        TryPlaySlave(__instance, DeadArmorBreakKind.Magic);
    }

    private static void Other_Physical_Prefix(OtherSlavebigAxe __instance)
    {
        SnapshotOther(__instance);
    }

    private static void Other_Physical_Postfix(OtherSlavebigAxe __instance)
    {
        TryPlayOther(__instance, DeadArmorBreakKind.Physical);
    }

    private static void Other_Magic_Prefix(OtherSlavebigAxe __instance)
    {
        SnapshotOther(__instance);
    }

    private static void Other_Magic_Postfix(OtherSlavebigAxe __instance)
    {
        TryPlayOther(__instance, DeadArmorBreakKind.Magic);
    }

    private static void SnapshotSlave(SlaveBigAxe instance)
    {
        _slaveWasArmored = ReadBool(_slaveNikuArmor, instance);
        _slaveSkinBefore = ReadSkinName(_slaveSpine, instance);
    }

    private static void SnapshotOther(OtherSlavebigAxe instance)
    {
        _otherWasArmored = ReadBool(_otherNikuArmor, instance);
        _otherSkinBefore = ReadSkinName(_otherSpine, instance);
    }

    private static void TryPlaySlave(SlaveBigAxe instance, DeadArmorBreakKind kind)
    {
        if (!DeadArmorConfig.IsEnabled || instance == null)
            return;

        if (NoREroMod.Patches.Enemy.SlaveBigAxeIllusiveEventGate.ShouldSkipHellGateLogic())
            return;

        bool nowArmored = ReadBool(_slaveNikuArmor, instance);
        string skinNow = ReadSkinName(_slaveSpine, instance);
        bool brokeFlag = _slaveWasArmored && !nowArmored;
        bool brokeSkin = IsNikuSkinName(_slaveSkinBefore) && !IsNikuSkinName(skinNow);

        if (!brokeFlag && !brokeSkin)
            return;

        DeadArmorPlayback.Play(instance, kind);
    }

    private static void TryPlayOther(OtherSlavebigAxe instance, DeadArmorBreakKind kind)
    {
        if (!DeadArmorConfig.IsEnabled || instance == null)
            return;

        bool nowArmored = ReadBool(_otherNikuArmor, instance);
        string skinNow = ReadSkinName(_otherSpine, instance);
        bool brokeFlag = _otherWasArmored && !nowArmored;
        bool brokeSkin = IsNikuSkinName(_otherSkinBefore) && !IsNikuSkinName(skinNow);

        if (!brokeFlag && !brokeSkin)
            return;

        DeadArmorPlayback.Play(instance, kind);
    }

    private static bool ReadBool(FieldInfo field, object instance)
    {
        if (field == null || instance == null)
            return false;
        try
        {
            return (bool)field.GetValue(instance);
        }
        catch
        {
            return false;
        }
    }

    private static string ReadSkinName(FieldInfo spineField, object instance)
    {
        try
        {
            SkeletonAnimation spine = null;
            if (spineField != null)
                spine = spineField.GetValue(instance) as SkeletonAnimation;
            if (spine == null && instance is UnityEngine.Component c)
                spine = c.GetComponent<SkeletonAnimation>();
            if (spine?.skeleton?.Skin == null)
                return null;
            return spine.skeleton.Skin.Name;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsNikuSkinName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;
        return name.IndexOf("Niku", StringComparison.OrdinalIgnoreCase) >= 0
            && name.IndexOf("JIGO", StringComparison.OrdinalIgnoreCase) < 0;
    }
}
