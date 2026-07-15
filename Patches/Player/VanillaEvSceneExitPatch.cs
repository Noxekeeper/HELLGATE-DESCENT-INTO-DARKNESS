using System.Collections;
using HarmonyLib;
using NoREroMod.Systems.Cache;
using NoREroMod.Systems.Spawn;
using UnityEngine;

namespace NoREroMod.Patches.Player;

/// <summary>
/// Take Vengeance must load the altar map when the death zone differs from the altar
/// that owns <c>_checkpoint</c> (see <see cref="VanillaAltarCatalog"/>).
/// Vanilla REstrat only calls savepoint() in the current zone while checkpoint targets
/// another map → void fall.
/// </summary>
internal static class VanillaEvSceneExitPatch
{
    // Search LogOutput.log for this tag after the vengeance bar to confirm the fix ran.
    private const string TraceTag = "[BAR EXIT TRACE]";

    private static readonly Vector2 ScapegoatAltarPos = new Vector2(216f, 55f);

    private static bool _exitScheduled;
    private static bool _allowAltarSceneLoad;

    internal static bool IsAllowingAltarSceneLoad => _allowAltarSceneLoad;

    internal static void LogTraceReady()
    {
    }

    private static void LogTrace(string message)
    {
    }

    private static bool IsTraceSource(string source)
    {
        return source.Contains("Lvreset")
            || source.Contains("REstrat")
            || source.Contains("Restart")
            || source.Contains("Allend");
    }

    private struct AltarTeleportTarget
    {
        internal string SceneName;
        internal Vector2 Position;
        internal string SavepointMethod;
    }

    /// <summary>
    /// Deterministic entry for the lethal-trap "Take Vengeance" path, which revives in-place via
    /// REgame.pl_REstrat / playercon.Death_flag and never hits PlayerStatus.REstrat / playercon.Restart
    /// (so the REstrat/Restart prefixes below never fire). Called from the pl_REstrat postfix when a
    /// cross-zone death is latched; forces a real scene load to the altar home.
    /// </summary>
    internal static void RequestNoAltarZoneAltarExit()
    {
        if (!VanillaCutsceneSceneGuard.IsNoAltarZoneDeathPending())
            return;

        TryExitToAltar("NoAltarZoneDeathVengeance");
    }

    private static void TryExitToAltar(string source)
    {
        if (VanillaCutsceneSceneGuard.IsBlockingRespawnRedirectForActiveHSceneDeath())
        {
            if (IsTraceSource(source))
            {
                LogTrace($"SKIP {source}: H-scene / fatality playback still active.");
            }

            return;
        }

        if (!VanillaCutsceneSceneGuard.ShouldForceAltarSceneLoad())
        {
            if (IsTraceSource(source))
            {
                LogTrace(
                    $"SKIP {source}: ShouldForceAltarSceneLoad=false (zone=\"{VanillaCutsceneSceneGuard.GetActiveZoneName()}\").");
            }

            return;
        }

        if (_exitScheduled)
        {
            if (IsTraceSource(source))
                LogTrace($"SKIP {source}: exit already scheduled.");

            return;
        }

        PlayerStatus status = UnifiedPlayerCacheManager.GetPlayerStatus();
        if (status == null)
        {
            LogTrace($"SKIP {source}: PlayerStatus missing.");
            return;
        }

        _exitScheduled = true;
        string zone = VanillaCutsceneSceneGuard.GetActiveZoneName();
        game_fragmng frag = UnifiedGameControllerCacheManager.GetGameFragMng();
        string checkpointScene = frag?._re_Scenename ?? "?";
        string altarHome = VanillaAltarCatalog.ResolveAltarHomeScene(frag) ?? "?";
        Vector2 checkpointPos = frag?._checkpoint ?? Vector2.zero;
        LogTrace(
            $"START {source} | zone=\"{zone}\" checkpointScene=\"{checkpointScene}\" altarHome=\"{altarHome}\" "
            + $"checkpointPos=({checkpointPos.x:F1},{checkpointPos.y:F1}) "
            + $"mismatch={VanillaCutsceneSceneGuard.IsCheckpointSceneMismatch()}");
        Plugin.Log?.LogInfo(
            $"[EV SCENE EXIT] {source} → altar load (zone=\"{zone}\", checkpointScene=\"{checkpointScene}\", "
            + $"altarHome=\"{altarHome}\", mismatch={VanillaCutsceneSceneGuard.IsCheckpointSceneMismatch()}).");

        if (VanillaCutsceneSceneGuard.IsAdditiveBossEvSceneActive()
            && !VanillaCutsceneSceneGuard.IsInsomniaBarInlineEvActive()
            && !VanillaCutsceneSceneGuard.IsCheckpointSceneMismatch())
        {
            Plugin.Log?.LogInfo("[EV SCENE EXIT] Additive GoInsomnia only → vanilla REstrat().");
            _exitScheduled = false;
            status.REstrat();
            VanillaCutsceneSceneGuard.ClearPendingBarAltarExit();
            return;
        }

        if (frag == null)
        {
            Plugin.Log?.LogWarning("[EV SCENE EXIT] game_fragmng missing.");
            _exitScheduled = false;
            return;
        }

        AltarTeleportTarget target = ResolveAltarTarget(frag);
        LogTrace(
            $"TARGET scene=\"{target.SceneName}\" pos=({target.Position.x:F1},{target.Position.y:F1}) savepoint=\"{target.SavepointMethod}\"");
        VanillaCutsceneSceneGuard.MarkAltarExitInProgress();
        Plugin.Instance.StartCoroutine(AltarLoadRoutine(status, frag, target, source));
    }

