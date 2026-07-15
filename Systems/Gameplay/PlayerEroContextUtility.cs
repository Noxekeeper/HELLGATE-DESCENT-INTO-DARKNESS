using System;
using HarmonyLib;
using Spine.Unity;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoREroMod;

/// <summary>
/// Distinguishes enemy grab H-scenes from solo pleasure/orgasm states (FEEL / BadstatusEro).
/// HellGate struggle automation must not treat the latter as a downed grab to escape from.
/// </summary>
internal static class PlayerEroContextUtility
{
    internal static bool IsSoloPleasureState(playercon player)
    {
        if (player == null || player.eroflag)
            return false;

        string state = player.state;
        return state == "FEEL" || state == "FEEL2" || state == "FEEL3";
    }

    internal static bool IsAnyEnemyEroActive()
    {
        foreach (EnemyDate enemy in Object.FindObjectsOfType<EnemyDate>())
        {
            if (enemy != null && enemy.eroflag)
                return true;
        }

        return false;
    }

    /// <summary>
    /// True death-fatality playback where HellGate start-zoom / pan-target break the camera (void).
    /// Only <c>RequiemKnightFatality</c> (and GA twin) — they call <c>camera_GetComponent</c> but never
    /// <c>ero_camera_1/2</c>. All other *Fatality scripts are grab H-scenes (Slaughterer/Butcher,
    /// BossScapegoatentrance/BOSS2, Sheepheaddemon, Candore, Boss_Ranch, …) and use HellGate camera.
    /// </summary>
    internal static bool IsEnemyFatalityPlaybackActive()
    {
        try
        {
            EnemyDate[] enemies = Object.FindObjectsOfType<EnemyDate>();
            if (enemies == null)
                return false;

            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyDate enemy = enemies[i];
                if (enemy == null || !enemy.eroflag)
                    continue;

                GameObject eroRoot = enemy.erodata;
                if (eroRoot == null || !eroRoot.activeInHierarchy)
                    continue;

                Component[] parts = eroRoot.GetComponents<Component>();
                for (int j = 0; j < parts.Length; j++)
                {
                    Component part = parts[j];
                    if (part == null)
                        continue;

                    if (IsDeathFatalitySkipHellGateCamera(part.GetType().Name))
                        return true;
                }
            }
        }
        catch
        {
        }

