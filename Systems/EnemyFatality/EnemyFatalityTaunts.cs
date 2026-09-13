using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using NoREroMod.Systems.EventCore.Core;
using NoREroMod.Systems.UI;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NoREroMod.Systems.EnemyFatality;

/// <summary>
/// Post-fatality taunts from HellGateJson/EnemyFatality/&lt;Lang&gt;/.
/// Resolution order:
/// 1) Clip override: phrases_&lt;clipFolder&gt;.json (e.g. phrases_lost_head.json)
/// 2) Profile override: phrases_&lt;ProfileId&gt;.json (e.g. phrases_CrawlingCreatures.json)
/// 3) Shared pool: phrases.json
/// Mute list: HellGateJson/EnemyFatality/_shared/settings.json.
/// After the PNG last frame + delay, shows a screen-centered line
/// (center Y + 120) until respawn cleanup.
/// </summary>
internal static class EnemyFatalityTaunts
{
    private const string RootFolderName = "EnemyFatality";
    private const string SharedFolderName = "_shared";
    private const string SettingsFileName = "settings.json";
    private const string PhrasesFileName = "phrases.json";
    private const string OverlayRootName = "HellGate_EnemyFatalityTauntOverlay";

    /// <summary>
    /// Screen-center Y offset in UI px: above "YOU'VE BEEN DEFEATED",
    /// below the raised Fatality icon.
    /// </summary>
    private const float ScreenCenterOffsetY = 120f;

    /// <summary>
    /// Clip folders that ship their own phrase files and must NOT fall back to
    /// shared <c>phrases.json</c> / leg-bisect lines when empty.
    /// StillAlive is excluded — it uses profile <c>phrases_CrawlingCreatures.json</c>.
    /// </summary>
    private static readonly string[] DedicatedClipPhraseKeys =
    {
        "lost_head",
        "HeavyCritical",
    };

    private static readonly Color TauntColor = new Color(0.92f, 0.12f, 0.18f, 1f);
    private static readonly Color TauntOutline = new Color(0f, 0f, 0f, 1f);

    private static MonoBehaviour _host;
    private static Coroutine _pending;
    private static GameObject _overlayRoot;
    private static string[] _sharedPhrases = new string[0];
    private static readonly Dictionary<string, string[]> ProfilePhrases =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string[]> ClipPhrases =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
    private static HashSet<string> _muteProfiles =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static string _cachedLang = string.Empty;
    private static bool _settingsLoaded;

    [Serializable]
    private sealed class PhrasesFile
    {
        public string[] phrases = new string[0];
    }

    [Serializable]
    private sealed class SettingsFile
    {
        public string[] muteProfiles = new string[0];
    }

    internal static void Initialize(MonoBehaviour host)
    {
        _host = host;
        EnsureSettingsLoaded();
        EnsurePhrasesLoaded(null);
    }

    /// <summary>
    /// Call once when the fatality clip first reaches its last frame.
    /// Waits <see cref="EnemyFatalityConfig.TauntDelaySeconds"/> then shows a random line.
    /// </summary>
    internal static void ScheduleAfterClip(IEnemyFatalityProfile profile)
    {
        CancelPending();

        if (EnemyFatalityConfig.TauntEnable == null || !EnemyFatalityConfig.TauntEnable.Value)
            return;

        if (profile == null || _host == null)
            return;

        EnsureSettingsLoaded();
        if (_muteProfiles.Contains(profile.Id))
        {
            EnemyFatalityConfig.LogDebug(
                "[EnemyFatality] Taunt skipped — mute profile " + profile.Id);
            return;
        }

        string[] pool = ResolvePhrasePool(profile);
        if (pool == null || pool.Length == 0)
        {
            EnemyFatalityConfig.LogDebug(
                "[EnemyFatality] Taunt skipped — no phrases for " + profile.Id);
            return;
        }

        float delay = EnemyFatalityConfig.TauntDelaySeconds != null
            ? Mathf.Max(0f, EnemyFatalityConfig.TauntDelaySeconds.Value)
            : 2f;

        _pending = _host.StartCoroutine(ShowAfterDelay(delay, profile));
    }

    internal static void CancelPending()
    {
        if (_pending != null && _host != null)
        {
            _host.StopCoroutine(_pending);
            _pending = null;
        }

        DestroyOverlay();
    }