    private static AltarTeleportTarget ResolveAltarTarget(game_fragmng frag)
    {
        string scene = VanillaAltarCatalog.ResolveAltarHomeScene(frag);
        Vector2 pos = frag._checkpoint;
        string savepoint = frag._re_savepoint;

        // Insomnia bar / EV gameplay has no usable local altar for Take Vengeance — fall back
        // to scapegoatEntrance. Do NOT treat InundergroundChurch as "no altar": it has
        // savepoint_InUnder / savepoint_InUnder_over; redirecting those coords onto
        // scapegoatEntrance causes void fall (regression 2026-07-14).
        bool forceScapegoatFallback =
            string.IsNullOrEmpty(scene)
            || VanillaCutsceneSceneGuard.IsInsomniaBarZoneName(scene)
            || (VanillaCutsceneSceneGuard.IsInsomniaEvArea(scene)
                && !VanillaAltarCatalog.SceneHasVanillaAltar(scene));

        if (forceScapegoatFallback)
        {
            scene = "scapegoatEntrance";
            if (string.IsNullOrEmpty(savepoint)
                || !VanillaAltarCatalog.TryGetSceneForSavepoint(savepoint, out string saveScene)
                || !saveScene.Equals("scapegoatEntrance", System.StringComparison.OrdinalIgnoreCase))
            {
                savepoint = "savepoint_scape";
            }
        }

        // Keep stored checkpoint position (last activated altar). Hardcoded default only when
        // no checkpoint was ever stored.
        if (pos == Vector2.zero)
            pos = ScapegoatAltarPos;

        if (string.IsNullOrEmpty(savepoint))
        {
            if (VanillaAltarCatalog.TryGetSceneForCheckpointCoords(pos, out _, out string matchedSave))
                savepoint = matchedSave;
            else
                savepoint = "savepoint_scape";
        }

        if (string.IsNullOrEmpty(scene))
            scene = "scapegoatEntrance";

        return new AltarTeleportTarget
        {
            SceneName = scene,
            Position = pos,
            SavepointMethod = savepoint,
        };
    }

    private static void PrepareFragForAltarLoad(game_fragmng frag, AltarTeleportTarget target)
    {
        StaticMng.MovePosBool = true;
        StaticMng.StartPos = target.Position;
        frag._checkpoint = target.Position;
        frag._re_savepoint = target.SavepointMethod;
        frag._re_Scenename = target.SceneName;
        StaticMng.Idea_Nowscene = target.SceneName;

        try { StaticMng.Re_Scenename = target.SceneName; }
        catch { }
    }

