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
/// SuccubusSpine H-scene: vanilla clears player <c>erodown</c> in <c>fun_nowdamage</c>, but
/// <c>eroflag</c> / <c>erodata</c> / hidden player mesh can stick (same class as Suraimu / Wolf /
/// Hellish Touzoku). Frozen "player double" is the combined SuccubusERO spine.
/// </summary>
internal static class SuccubusHSceneEscapePatch
{
    internal static void AbortActiveSuccubusHSceneOnPlayerEscape(playercon player, bool requireErodownClear)
    {
        if (player == null)
            return;

        if (requireErodownClear && player.erodown != 0)
            return;

        if (!IsAnySuccubusHSceneVisualActive())
            return;

        bool abortedAny = false;
        foreach (SuccubusSpine boss in Object.FindObjectsOfType<SuccubusSpine>())
        {
            if (boss != null && TryAbortSingle(boss))
                abortedAny = true;
        }

        if (!abortedAny)
            return;

        ClearPlayerHSceneFlags(player);
        CancelPendingHandoff();
        PlayerCombatControlRecovery.RestoreAfterStruggleEscape();
    }

    internal static bool TryAbortSingle(SuccubusSpine boss)
    {
        if (boss == null)
            return false;

        bool eroDataActive = boss.erodata != null && boss.erodata.activeSelf;
        if (!boss.eroflag && !eroDataActive)
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

            SkeletonAnimation eroSpine = AccessTools.Field(typeof(SuccubusSpine), "erospine")
                ?.GetValue(boss) as SkeletonAnimation;
            if (eroSpine == null && boss.erodata != null)
                eroSpine = boss.erodata.GetComponent<SkeletonAnimation>();
            eroSpine?.AnimationState?.ClearTracks();

            if (eroDataActive)
                boss.erodata.SetActive(false);

            boss.eroflag = false;

            MeshRenderer meshRenderer = AccessTools.Field(typeof(SuccubusSpine), "myspinerennder")
                ?.GetValue(boss) as MeshRenderer;
            if (meshRenderer == null)
                meshRenderer = boss.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
                meshRenderer.enabled = true;

            Rigidbody2D rigidBody = AccessTools.Field(typeof(EnemyDate), "rigi2D")?.GetValue(boss) as Rigidbody2D;
            if (rigidBody != null && !rigidBody.simulated)
                rigidBody.simulated = true;

            GameObject ui = AccessTools.Field(typeof(SuccubusSpine), "UI")?.GetValue(boss) as GameObject;
            if (ui != null)
                ui.SetActive(true);

            try
            {
                AudioSource bgm = AccessTools.Field(typeof(SuccubusSpine), "BGM")?.GetValue(boss) as AudioSource;
                object bgmVolObj = AccessTools.Field(typeof(SuccubusSpine), "BgmVol")?.GetValue(boss);
                if (bgm != null && bgmVolObj is float bgmVol)
                    bgm.volume = bgmVol;
            }
            catch
            {
            }

            try
            {
                boss.ero_camerareset();
            }
            catch
            {
            }

            try
            {
                boss.Invoke("fun_DisableWhenOneTarget_reset", 0.05f);
            }
            catch
            {
            }

            // Brief falter so the boss does not instantly re-grab like vanilla eroanime.
            try
            {
                boss.enmTough -= 999f;
                boss.enmMAXfaltertime = 2.2f;
                boss.enmfaltertime = 1f;
            }
            catch
            {
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[SuccubusEscape] Failed to abort H-scene: " + ex.Message);
            return false;
        }

        return true;
    }

    private static bool IsAnySuccubusHSceneVisualActive()
    {
        foreach (SuccubusSpine boss in Object.FindObjectsOfType<SuccubusSpine>())
        {
            if (boss != null && IsBossInActiveHScene(boss))
                return true;
        }

        return false;
    }

    internal static bool IsPlayerInActiveSuccubusHScene(playercon player)
    {
        if (player == null || !player.eroflag)
            return false;

        foreach (SuccubusSpine boss in Object.FindObjectsOfType<SuccubusSpine>())
        {
            if (boss != null && boss.eroflag && boss.com_player == player && IsBossInActiveHScene(boss))
                return true;
        }

        return false;
    }

    internal static bool IsBossInActiveHScene(SuccubusSpine boss)
    {
        if (boss == null)
            return false;

        return boss.eroflag || (boss.erodata != null && boss.erodata.activeSelf);
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

            // Restore player mesh that vanilla hides while eroflag && !_eroflag2.
            try
            {
                MeshRenderer[] renderers = player.GetComponentsInChildren<MeshRenderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null)
                        renderers[i].enabled = true;
                }
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
/// At full SP, NoREroMod's fun_nowdamage prefix is skipped; vanilla still needs downup &gt;= 2.
/// Force the same end state as a successful struggle when Succubus H is active.
/// </summary>
[HarmonyPatch(typeof(playercon), "fun_nowdamage")]
internal static class SuccubusStruggleMaxSpEscapePostfix
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
        if (!SuccubusHSceneEscapePatch.IsPlayerInActiveSuccubusHScene(__instance))
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
        SuccubusHSceneEscapePatch.AbortActiveSuccubusHSceneOnPlayerEscape(__instance, requireErodownClear: true);
    }
}

[HarmonyPatch(typeof(StruggleSystem), nameof(StruggleSystem.startGrabInvul))]
internal static class SuccubusHSceneEscapeStrugglePatch
{
    [HarmonyPostfix]
    private static void OnStruggleEscapeCleanup()
    {
        try
        {
            playercon player = UnifiedPlayerCacheManager.GetPlayer();
            SuccubusHSceneEscapePatch.AbortActiveSuccubusHSceneOnPlayerEscape(
                player,
                requireErodownClear: false);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[SuccubusEscape] Struggle cleanup failed: " + ex.Message);
        }
    }
}

[HarmonyPatch(typeof(playercon), "fun_nowdamage")]
internal static class SuccubusHSceneEscapeFunNowDamagePatch
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

            SuccubusHSceneEscapePatch.AbortActiveSuccubusHSceneOnPlayerEscape(__instance, requireErodownClear: true);
            StruggleSystem.startGrabInvul();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[SuccubusEscape] fun_nowdamage cleanup failed: " + ex.Message);
        }
    }
}

[HarmonyPatch(typeof(playercon), nameof(playercon.ImmediatelyERO))]
internal static class SuccubusHSceneEscapeGiveUpPatch
{
    [HarmonyPostfix]
    private static void OnGiveUpCleanup(playercon __instance)
    {
        try
        {
            SuccubusHSceneEscapePatch.AbortActiveSuccubusHSceneOnPlayerEscape(__instance, requireErodownClear: false);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[SuccubusEscape] GiveUp cleanup failed: " + ex.Message);
        }
    }
}
