using System.Collections;
using System.Reflection;
using HarmonyLib;
using NoREroMod.Systems.Cache;
using NoREroMod.Systems.Effects;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Patches.Enemy;

/// <summary>
/// Vanilla lose path in EvuChurch never reliably reaches a scene load:
/// <see cref="SlaveBigAxeEro"/> only calls <c>fadeevent</c> when Spine event
/// <c>JIGOTOFADE</c> fires with <c>count == 1</c> after increment — shared
/// <c>count</c> from earlier stages often skips that branch, so H keeps looping
/// and <c>EvuChurchSP</c> never loads. Win path uses <see cref="EvBigAxeTalkMng"/>
/// → <c>MoveSceneObjSP</c>. This patch forces the fade + that handoff.
/// </summary>
internal static class SlaveBigAxeIllusiveLoseHandoffPatch
{
    private const string TargetScene = "EvuChurchSP";
    private const float HandoffDelaySeconds = 2.5f;

    private static bool _handoffScheduled;
    private static bool _fadeForced;
    private static Coroutine _routine;
    private static FieldInfo _moveSceneObjSpField;
    private static FieldInfo _countField;
    private static FieldInfo _myspineField;
    private static FieldInfo _fadeimgField;

    [HarmonyPatch(typeof(SlaveBigAxeEro), "OnEvent")]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void OnEvent_ForceJigoToFadeCount(SlaveBigAxeEro __instance, Spine.Event e)
    {
        try
        {
            if (__instance == null || e?.Data == null)
            {
                return;
            }

            if (!SlaveBigAxeIllusiveEventGate.IsChurchBattleLoseHandoffScene())
            {
                return;
            }

            string eventName = e.Data.Name ?? string.Empty;
            if (!eventName.Equals("JIGOTOFADE", System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Vanilla: count++; if (count == 1) fadeevent(). Reset so the first tick always wins.
            EnsureCountField();
            if (_countField != null)
            {
                _countField.SetValue(__instance, 0);
            }

            Plugin.Log?.LogInfo(
                $"[Illusive Lose] JIGOTOFADE seen (idea={SafeIdea()} active={SafeActive()}) — reset count for fadeevent.");
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogWarning($"[Illusive Lose] OnEvent prefix failed: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(SlaveBigAxeEro), "OnEvent")]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void OnEvent_EnsureFadeAndHandoff(SlaveBigAxeEro __instance, Spine.Event e)
    {
        try
        {
            if (__instance == null || !SlaveBigAxeIllusiveEventGate.IsChurchBattleLoseHandoffScene())
            {
                return;
            }

            string eventName = e?.Data?.Name ?? string.Empty;
            string animName = GetAnimName(__instance);
            bool jigoToFade = eventName.Equals("JIGOTOFADE", System.StringComparison.OrdinalIgnoreCase)
                || animName.Equals("JIGOTOFADE", System.StringComparison.OrdinalIgnoreCase);

            if (!jigoToFade)
            {
                return;
            }

            ForceFadeEventIfNeeded(__instance);
            ScheduleHandoff(__instance, "OnEvent:" + eventName + "/" + animName);
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogWarning($"[Illusive Lose] OnEvent postfix failed: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(SlaveBigAxeEro), "fadeevent")]
    [HarmonyPostfix]
    private static void FadeEvent_Postfix(SlaveBigAxeEro __instance)
    {
        try
        {
            if (__instance == null || !SlaveBigAxeIllusiveEventGate.IsChurchBattleLoseHandoffScene())
            {
                Plugin.Log?.LogInfo(
                    $"[Illusive Lose] fadeevent fired but handoff gate=false (idea={SafeIdea()} active={SafeActive()}).");
                return;
            }

            _fadeForced = true;
            ScheduleHandoff(__instance, "fadeevent");
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogWarning($"[Illusive Lose] fadeevent postfix failed: {ex.Message}");
        }
    }

    private static void ForceFadeEventIfNeeded(SlaveBigAxeEro ero)
    {
        if (_fadeForced || ero == null)
        {
            return;
        }

        try
        {
            EnsureFadeimgField();
            fadein_out fade = _fadeimgField?.GetValue(ero) as fadein_out;
            if (fade == null)
            {
                GameObject ui = GameObject.Find("UIeffect");
                fade = ui != null ? ui.GetComponent<fadein_out>() : null;
                if (fade != null && _fadeimgField != null)
                {
                    _fadeimgField.SetValue(ero, fade);
                }
            }

            // Call vanilla fadeevent so canvascreate Invoke still runs.
            MethodInfo fadeEvent = AccessTools.Method(typeof(SlaveBigAxeEro), "fadeevent");
            fadeEvent?.Invoke(ero, null);
            _fadeForced = true;
            Plugin.Log?.LogInfo("[Illusive Lose] Forced fadeevent() after JIGOTOFADE.");
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogWarning($"[Illusive Lose] Force fadeevent failed: {ex.Message}");
        }
    }

    private static void ScheduleHandoff(SlaveBigAxeEro ero, string reason)
    {
        if (_handoffScheduled || ero == null)
        {
            return;
        }

        _handoffScheduled = true;
        if (_routine != null)
        {
            try { ero.StopCoroutine(_routine); } catch { }
        }

        _routine = ero.StartCoroutine(HandoffAfterFade(ero));
        Plugin.Log?.LogInfo($"[Illusive Lose] Scheduled EvuChurch → EvuChurchSP ({reason}).");
    }

    private static IEnumerator HandoffAfterFade(SlaveBigAxeEro ero)
    {
        yield return new WaitForSecondsRealtime(HandoffDelaySeconds);

        try
        {
            HSceneBlackBackgroundSystem.Deactivate();
        }
        catch
        {
        }

        if (!SlaveBigAxeIllusiveEventGate.IsChurchBattleLoseHandoffScene())
        {
            Plugin.Log?.LogWarning(
                $"[Illusive Lose] Handoff aborted — no longer battle scene (idea={SafeIdea()} active={SafeActive()}).");
            _handoffScheduled = false;
            _routine = null;
            yield break;
        }

        PreparePlayerForSceneMove();

        if (TrySceneMoveViaTalkManager())
        {
            Plugin.Log?.LogInfo("[Illusive Lose] MoveSceneObjSP.SceneMOVE() → EvuChurchSP.");
            _routine = null;
            yield break;
        }

        Plugin.Log?.LogWarning("[Illusive Lose] MoveSceneObjSP missing — fallback LoadSceneAndWait(EvuChurchSP).");
        yield return LoadEvuChurchSpFallback();
        _routine = null;
    }

    private static void PreparePlayerForSceneMove()
    {
        try
        {
            playercon player = UnifiedPlayerCacheManager.GetPlayer();
            PlayerStatus status = UnifiedPlayerCacheManager.GetPlayerStatus();
            if (player != null)
            {
                player.eroflag = false;
                if (player.rigi2d != null)
                {
                    player.rigi2d.simulated = false;
                }

                player.state = "IDLE";
                player.nowdamage = false;
            }

            if (status != null)
            {
                status._SOUSA = false;
                status._SOUSAMNG = false;
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogWarning($"[Illusive Lose] PreparePlayer failed: {ex.Message}");
        }
    }

    private static bool TrySceneMoveViaTalkManager()
    {
        try
        {
            EvBigAxeTalkMng talk = Object.FindObjectOfType<EvBigAxeTalkMng>();
            if (talk == null)
            {
                return false;
            }

            _moveSceneObjSpField ??= AccessTools.Field(typeof(EvBigAxeTalkMng), "MoveSceneObjSP");
            GameObject moveSp = _moveSceneObjSpField?.GetValue(talk) as GameObject;
            if (moveSp == null)
            {
                return false;
            }

            moveSp.SetActive(true);
            SceneMove sceneMove = moveSp.GetComponent<SceneMove>();
            if (sceneMove == null)
            {
                return false;
            }

            sceneMove.SceneMOVE();
            return true;
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogWarning($"[Illusive Lose] TalkMng SceneMOVE failed: {ex.Message}");
            return false;
        }
    }

    private static IEnumerator LoadEvuChurchSpFallback()
    {
        PlayerStatus pl = UnifiedPlayerCacheManager.GetPlayerStatus();
        if (pl == null)
        {
            _handoffScheduled = false;
            yield break;
        }

        try
        {
            StaticMng.Idea_Nowscene = TargetScene;
            StaticMng.MovePosBool = false;
        }
        catch
        {
        }

        yield return pl.StartCoroutine(pl.LoadSceneAndWait("Common", TargetScene));
    }

    private static string GetAnimName(SlaveBigAxeEro ero)
    {
        try
        {
            EnsureMyspineField();
            SkeletonAnimation spine = _myspineField?.GetValue(ero) as SkeletonAnimation;
            return spine?.AnimationName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void EnsureCountField()
    {
        _countField ??= AccessTools.Field(typeof(SlaveBigAxeEro), "count");
    }

    private static void EnsureMyspineField()
    {
        _myspineField ??= AccessTools.Field(typeof(SlaveBigAxeEro), "myspine")
            ?? AccessTools.Field(typeof(SlaveBigAxeEro), "mySpine");
    }

    private static void EnsureFadeimgField()
    {
        _fadeimgField ??= AccessTools.Field(typeof(SlaveBigAxeEro), "fadeimg");
    }

    private static string SafeIdea()
    {
        try { return StaticMng.Idea_Nowscene ?? ""; } catch { return ""; }
    }

    private static string SafeActive()
    {
        try { return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name ?? ""; } catch { return ""; }
    }

    /// <summary>Reset latch on scene change so a later replay can hand off again.</summary>
    internal static void ResetHandoffLatch()
    {
        _handoffScheduled = false;
        _fadeForced = false;
        _routine = null;
    }
}