    private static IEnumerator AltarLoadRoutine(PlayerStatus pl, game_fragmng frag, AltarTeleportTarget target, string source)
    {
        pl._SOUSA = false;
        pl._SOUSAMNG = false;

        try
        {
            fadein_out fade = GameObject.Find("Canvas").GetComponent<fadein_out>();
            if (fade != null)
                fade.on();
        }
        catch { }

        PrepareFragForAltarLoad(frag, target);

        _allowAltarSceneLoad = true;
        LogTrace($"LOAD Common + {target.SceneName} (from {source})");
        Plugin.Log?.LogInfo($"[EV SCENE EXIT] LoadSceneAndWait(Common, {target.SceneName}).");

        yield return pl.StartCoroutine(pl.LoadSceneAndWait("Common", target.SceneName));

        for (int i = 0; i < 5; i++)
            yield return null;

        ApplyPlayerAtAltar(pl, frag, target, source);
        SpawnRespawnAfterAltarPatch.RunHellGateRespawnAfterVanillaAltarReset();
        LogTrace(
            $"OK bar exit complete | landed scene=\"{target.SceneName}\" pos=({target.Position.x:F1},{target.Position.y:F1}) via {source}");

        _allowAltarSceneLoad = false;
        _exitScheduled = false;
        VanillaCutsceneSceneGuard.ClearPendingBarAltarExit();
    }

    private static void ApplyPlayerAtAltar(PlayerStatus pl, game_fragmng frag, AltarTeleportTarget target, string source)
    {
        playercon player = UnifiedPlayerCacheManager.GetPlayer();
        if (player == null)
            return;

        player.eroflag = false;
        player._eroflag2 = false;
        player.erodown = 0;
        player.state = "IDLE";
        player.nowdamage = false;

        if (player.rigi2d != null)
            player.rigi2d.simulated = true;

        player.transform.position = new Vector3(target.Position.x, target.Position.y, player.transform.position.z);
        frag._checkpoint = target.Position;
        StaticMng.MovePosBool = false;

        try { frag.aa(target.SavepointMethod); }
        catch { }

        player.fun_cameramove();
        pl._SOUSA = true;
        pl._SOUSAMNG = true;

        LogTrace(
            $"PLACE player at ({target.Position.x:F1},{target.Position.y:F1}) scene=\"{target.SceneName}\" ({source})");
        Plugin.Log?.LogInfo(
            $"[EV SCENE EXIT] Player placed at ({target.Position.x:F1},{target.Position.y:F1}) scene=\"{target.SceneName}\".");
    }