    private static IEnumerator ShowAfterDelay(float delaySeconds, IEnemyFatalityProfile profile)
    {
        if (delaySeconds > 0f)
            yield return new WaitForSecondsRealtime(delaySeconds);

        _pending = null;

        if (!EnemyFatalitySession.IsActive)
            yield break;

        if (EnemyFatalityConfig.TauntEnable == null || !EnemyFatalityConfig.TauntEnable.Value)
            yield break;

        string[] pool = ResolvePhrasePool(profile);
        if (pool == null || pool.Length == 0)
            yield break;

        string line = pool[UnityEngine.Random.Range(0, pool.Length)];
        if (string.IsNullOrEmpty(line))
            yield break;

        TryShowScreenTaunt(line.Trim());
    }

    private static void TryShowScreenTaunt(string text)
    {
        try
        {
            DestroyOverlay();

            var go = new GameObject(OverlayRootName);
            Object.DontDestroyOnLoad(go);
            _overlayRoot = go;

            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above FatalityDeathIcon / death chrome, below scarlet flash (32000).
            canvas.sortingOrder = 25000;

            go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

            var textGo = new GameObject("Taunt_XUAIGNORE");
            textGo.transform.SetParent(go.transform, false);

            Text ui = textGo.AddComponent<Text>();
            ui.text = text;
            ui.alignment = TextAnchor.MiddleCenter;
            ui.fontSize = 14;
            ui.color = TauntColor;
            ui.fontStyle = FontStyle.Bold;
            ui.raycastTarget = false;
            ui.horizontalOverflow = HorizontalWrapMode.Wrap;
            ui.verticalOverflow = VerticalWrapMode.Overflow;
            ui.font = HellGateFontProvider.GetUiFont();

            var outline = textGo.AddComponent<Outline>();
            outline.effectColor = TauntOutline;
            outline.effectDistance = new Vector2(1f, -1f);

            RectTransform rt = ui.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(640f, 64f);
            // Between Fatality icon and "YOU'VE BEEN DEFEATED" banner.
            rt.anchoredPosition = new Vector2(0f, ScreenCenterOffsetY);

            EnemyFatalityConfig.LogDebug("[EnemyFatality] Taunt shown (screen): " + text);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[EnemyFatality] Taunt show failed: " + ex.Message);
        }
    }

    private static void DestroyOverlay()
    {
        if (_overlayRoot != null)
        {
            Object.Destroy(_overlayRoot);
            _overlayRoot = null;
        }

        GameObject orphan = GameObject.Find(OverlayRootName);
        if (orphan != null)
            Object.Destroy(orphan);
    }

    private static void EnsureSettingsLoaded()
    {
        if (_settingsLoaded)
            return;

        _settingsLoaded = true;
        _muteProfiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string path = Path.Combine(Path.Combine(ResolveRoot(), SharedFolderName), SettingsFileName);
        if (!File.Exists(path))
        {
            // Sensible defaults if JSON missing: non-speaking beasts.
            _muteProfiles.Add("Minotaurosu");
            _muteProfiles.Add("BlackOoze");
            _muteProfiles.Add("Cocoonman");
            return;
        }

        try
        {
            SettingsFile data = JsonUtility.FromJson<SettingsFile>(File.ReadAllText(path));
            if (data?.muteProfiles == null)
                return;

            for (int i = 0; i < data.muteProfiles.Length; i++)
            {
                string id = data.muteProfiles[i];
                if (!string.IsNullOrEmpty(id))
                    _muteProfiles.Add(id.Trim());
            }

            Plugin.Log?.LogInfo(
                "[EnemyFatality] Taunt mute profiles: " + _muteProfiles.Count
                + " (" + path + ")");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[EnemyFatality] settings.json parse failed: " + ex.Message);
        }
    }

    /// <summary>
    /// Clip override <c>phrases_&lt;clipFolder&gt;.json</c> when present;
    /// else profile <c>phrases_&lt;Id&gt;.json</c>; else shared <c>phrases.json</c>.
    /// Clip/profile files fall back active lang → RU → EN.
    /// </summary>
    private static string[] ResolvePhrasePool(IEnemyFatalityProfile profile)
    {
        EnsurePhrasesLoaded(profile);

        string clipKey = ResolveActiveClipKey();
        if (!string.IsNullOrEmpty(clipKey) &&
            ClipPhrases.TryGetValue(clipKey, out string[] clipPool) &&
            clipPool != null)
        {
            if (clipPool.Length > 0)
                return clipPool;

            // Dedicated clip pools must not fall back to shared leg-bisect lines.
            if (IsDedicatedClipTauntKey(clipKey))
                return clipPool;
        }

        if (profile == null)
            return _sharedPhrases;

        if (ProfilePhrases.TryGetValue(profile.Id, out string[] dedicated) &&
            dedicated != null &&
            dedicated.Length > 0)
            return dedicated;

        return _sharedPhrases;
    }

