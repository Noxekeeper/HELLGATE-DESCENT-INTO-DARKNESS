using System;
using System.Reflection;
using HarmonyLib;
using NoREroMod.Systems.Cache;
using NoREroMod.Systems.EventCore.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NoREroMod.Systems.UI;

/// <summary>
/// Single source of truth for "should our gameplay HUD widgets be visible right now?".
///
/// <para>
/// We mirror the vanilla <see cref="UImng"/> HUD (HP/MP/SP bar, equip icons, exp).
/// When the game disables that HUD — title screen, loading transitions, BadEnd,
/// some cutscenes and H-scenes — our modded widgets hide along with it. When the
/// vanilla HUD comes back, ours reappear in the same frame.
/// </para>
///
/// Consumers:
///  - <c>RageUISystem</c> (Rage label + GrabChance sub-label on the same canvas).
///  - <c>MindBrokenUIPatch</c> (with a small override for black-background H-scenes).
///  - <c>FactionReputationHud</c> (combined with the manual H toggle).
///  - <c>GoldHud</c> uses <see cref="IsNpcDiaryReaderOpen"/> only (gold stays visible in the
///    status/inventory menu, but hides while an NPC diary reader is open).
/// </summary>
internal static class HudVisibilityGate
{
    private static UImng _cachedUImng;
    private static FieldInfo _menuCanvasField;
    private static FieldInfo _operationField;
    private static bool _diaryReaderOpenCached;
    private static float _lastDiaryLookupTime = -999f;
    // Scene.handle isn't available in this Unity version — buildIndex + name is
    // plenty to detect a scene change.
    private static int _lastSceneBuildIndex = int.MinValue;
    private static string _lastSceneName;
    private static float _lastLookupTime = -999f;
    // Cheap enough — a few frames of staleness are acceptable and FindObjectOfType is costly.
    private const float LookupIntervalSeconds = 0.2f;

    /// <summary>
    /// True when the vanilla gameplay HUD is alive and rendering.
    /// False in <c>Gametitle</c>, during scene transitions, BadEnd, or any state where
    /// the game itself has hidden the HP/MP bar.
    /// </summary>
    public static bool ShouldShowGameplayHud()
    {
        try
        {
            // Quick rejection: main menu never has UImng, avoid the scan altogether.
            Scene active = SceneManager.GetActiveScene();
            if (string.Equals(active.name, "Gametitle", StringComparison.OrdinalIgnoreCase))
                return false;

            UImng ui = GetCachedUImng(active);
            if (ui == null)
                return false;

            // isActiveAndEnabled already covers Behaviour.enabled and the whole
            // parent chain being active — matches exactly when vanilla is drawing HP bar.
            if (!ui.isActiveAndEnabled)
                return false;

            if (EventCorePause.IsFrozen)
                return false;

            if (IsVanillaStatusMenuOpen())
                return false;

            // NPC diary pickup / library reader (UIDialymng → UIDialyClass): vanilla keeps the
            // root "Canvas" enabled, so the ancestor walk above does not fire. Our overlay HUD
            // (sortingOrder 850–1000) would otherwise draw on top of diary CG pages.
            if (IsNpcDiaryReaderOpen())
                return false;

            GameObject go = ui.gameObject;
            if (go == null || !go.activeInHierarchy)
                return false;

            // Vanilla CG / nightmare / dialogue cutscenes hide the HUD by doing
            //   GameObject.Find("Canvas").GetComponent<Canvas>().enabled = false;
            // on the root UI canvas (see TextControllerGO, TalkStartMng, *ERO mng, etc).
            // This leaves UImng's gameObject active in the hierarchy, so we also need
            // to respect every parent Canvas' .enabled flag. If ANY ancestor Canvas is
            // disabled the vanilla HUD is invisible — ours should follow.
            Transform t = ui.transform;
            while (t != null)
            {
                Canvas canvas = t.GetComponent<Canvas>();
                if (canvas != null && !canvas.enabled)
                    return false;
                t = t.parent;
            }
            return true;
        }
        catch
        {
            // Fail-safe: hide our HUD rather than risk ghost widgets over a cutscene.
            return false;
        }
    }

