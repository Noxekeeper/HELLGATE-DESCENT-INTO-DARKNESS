using System;
using DarkTonic.MasterAudio;
using HarmonyLib;
using NoREroMod.Patches.Enemy.BossTouzokuCustom;
using NoREroMod.Patches.Player;
using NoREroMod.Systems.Cache;
using Spine.Unity;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoREroMod.Patches.Enemy.HellishTouzokuModCustom;

/// <summary>
/// Hellish Touzoku use disk-loaded Spine assets. Vanilla struggle escape can clear player erodown
/// while enemy eroflag/erodata stay active (same class of bug as Wolf / MummyDogPassPatch).
/// </summary>
internal static class HellishTouzokuHSceneEscapePatch
{
    internal const string NameToken = "HellishTouzoku";

    private static bool IsHellishTouzoku(EnemyDate enemy)
    {
        if (enemy == null || enemy.gameObject == null)
            return false;

        string name = enemy.gameObject.name;
        return !string.IsNullOrEmpty(name)
               && name.IndexOf(NameToken, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    internal static void AbortActiveHellishTouzokuHSceneOnPlayerEscape(playercon player, bool requireErodownClear)
    {
        if (player == null)
            return;

        if (requireErodownClear && player.erodown != 0)
            return;

        if (!IsAnyHellishTouzokuHSceneVisualActive())
            return;

        bool abortedAny = false;

        foreach (TouzokuNormal touzoku in Object.FindObjectsOfType<TouzokuNormal>())
        {
            if (touzoku != null && TryAbortSingle(touzoku))
                abortedAny = true;
        }

        foreach (TouzokuAxe axe in Object.FindObjectsOfType<TouzokuAxe>())
        {
            if (axe != null && TryAbortSingle(axe))
                abortedAny = true;
        }

        foreach (BossTouzoku boss in Object.FindObjectsOfType<BossTouzoku>())
        {
            if (boss != null && TryAbortSingle(boss))
                abortedAny = true;
        }

        if (!abortedAny)
            return;

        ClearPlayerHSceneFlags(player);
        CancelPendingHandoff();
        TouzokuNormalPassPatch.ResetAll();
        TouzokuAxePassPatch.ResetAll();
        PlayerCombatControlRecovery.RestoreAfterStruggleEscape();
    }

    private static bool IsAnyHellishTouzokuHSceneVisualActive()
    {
        foreach (TouzokuNormal touzoku in Object.FindObjectsOfType<TouzokuNormal>())
        {
            if (IsHellishTouzoku(touzoku) && (touzoku.eroflag || (touzoku.erodata != null && touzoku.erodata.activeSelf)))
                return true;
        }

        foreach (TouzokuAxe axe in Object.FindObjectsOfType<TouzokuAxe>())
        {
            if (IsHellishTouzoku(axe) && (axe.eroflag || (axe.erodata != null && axe.erodata.activeSelf)))
                return true;
        }

        foreach (BossTouzoku boss in Object.FindObjectsOfType<BossTouzoku>())
        {
            if (IsHellishTouzoku(boss) && (boss.eroflag || (boss.erodata != null && boss.erodata.activeSelf)))
                return true;
        }

        return false;
    }

    private static bool TryAbortSingle(EnemyDate enemy)
    {
        if (!IsHellishTouzoku(enemy))
            return false;

        bool eroActive = enemy.erodata != null && enemy.erodata.activeSelf;
        if (!enemy.eroflag && !eroActive)
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

            if (enemy is BossTouzoku customBoss
                && BossTouzokuCustomStats.IsCustom(customBoss)
                && customBoss.com_player != null
                && customBoss.com_player.erodown == 0)
            {
                BossTouzokuCustomRuntime.RunSafeEroAnime(customBoss);
                return true;
            }

            SkeletonAnimation eroSpine = enemy.erodata != null
                ? enemy.erodata.GetComponent<SkeletonAnimation>()
                : null;
            eroSpine?.AnimationState?.ClearTracks();

            if (eroActive)
                enemy.erodata.SetActive(false);

            enemy.eroflag = false;

            MeshRenderer meshRenderer = enemy.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
                meshRenderer.enabled = true;

            Rigidbody2D rigidBody = AccessTools.Field(typeof(EnemyDate), "rigi2D")?.GetValue(enemy) as Rigidbody2D;
            if (rigidBody != null && !rigidBody.simulated)
                rigidBody.simulated = true;

            if (enemy is BossTouzoku boss && BossTouzokuCustomStats.IsCustom(boss))
            {
                BossTouzokuCustomRuntime.ForceSpineMeshRefresh(boss);
                BossTouzokuCustomRuntime.EnsureVisible(boss);
            }
            else
            {
                try
                {
                    enemy.ero_camerareset();
                }
                catch
                {
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[HellishTouzokuEscape] Failed to abort H-scene: " + ex.Message);
            return false;
        }

        return true;
    }

    private static void ClearPlayerHSceneFlags(playercon player)
    {
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

[HarmonyPatch(typeof(StruggleSystem), nameof(StruggleSystem.startGrabInvul))]
internal static class HellishTouzokuHSceneEscapeStrugglePatch
{
    [HarmonyPostfix]
    private static void OnStruggleEscapeCleanup()
    {
        try
        {
            playercon player = UnifiedPlayerCacheManager.GetPlayer();
            HellishTouzokuHSceneEscapePatch.AbortActiveHellishTouzokuHSceneOnPlayerEscape(
                player,
                requireErodownClear: false);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[HellishTouzokuEscape] Struggle cleanup failed: " + ex.Message);
        }
    }
}

[HarmonyPatch(typeof(playercon), "fun_nowdamage")]
internal static class HellishTouzokuHSceneEscapeFunNowDamagePatch
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

            HellishTouzokuHSceneEscapePatch.AbortActiveHellishTouzokuHSceneOnPlayerEscape(
                __instance,
                requireErodownClear: true);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[HellishTouzokuEscape] fun_nowdamage cleanup failed: " + ex.Message);
        }
    }
}

[HarmonyPatch(typeof(playercon), nameof(playercon.ImmediatelyERO))]
internal static class HellishTouzokuHSceneEscapeGiveUpPatch
{
    [HarmonyPostfix]
    private static void OnGiveUpCleanup(playercon __instance)
    {
        try
        {
            HellishTouzokuHSceneEscapePatch.AbortActiveHellishTouzokuHSceneOnPlayerEscape(
                __instance,
                requireErodownClear: false);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[HellishTouzokuEscape] GiveUp cleanup failed: " + ex.Message);
        }
    }
}