        return false;
    }

    /// <summary>
    /// ERO scripts that must skip HellGate start-zoom / spacebar zoom / pan-target.
    /// Everything else named *Fatality is a normal grab (HellGate like BossScapegoatentrance).
    /// </summary>
    private static bool IsDeathFatalitySkipHellGateCamera(string componentTypeName)
    {
        return componentTypeName == "RequiemKnightFatality"
            || componentTypeName == "GARequiemKnightFatality";
    }

    /// <summary>Downed H-scene — arrow pan works here, including enemy death-fatality.</summary>
    internal static bool IsDownedHSceneForArrowPan(playercon player)
    {
        return player != null && player.eroflag && player.erodown != 0;
    }

    /// <summary>Grab H-scene with HellGate start-zoom / spacebar zoom (not enemy fatality when SkipEnemyFatality is on).</summary>
    internal static bool IsHellGateManagedGrabHScene(playercon player)
    {
        if (player == null || !player.eroflag || player.erodown == 0)
            return false;

        if (Plugin.startZoomSkipEnemyFatality?.Value ?? true)
            return !IsEnemyFatalityPlaybackActive();

        return true;
    }

    /// <summary>
    /// BadstatusEro orgasm overlay: player eroflag + erodown, but no enemy is in ERO.
    /// </summary>
    internal static bool IsSoloBadstatusOrgasm(playercon player)
    {
        if (player == null || !player.eroflag || player.erodown == 0)
            return false;

        return !IsAnyEnemyEroActive();
    }

    internal static bool ShouldBlockEnemyStruggleAutomation(playercon player)
    {
        // Block only pre-orgasm masturbation (FEEL* with erodown still 0).
        // BadstatusEro overlay (eroflag + erodown) should use normal QTE after SP reset in Erostart.
        return IsSoloPleasureState(player);
    }

    /// <summary>
    /// Keep vanilla <see cref="playercon.erodown"/> / mash stand-up / birth playback intact.
    /// Rage knockdown immunity must not zero erodown during DOWN, FEEL* intro, pregnancy birth,
    /// or solo badstatus H-scenes (eroflag + erodown, no enemy ERO).
    /// </summary>
    internal static bool ShouldPreserveKnockdownState(playercon player)
    {
        if (player == null)
            return true;

        if (player.eroflag || player._easyESC)
            return true;

        if (IsSoloPleasureState(player) || IsSoloBadstatusOrgasm(player))
            return true;

        if (player.erodown != 0 && (player.state == "DOWN" || player.nowdamage))
            return true;

        return false;
    }

    /// <summary>
    /// Active pregnancy birth overlay (BadstatusBirth spine). Vanilla disables the main player mesh here.
    /// </summary>
    internal static bool IsActivePregnancyBirth(playercon player)
    {
        if (player == null || !player.eroflag)
            return false;

        try
        {
            PlayerconBadstatusPregnancy pregnancy =
                Traverse.Create(player).Field<PlayerconBadstatusPregnancy>("Pregnancyero").Value;
            if (pregnancy == null)
                return false;

            bool[] flags = Traverse.Create(pregnancy).Field<bool[]>("eroflag").Value;
            return flags != null && flags.Length > 4 && (flags[3] || flags[4]);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// FEEL3 intro or birth overlay — HellGate visual restore must not re-enable the main body mesh.
    /// </summary>
    internal static bool ShouldPreserveBadstatusBirthVisuals(playercon player)
    {
        if (player == null)
            return false;

        if (Plugin.isBirthing || IsActivePregnancyBirth(player))
            return true;

        return player._easyESC && player.state == "FEEL3";
    }

    /// <summary>Vanilla Birthstart hides the main player mesh; reinforce when cleanup patches run.</summary>
    internal static void HideMainPlayerBodyForBadstatusOverlay(playercon player)
    {
        if (player == null)
            return;

        try
        {
            SkeletonAnimation spine = Traverse.Create(player).Field<SkeletonAnimation>("spineanime").Value;
            if (spine == null)
                spine = player.GetComponent<SkeletonAnimation>() ?? player.GetComponentInChildren<SkeletonAnimation>(true);

            if (spine == null)
                return;

            MeshRenderer mesh = spine.GetComponent<MeshRenderer>();
            if (mesh != null)
                mesh.enabled = false;
        }
        catch
        {
        }
    }
}

/// <summary>
/// Solo orgasm sets erodown via spine END, not fun_damage — NoREroMod never zeroes SP on this path.
/// Mirror enemy-down setup so QTE struggle works from empty SP.
/// </summary>
[HarmonyPatch(typeof(PlayerconBadero), "Erostart")]
internal static class SoloOrgasmStruggleSetupPatch
{
    [HarmonyPostfix]
    private static void ResetSpForOrgasmStruggle(PlayerStatus Pl)
    {
        if (Pl == null)
            return;

        Pl.Sp = 0f;
        StruggleSystem.setStruggleLevel(-1);
    }
}

/// <summary>
/// NoREroMod <c>recovery_fun</c> uses idle SP regen when <c>erodown == 0</c> (FEEL) or <c>isOrgasming</c> (BadstatusEro).
/// Passive regen must not run during solo pleasure/orgasm — SP should only change via struggle/QTE clicks.
/// </summary>
[HarmonyPatch(typeof(playercon), "recovery_fun")]
internal static class SoloPleasureSpRecoveryPatch
{
    private static float spBeforeRecovery;

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void SnapshotSp(playercon __instance, PlayerStatus ___playerstatus)
    {
        if (ShouldFreezePassiveSpRegen(__instance) && ___playerstatus != null)
            spBeforeRecovery = ___playerstatus.Sp;
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void RestoreSpAfterPassiveRegen(playercon __instance, PlayerStatus ___playerstatus)
    {
        if (!ShouldFreezePassiveSpRegen(__instance) || ___playerstatus == null)
            return;

        ___playerstatus.Sp = spBeforeRecovery;
    }

    private static bool ShouldFreezePassiveSpRegen(playercon player)
    {
        return PlayerEroContextUtility.IsSoloPleasureState(player)
            || PlayerEroContextUtility.IsSoloBadstatusOrgasm(player);
    }
}

/// <summary>
/// Vanilla <c>anime_fun</c> switches to IDLE when <c>nowdamage</c> timer expires even if FEEL animation is still playing.
/// </summary>
[HarmonyPatch(typeof(playercon), "anime_fun")]
internal static class SoloPleasureAnimeFunPatch
{
    [HarmonyPostfix]
    private static void RestoreFeelStateWhileAnimationPlaying(playercon __instance)
    {
        if (__instance == null || __instance.eroflag || __instance.erodown != 0)
            return;

        try
        {
            var spineField = AccessTools.Field(typeof(playercon), "spineanime");
            var spine = spineField?.GetValue(__instance) as SkeletonAnimation;
            if (spine == null)
                return;

            string anim = spine.AnimationName;
            if (anim == "FEEL" || anim == "FEEL2" || anim == "FEEL3")
                __instance.state = anim;
        }
        catch
        {
        }
    }
}