    /// <summary>
    /// Invalidate the cached reference — callers (scene loaders, tests) can force a refresh.
    /// </summary>
    public static void Invalidate()
    {
        _cachedUImng = null;
        _lastSceneBuildIndex = int.MinValue;
        _lastSceneName = null;
        _lastLookupTime = -999f;
        _lastDiaryLookupTime = -999f;
    }

    /// <summary>
    /// True while the player is paging through a vanilla NPC diary
    /// (<see cref="UIDialyClass"/> world pickup or <see cref="UIListDialyClass"/> at the base).
    /// </summary>
    internal static bool IsNpcDiaryReaderOpen()
    {
        try
        {
            Scene active = SceneManager.GetActiveScene();
            if (string.Equals(active.name, "Gametitle", StringComparison.OrdinalIgnoreCase))
                return false;

            float now = Time.unscaledTime;
            if (now - _lastDiaryLookupTime < LookupIntervalSeconds)
                return _diaryReaderOpenCached;

            _lastDiaryLookupTime = now;
            _diaryReaderOpenCached =
                UnityEngine.Object.FindObjectOfType<UIDialyClass>() != null ||
                UnityEngine.Object.FindObjectOfType<UIListDialyClass>() != null;
            return _diaryReaderOpenCached;
        }
        catch
        {
            return false;
        }
    }

    private static UImng GetCachedUImng(Scene activeScene)
    {
        // New scene → drop the cached reference from the previous scene's hierarchy.
        if (activeScene.buildIndex != _lastSceneBuildIndex ||
            !string.Equals(activeScene.name, _lastSceneName, StringComparison.Ordinal))
        {
            _cachedUImng = null;
            _lastSceneBuildIndex = activeScene.buildIndex;
            _lastSceneName = activeScene.name;
            _lastDiaryLookupTime = -999f;
            _diaryReaderOpenCached = false;
        }

        // Cached object got destroyed (happens between scene loads) — clear it.
        if (_cachedUImng == null)
        {
            _lastLookupTime = -999f;
        }

        float now = Time.unscaledTime;
        if (_cachedUImng != null && (now - _lastLookupTime) < LookupIntervalSeconds)
            return _cachedUImng;

        // Refresh at most ~5x/sec; FindObjectOfType is expensive if called every frame.
        _cachedUImng = UnityEngine.Object.FindObjectOfType<UImng>();
        _lastLookupTime = now;
        return _cachedUImng;
    }

    /// <summary>
    /// Vanilla status / inventory menu (Menu key): <see cref="PlayerStatus.MENU"/> canvas enabled,
    /// <c>operation = false</c>, <c>Time.timeScale = 0</c>. HP/MP/SP bars are covered; mod HUD must hide too.
    /// </summary>
    private static bool IsVanillaStatusMenuOpen()
    {
        try
        {
            // PlayerStatus lives on GameController (singleton), not on the Player tag object.
            PlayerStatus status = UnifiedGameControllerCacheManager.GetPlayerStatus();
            if (status == null)
                return false;

            _menuCanvasField ??= AccessTools.Field(typeof(PlayerStatus), "MENU");
            if (_menuCanvasField?.GetValue(status) is Canvas menu && menu.enabled)
                return true;

            // Backup: menu / BadEnd fully freeze time (timeScale == 0).
            // Do NOT use a loose "< 0.5" threshold — Vengeance Strike sets operation=false
            // and timeScale≈0.1 during Stab_fun, which falsely hid MindBroken / Pregnancy HUD.
            _operationField ??= AccessTools.Field(typeof(PlayerStatus), "operation");
            if (_operationField != null && _operationField.GetValue(status) is bool operation && !operation)
            {
                if (Time.timeScale <= 0.001f)
                    return true;
            }
        }
        catch
        {
            // fall through
        }

        return false;
    }
}