    private static bool IsDedicatedClipTauntKey(string clipKey)
    {
        if (string.IsNullOrEmpty(clipKey))
            return false;

        for (int i = 0; i < DedicatedClipPhraseKeys.Length; i++)
        {
            if (string.Equals(clipKey, DedicatedClipPhraseKeys[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string ResolveActiveClipKey()
    {
        string rel = EnemyFatalitySession.ActiveClipRelative;
        if (string.IsNullOrEmpty(rel))
            return null;

        rel = rel.Replace('\\', '/').Trim().Trim('/');
        int slash = rel.LastIndexOf('/');
        if (slash >= 0 && slash < rel.Length - 1)
            return rel.Substring(slash + 1);
        return rel;
    }

    private static void EnsurePhrasesLoaded(IEnemyFatalityProfile profile)
    {
        string lang = ResolveLangFolder();
        bool langChanged = !string.Equals(_cachedLang, lang, StringComparison.OrdinalIgnoreCase);
        if (langChanged)
        {
            _cachedLang = lang;
            ProfilePhrases.Clear();
            ClipPhrases.Clear();
            _sharedPhrases = LoadPhrasesFile(lang, PhrasesFileName);
            if (_sharedPhrases.Length == 0 &&
                !string.Equals(lang, "EN", StringComparison.OrdinalIgnoreCase))
                _sharedPhrases = LoadPhrasesFile("EN", PhrasesFileName);

            // Preload dedicated clip packs so a mid-session language switch
            // does not leave HeavyCritical / lost_head on a stale or empty pool.
            for (int i = 0; i < DedicatedClipPhraseKeys.Length; i++)
            {
                string key = DedicatedClipPhraseKeys[i];
                ClipPhrases[key] = LoadDedicatedPhrases(lang, "phrases_" + key + ".json");
            }
        }

        string clipKey = ResolveActiveClipKey();
        if (!string.IsNullOrEmpty(clipKey) && !ClipPhrases.ContainsKey(clipKey))
            ClipPhrases[clipKey] = LoadDedicatedPhrases(lang, "phrases_" + clipKey + ".json");

        if (profile == null || string.IsNullOrEmpty(profile.Id))
            return;

        if (!langChanged && ProfilePhrases.ContainsKey(profile.Id))
            return;

        ProfilePhrases[profile.Id] = LoadDedicatedPhrases(lang, "phrases_" + profile.Id + ".json");
    }

    private static string[] LoadDedicatedPhrases(string lang, string fileName)
    {
        string[] loaded = LoadPhrasesFile(lang, fileName);
        if (loaded.Length == 0 && !string.Equals(lang, "RU", StringComparison.OrdinalIgnoreCase))
            loaded = LoadPhrasesFile("RU", fileName);
        if (loaded.Length == 0 && !string.Equals(lang, "EN", StringComparison.OrdinalIgnoreCase))
            loaded = LoadPhrasesFile("EN", fileName);
        return loaded;
    }

    private static string[] LoadPhrasesFile(string langFolder, string fileName)
    {
        string path = Path.Combine(Path.Combine(ResolveRoot(), langFolder), fileName);
        if (!File.Exists(path))
            return new string[0];

        try
        {
            // Explicit UTF-8 so JP/CN/KR (and accented FR/DE) survive language switches
            // on Windows ANSI default code pages.
            PhrasesFile data = JsonUtility.FromJson<PhrasesFile>(File.ReadAllText(path, Encoding.UTF8));
            if (data?.phrases == null || data.phrases.Length == 0)
                return new string[0];

            var list = new List<string>(data.phrases.Length);
            for (int i = 0; i < data.phrases.Length; i++)
            {
                string p = data.phrases[i];
                if (!string.IsNullOrEmpty(p) && p.Trim().Length > 0)
                    list.Add(p.Trim());
            }

            Plugin.Log?.LogInfo(
                "[EnemyFatality] Loaded " + list.Count + " taunt phrase(s) from " + path);
            return list.ToArray();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning(
                "[EnemyFatality] " + fileName + " parse failed (" + path + "): " + ex.Message);
            return new string[0];
        }
    }

    private static string ResolveRoot()
    {
        return Path.Combine(Path.Combine(Paths.PluginPath, "HellGateJson"), RootFolderName);
    }

    private static string ResolveLangFolder()
    {
        // Prefer HellGate language picker codes (EN/RU/…); EventCore returns En/Ru.
        try
        {
            string raw = Plugin.hellGateLanguage?.Value;
            if (!string.IsNullOrEmpty(raw) && raw.Trim().Length > 0)
                return raw.Trim().ToUpperInvariant();
        }
        catch
        {
        }

        string folder = EventCoreLanguage.ResolveFolderCode();
        return string.IsNullOrEmpty(folder) ? "EN" : folder.ToUpperInvariant();
    }
}