    [HarmonyPatch(typeof(Lvreset), nameof(Lvreset.ResetStatus))]
    internal static class LvresetResetStatusPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            VanillaCutsceneSceneGuard.MarkVendettaAltarExitPending();
            LogTrace(
                $"ARM Lvreset.ResetStatus (месть) zone=\"{VanillaCutsceneSceneGuard.GetActiveZoneName()}\"");
            Plugin.Log?.LogInfo("[EV SCENE EXIT] Lvreset.ResetStatus — vendetta altar exit armed.");
        }
    }

    [HarmonyPatch(typeof(Lvreset), "ResetStatusend")]
    internal static class LvresetResetStatusendPatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            if (!VanillaCutsceneSceneGuard.ShouldForceAltarSceneLoad())
            {
                LogTrace("SKIP Lvreset.ResetStatusend: guard false — vanilla dialog reopen.");
                return true;
            }

            TryExitToAltar("Lvreset.ResetStatusend");
            return false;
        }
    }

    private static void LogPrefixEntry(string method)
    {
        LogTrace(
            $"PREFIX {method} | zone=\"{VanillaCutsceneSceneGuard.GetActiveZoneName()}\" "
            + $"mismatch={VanillaCutsceneSceneGuard.IsCheckpointSceneMismatch()} "
            + $"forceAltar={VanillaCutsceneSceneGuard.ShouldForceAltarSceneLoad()} "
            + $"noAltarDeath={VanillaCutsceneSceneGuard.IsNoAltarZoneDeathPending()}");
    }

    [HarmonyPatch(typeof(PlayerStatus), nameof(PlayerStatus.REstrat))]
    internal static class REstratPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix()
        {
            LogPrefixEntry("PlayerStatus.REstrat");

            if (!VanillaCutsceneSceneGuard.ShouldForceAltarSceneLoad())
                return true;

            if (VanillaCutsceneSceneGuard.IsAdditiveBossEvSceneActive()
                && !VanillaCutsceneSceneGuard.IsInsomniaBarInlineEvActive()
                && !VanillaCutsceneSceneGuard.IsCheckpointSceneMismatch())
                return true;

            TryExitToAltar("PlayerStatus.REstrat");
            return false;
        }
    }

    /// <summary>
    /// Blocks vanilla Restart() savepoint-in-place when active zone != altar scene (void fall in Ragdum / InundergroundChurch).
    /// </summary>
    [HarmonyPatch(typeof(PlayerStatus), "REstrat_invoke")]
    internal static class REstratInvokePatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            LogPrefixEntry("PlayerStatus.REstrat_invoke");

            if (!VanillaCutsceneSceneGuard.IsCheckpointSceneMismatch())
                return true;

            TryExitToAltar("PlayerStatus.REstrat_invoke");
            return false;
        }
    }

    [HarmonyPatch(typeof(playercon), nameof(playercon.Restart))]
    internal static class PlayerRestartMismatchPatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            LogPrefixEntry("playercon.Restart");

            if (!VanillaCutsceneSceneGuard.IsCheckpointSceneMismatch())
                return true;

            TryExitToAltar("playercon.Restart");
            return false;
        }
    }

    [HarmonyPatch(typeof(playercon), nameof(playercon.RestartSceneMove))]
    internal static class PlayerRestartSceneMoveMismatchPatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            LogPrefixEntry("playercon.RestartSceneMove");

            if (!VanillaCutsceneSceneGuard.IsCheckpointSceneMismatch())
                return true;

            TryExitToAltar("playercon.RestartSceneMove");
            return false;
        }
    }

    [HarmonyPatch(typeof(VendettaDialogtextcontrol_second), "Allend")]
    internal static class VendettaDialogAllendPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            TryExitToAltar("VendettaDialogtextcontrol_second.Allend");
        }
    }

    [HarmonyPatch(typeof(SceneMove), nameof(SceneMove.SceneMOVE))]
    internal static class SceneMoveDuringEvPatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            if (!VanillaCutsceneSceneGuard.IsInsomniaBarInlineEvActive())
                return true;

            TryExitToAltar("SceneMove.SceneMOVE");
            return false;
        }
    }

    [HarmonyPatch(typeof(EvBarEV1_1TalkMng), "SceneMove")]
    internal static class EvBar11SceneMoveBlockPatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            if (!VanillaCutsceneSceneGuard.IsInsomniaBarInlineEvActive())
                return true;

            Plugin.Log?.LogInfo("[EV SCENE EXIT] EvBarEV1_1TalkMng.SceneMove blocked.");
            return false;
        }
    }

    [HarmonyPatch(typeof(EvBarEV1_1TalkMng), "SceneMove2")]
    internal static class EvBar11SceneMove2BlockPatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            if (!VanillaCutsceneSceneGuard.IsInsomniaBarInlineEvActive())
                return true;

            Plugin.Log?.LogInfo("[EV SCENE EXIT] EvBarEV1_1TalkMng.SceneMove2 blocked.");
            return false;
        }
    }

    [HarmonyPatch(typeof(EvBarEV1_2TalkMng), "SceneMove")]
    internal static class EvBar12SceneMoveBlockPatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            if (!VanillaCutsceneSceneGuard.IsInsomniaBarInlineEvActive())
                return true;

            Plugin.Log?.LogInfo("[EV SCENE EXIT] EvBarEV1_2TalkMng.SceneMove blocked.");
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerStatus), nameof(PlayerStatus.LoadSceneAndWait))]
    internal static class LoadSceneAndWaitDuringEvPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(string a, string b)
        {
            if (_allowAltarSceneLoad)
                return true;

            if (!VanillaCutsceneSceneGuard.IsInsomniaBarInlineEvActive())
                return true;

            TryExitToAltar($"PlayerStatus.LoadSceneAndWait({a}, {b})");
            return false;
        }
    }
}

