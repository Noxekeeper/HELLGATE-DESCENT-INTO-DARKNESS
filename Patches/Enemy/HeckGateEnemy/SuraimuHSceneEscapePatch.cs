using System;
using DarkTonic.MasterAudio;
using HarmonyLib;
using NoREroMod.Patches.Player;
using NoREroMod.Systems.Cache;
using Spine.Unity;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoREroMod.Patches.Enemy.HeckGateEnemy;

/// <summary>
/// Vanilla struggle escape clears player <see cref="playercon.erodown"/> in <c>fun_nowdamage</c>, but
/// <see cref="suraimu"/> can keep <c>eroflag</c> / <c>erodata</c> / private <c>ero2data</c> active
/// (same class of bug as Wolf / MummyDog / Hellish Touzoku). Biscord also used to disable
/// <c>erodata</c> every frame during H, which breaks the vanilla release path.
/// </summary>
internal static class SuraimuHSceneEscapePatch
{
    internal static void AbortActiveSuraimuHSceneOnPlayerEscape(playercon player, bool requireErodownClear)
    {
        if (player == null)
            return;

        if (requireErodownClear && player.erodown != 0)
            return;

        if (!IsAnySuraimuHSceneVisualActive())
            return;

        bool abortedAny = false;
        foreach (suraimu slime in Object.FindObjectsOfType<suraimu>())
        {
            if (slime != null && TryAbortSingleSuraimu(slime))
                abortedAny = true;
        }

        if (!abortedAny)
            return;

        ClearPlayerHSceneFlags(player);
        CancelPendingHandoff();
        PlayerCombatControlRecovery.RestoreAfterStruggleEscape();
    }

    internal static bool TryAbortSingleSuraimu(suraimu slime)
    {
        if (slime == null)
            return false;

        bool eroDataActive = slime.erodata != null && slime.erodata.activeSelf;
        bool ero2Active = TryGetEro2Data(slime, out GameObject ero2Data) && ero2Data.activeSelf;
        if (!slime.eroflag && !eroDataActive && !ero2Active)
            return false;

        try
        {
            try
            {
                MasterAudio.StopBus("EroVoice");
            }
            catch
            {
            }

            SkeletonAnimation eroSpine = slime.erodata != null
                ? slime.erodata.GetComponent<SkeletonAnimation>()
                : null;
            eroSpine?.AnimationState?.ClearTracks();

            if (ero2Active)
            {
                SkeletonAnimation ero2Spine = ero2Data.GetComponent<SkeletonAnimation>();
                ero2Spine?.AnimationState?.ClearTracks();
                ero2Data.SetActive(false);
            }

            if (eroDataActive)
                slime.erodata.SetActive(false);

            slime.eroflag = false;

            MeshRenderer meshRenderer = slime.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
                meshRenderer.enabled = true;

            Rigidbody2D rigidBody = AccessTools.Field(typeof(EnemyDate), "rigi2D")?.GetValue(slime) as Rigidbody2D;
            if (rigidBody != null && !rigidBody.simulated)
                rigidBody.simulated = true;

            try
            {
                slime.ero_camerareset();
            }
            catch
            {
            }

            try
            {
                slime.state = suraimu.enemystate.BLANK;
            }
            catch
            {
            }

            GameObject ui = AccessTools.Field(typeof(suraimu), "UI")?.GetValue(slime) as GameObject;
            if (ui != null)
                ui.SetActive(true);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[SuraimuEscape] Failed to abort H-scene: " + ex.Message);
            return false;
        }

        return true;
    }

    private static bool IsAnySuraimuHSceneVisualActive()
    {
        foreach (suraimu slime in Object.FindObjectsOfType<suraimu>())
        {
            if (slime != null && IsSlimeInActiveHScene(slime))
                return true;
        }

        return false;
    }

    internal static bool IsPlayerInActiveSuraimuHScene(playercon player)
    {
        if (player == null || !player.eroflag)
            return false;

        foreach (suraimu slime in Object.FindObjectsOfType<suraimu>())
        {
            if (slime != null && slime.eroflag && slime.com_player == player && IsSlimeInActiveHScene(slime))
                return true;
        }

        return false;
    }

    internal static bool IsSlimeInActiveHScene(suraimu slime)
    {
        if (slime == null)
            return false;

        if (slime.eroflag || (slime.erodata != null && slime.erodata.activeSelf))
            return true;

        return TryGetEro2Data(slime, out GameObject ero2Data) && ero2Data.activeSelf;
    }

    private static bool TryGetEro2Data(suraimu slime, out GameObject ero2Data)
    {
        ero2Data = null;
        if (slime == null)
            return false;

        try
        {
            object value = Traverse.Create(slime).Field("ero2data").GetValue();
            ero2Data = value as GameObject;
            return ero2Data != null;
        }
        catch
        {
            return false;
        }
    }

    internal static void ClearPlayerHSceneFlags(playercon player)
    {
        if (player == null)
            return;

        player.eroflag = false;

        try
        {
            Traverse.Create(player).Field("_eroflag2").SetValue(false);
        }
        catch
        {
            try
            {
                Traverse.Create(player).Field("eroflag2").SetValue(false);
            }
            catch
            {
            }
        }

        if (!player._Death)
        {
            try
            {
                player.rigi2d.simulated = true;
            }
            catch
            {
            }
        }
    }

