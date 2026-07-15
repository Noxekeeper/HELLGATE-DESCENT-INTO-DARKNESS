using System;
using HarmonyLib;
using NoREroMod.Systems.Gameplay.WeaponAnimations.Profiles;
using NoREroMod.Systems.Rage;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Systems.Gameplay.WeaponAnimations;

/// <summary>
/// Extends vanilla <see cref="playercon.atk_fun"/> for ground combo indices 5-8 when <see cref="PlayerStatus._AtkMotion"/> has 9+ entries.
/// Clamps <see cref="PlayerStatus.weaponcount"/> outside Rage when <see cref="Plugin.witchExtendedGroundComboRequiresRage"/> is true.
/// </summary>
[HarmonyPatch(typeof(playercon), "atk_fun")]
internal static class WitchExtendedGroundSwordComboPatch
{
    private const int WithoutRageWeaponCountMax = 2;

    private static int _atkCountBeforeAtkFun;
    private static int _savedWeaponCount = -1;

    [HarmonyPrefix]
    private static void Prefix(playercon __instance)
    {
        _savedWeaponCount = -1;
        if (__instance != null)
        {
            var ps = Traverse.Create(__instance).Field("playerstatus").GetValue<PlayerStatus>();
            if (ps != null
                && LightSwordExtendedComboProfile.IsMatch(ps)
                && (Plugin.witchExtendedGroundComboRequiresRage?.Value ?? true)
                && (!RageSystem.Enabled || !RageSystem.IsActive))
            {
                _savedWeaponCount = ps.weaponcount;
                ps.weaponcount = Mathf.Min(WithoutRageWeaponCountMax, ps.weaponcount);
            }
        }

        _atkCountBeforeAtkFun = __instance != null ? __instance.Atkcount : -1;
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.First)]
    private static void PostfixRestoreWeaponCount(playercon __instance)
    {
        if (_savedWeaponCount < 0) return;
        var ps = Traverse.Create(__instance).Field("playerstatus").GetValue<PlayerStatus>();
        if (ps != null)
            ps.weaponcount = _savedWeaponCount;
        _savedWeaponCount = -1;
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(playercon __instance)
    {
        if (__instance == null) return;
        if (Plugin.witchExtendedGroundComboRequiresRage?.Value ?? true)
        {
            if (!RageSystem.Enabled || !RageSystem.IsActive) return;
        }

        if (__instance.Atkcount != _atkCountBeforeAtkFun) return;

        var ps = Traverse.Create(__instance).Field("playerstatus").GetValue<PlayerStatus>();
        if (ps == null) return;
        if (ps.WeaponKind != 1) return;
        if (!LightSwordExtendedComboProfile.IsMatch(ps)) return;

        bool keyAtk = Traverse.Create(__instance).Field("key_atk").GetValue<bool>();
        if (!keyAtk) return;

        float atkInterbal = Traverse.Create(__instance).Field("Atkinterbal").GetValue<float>();
        bool itemUse = Traverse.Create(__instance).Field("Itemuse").GetValue<bool>();
        bool parry = Traverse.Create(__instance).Field("Parry").GetValue<bool>();

        if (!__instance.m_Grounded || atkInterbal != 0f || !ps._SOUSA || itemUse || parry) return;
        if (__instance.nowdamage || __instance.stepfrag || __instance.magicnow) return;
        if (ps.Sp < (float)__instance.atksp) return;

        if (!(__instance.Attacknow && __instance.Atkcombo == 1 && __instance.Atkcount <= ps.weaponcount)) return;

        int ac = __instance.Atkcount;
        if (ac < 5 || ac > 8) return;
        if (ps._AtkMotion == null || ac >= ps._AtkMotion.Count) return;
        if (ps._SmashKind == null || ac >= ps._SmashKind.Count) return;

        try
        {
            ps.PleasureParalysisActionPercentage();
            __instance.rigi2d.velocity = new Vector2(0f, __instance.rigi2d.velocity.y);
            SubscribeSpineComplete(__instance);

            Traverse.Create(__instance).Field("ATKchage").SetValue(false);
            Traverse.Create(__instance).Field("ATKmove").SetValue(10f);
            ps._NowSmashKind = ps._SmashKind[ac];
            __instance.ATKID = UnityEngine.Random.Range(2f, 100f);
            __instance.dir_fun();
            ps.Sp -= (float)__instance.atksp;

            __instance.state = ps._AtkMotion[ac];
            Traverse.Create(__instance).Field("loopanim").SetValue(false);
            Traverse.Create(__instance).Field("imagetime").SetValue(1.5f);

            __instance.Atkcount++;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[LightSwordExtendedCombo] hit {ac}: {ex.Message}");
        }
    }

    private static void SubscribeSpineComplete(playercon pc)
    {
        var sk = pc.gameObject.GetComponent<SkeletonAnimation>();
        if (sk == null) sk = pc.gameObject.GetComponentInChildren<SkeletonAnimation>(true);
        if (sk == null) return;

        var onComplete = AccessTools.Method(typeof(playercon), "OnCompleteSpineAnim");
        var ev = sk.state.GetType().GetEvent("Complete");
        if (ev == null) return;

        var handler = Delegate.CreateDelegate(ev.EventHandlerType, pc, onComplete);
        ev.AddEventHandler(sk.state, handler);
    }
}