    private static void CancelPendingHandoff()
    {
        try
        {
            GameObject playerObject = UnifiedPlayerCacheManager.GetPlayerObject();
            if (playerObject != null)
            {
                var script = playerObject.GetComponent<DelayedHandoffScript>();
                if (script != null)
                {
                    script.StopAllCoroutines();
                    Object.Destroy(script);
                }
            }

            GameObject temp = GameObject.Find("DelayedHandoffTemp");
            if (temp != null)
                Object.Destroy(temp);
        }
        catch
        {
        }
    }
}

/// <summary>
/// Keeps suraimu grabs compatible with NoREroMod struggle: ero2 swallow often leaves erodown at 0,
/// and EroAnimation_suraimu sets struggle level 10 (no SP gain when enableImpossibleStruggles is on).
/// </summary>
[HarmonyPatch(typeof(playercon), "fun_nowdamage")]
internal static class SuraimuStruggleContextPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void EnsureSuraimuStruggleContext(playercon __instance)
    {
        if (__instance == null || PlayerEroContextUtility.ShouldBlockEnemyStruggleAutomation(__instance))
            return;
        if (!SuraimuHSceneEscapePatch.IsPlayerInActiveSuraimuHScene(__instance))
            return;

        if (__instance.erodown == 0)
            __instance.erodown = 1;

        StruggleSystem.setStruggleLevel(-1);

        try
        {
            Traverse.Create(__instance).Field("_easyESC").SetValue(false);
        }
        catch
        {
        }
    }
}

/// <summary>
/// At full SP, NoREroMod's fun_nowdamage prefix is skipped; vanilla still needs downup &gt;= 2.
/// Force the same end state as a successful struggle when suraimu H is active.
/// </summary>
[HarmonyPatch(typeof(playercon), "fun_nowdamage")]
internal static class SuraimuStruggleMaxSpEscapePostfix
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void ForceEscapeWhenSpFull(playercon __instance, PlayerStatus ___playerstatus)
    {
        if (__instance == null || ___playerstatus == null)
            return;
        if (PlayerEroContextUtility.ShouldBlockEnemyStruggleAutomation(__instance))
            return;
        if (__instance.erodown == 0 || __instance._easyESC || !___playerstatus._SOUSA)
            return;
        if (___playerstatus.Sp < ___playerstatus.AllMaxSP())
            return;
        if (!SuraimuHSceneEscapePatch.IsPlayerInActiveSuraimuHScene(__instance))
            return;

        __instance.erodown = 0;
        __instance.nowdamage = false;
        __instance.tough = __instance.maxtough;

        try
        {
            Traverse.Create(__instance).Field("damecount").SetValue(0f);
            Traverse.Create(__instance).Field("downup").SetValue(0);
        }
        catch
        {
        }

        StruggleSystem.startGrabInvul();
        SuraimuHSceneEscapePatch.AbortActiveSuraimuHSceneOnPlayerEscape(__instance, requireErodownClear: true);
    }
}

[HarmonyPatch(typeof(EroAnimation_suraimu), "OnEvent")]
internal static class SuraimuEroAnimationStrugglePatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void KeepStruggleWindowOpen()
    {
        StruggleSystem.setStruggleLevel(-1);
    }
}

[HarmonyPatch(typeof(suraimu_hannomi), "OnEvent")]
internal static class SuraimuHannomiStrugglePatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void KeepStruggleWindowOpenForEro2()
    {
        StruggleSystem.setStruggleLevel(-1);
    }
}

[HarmonyPatch(typeof(suraimu), "Update")]
internal static class SuraimuActiveHSceneStrugglePatch
{
    [HarmonyPostfix]
    private static void MaintainStruggleWhileHActive(suraimu __instance)
    {
        if (__instance == null || !__instance.eroflag || __instance.com_player == null)
            return;
        if (!SuraimuHSceneEscapePatch.IsSlimeInActiveHScene(__instance))
            return;
        if (!__instance.com_player.eroflag)
            return;

        if (__instance.com_player.erodown == 0)
            __instance.com_player.erodown = 1;

        StruggleSystem.setStruggleLevel(-1);
    }
}

[HarmonyPatch(typeof(playercon), "fun_nowdamage")]
internal static class SuraimuHSceneEscapeFunNowDamagePatch
{
    private static int _playerErodownBeforeFunNowdamage;

    [HarmonyPrefix]
    private static void BeforeFunNowdamage(playercon __instance)
    {
        _playerErodownBeforeFunNowdamage = __instance != null ? __instance.erodown : 0;
    }

    [HarmonyPostfix]
    private static void AfterFunNowdamage(playercon __instance)
    {
        try
        {
            if (__instance == null
                || _playerErodownBeforeFunNowdamage == 0
                || __instance.erodown != 0)
            {
                return;
            }

            SuraimuHSceneEscapePatch.AbortActiveSuraimuHSceneOnPlayerEscape(__instance, requireErodownClear: true);
            StruggleSystem.startGrabInvul();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[SuraimuEscape] fun_nowdamage cleanup failed: " + ex.Message);
        }
    }
}

[HarmonyPatch(typeof(playercon), nameof(playercon.ImmediatelyERO))]
internal static class SuraimuHSceneEscapeGiveUpPatch
{
    [HarmonyPostfix]
    private static void OnGiveUpCleanup(playercon __instance)
    {
        try
        {
            SuraimuHSceneEscapePatch.AbortActiveSuraimuHSceneOnPlayerEscape(__instance, requireErodownClear: false);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[SuraimuEscape] GiveUp cleanup failed: " + ex.Message);
        }
    }
}
