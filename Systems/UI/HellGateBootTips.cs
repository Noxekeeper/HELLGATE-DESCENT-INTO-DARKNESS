using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace NoREroMod.Systems.UI;

/// <summary>
/// HellGate tips for Early Boot / illustrated guide.
/// Locale files: BepInEx/plugins/HellGateJson/{LANG}/BootLoadingTips.json
/// Parsed manually (Unity JsonUtility is unreliable with large localized tip arrays).
/// </summary>
internal static class HellGateBootTips
{
    internal enum TipPhase
    {
        Loading,
        Guide
    }

    private static readonly List<BootTipEntry> LoadingTips = new List<BootTipEntry>(32);
    private static readonly List<BootTipEntry> GuideTips = new List<BootTipEntry>(16);
    private static float _intervalSeconds = 10f;
    private static int _currentIndex = -1;
    private static float _nextSwapAt;
    private static BootTipEntry _current = BootTipEntry.Empty;
    private static TipPhase _phase = TipPhase.Loading;
    private static bool _loaded;
    private static string _loadedLang = "";
    private static string _cachingMessage = "Caching HellGate content — please wait…";
    private static string _doneButton = "Done";
    private static string _tutorialTitle = "TUTORIAL";
    private static int _navEpoch;

    /// <summary>Warm cream — keycaps stand out on body text without fighting yellow/red semantic colors.</summary>
    private const string KeyHintColorOpen = "<color=#F2E2A8>";
    private const string KeyHintColorClose = "</color>";
    private const string KeyHintBoldOpen = "<b>";
    private const string KeyHintBoldClose = "</b>";

    internal static string CurrentTip
    {
        get
        {
            try
            {
                return StyleKeyHints(_current.text ?? "");
            }
            catch
            {
                return _current.text ?? "";
            }
        }
    }
    internal static string CurrentVideo => _current.video ?? "";
    internal static bool HasCurrentVideo => !string.IsNullOrEmpty(_current.video);
    internal static int NavEpoch => _navEpoch;
    internal static string CachingMessage =>
        string.IsNullOrEmpty(_cachingMessage) ? "Caching HellGate content — please wait…" : _cachingMessage;
    internal static string DoneButtonLabel =>
        string.IsNullOrEmpty(_doneButton) ? "Done" : _doneButton;
    internal static string TutorialTitle =>
        string.IsNullOrEmpty(_tutorialTitle) ? "TUTORIAL" : _tutorialTitle;

    internal static string ResolveCurrentLanguage()
    {
        try
        {
            if (Plugin.hellGateLanguage != null && !string.IsNullOrEmpty(Plugin.hellGateLanguage.Value))
                return Plugin.hellGateLanguage.Value.Trim().ToUpperInvariant();
        }
        catch { }

        return "EN";
    }

    internal static void EnsureLoaded()
    {
        string lang = ResolveCurrentLanguage();
        if (_loaded && string.Equals(_loadedLang, lang, StringComparison.OrdinalIgnoreCase))
            return;

        ReloadForLanguage(lang);
    }

    internal static void ReloadForLanguage(string languageCode)
    {
        string lang = string.IsNullOrEmpty(languageCode) ? "EN" : languageCode.Trim().ToUpperInvariant();
        TipPhase keepPhase = _phase;

        LoadingTips.Clear();
        GuideTips.Clear();
        _cachingMessage = "Caching HellGate content — please wait…";
        _doneButton = "Done";
        _tutorialTitle = "TUTORIAL";
        _intervalSeconds = 10f;
        _loaded = false;
        _loadedLang = "";

        bool ok = TryLoadFromJson(lang);
        if (!ok && !string.Equals(lang, "EN", StringComparison.OrdinalIgnoreCase))
        {
            Plugin.Log?.LogWarning($"[HellGate Tips] No BootLoadingTips.json for {lang} — falling back to EN.");
            ok = TryLoadFromJson("EN");
            if (ok)
                lang = "EN";
        }

        if (!ok || LoadingTips.Count == 0)
            AddBuiltInLoadingTips();
        if (!ok || GuideTips.Count == 0)
            AddBuiltInGuideTips();

        _loaded = true;
        _loadedLang = lang;
        SetPhase(keepPhase == TipPhase.Guide && GuideTips.Count > 0 ? keepPhase : TipPhase.Loading);
        Plugin.Log?.LogInfo(
            $"[HellGate Tips] Loaded loading={LoadingTips.Count}, guide={GuideTips.Count} ({_loadedLang}), interval={_intervalSeconds:0.#}s.");
    }

    internal static void SetPhase(TipPhase phase)
    {
        _phase = phase;
        _currentIndex = -1;
        _current = BootTipEntry.Empty;
        _nextSwapAt = 0f;
        ShowAtIndex(0, bumpNav: true);
    }

    /// <summary>Loading tips auto-advance; guide clips loop every intervalSeconds (timer resets on Back/Forward).</summary>
    internal static void Tick(float unscaledDeltaTime)
    {
        if (!_loaded)
            return;

        if (_phase == TipPhase.Loading)
        {
            List<BootTipEntry> list = LoadingTips;
            if (list.Count <= 1)
                return;

            if (_currentIndex >= list.Count - 1)
                return;
            if (_current != null && _current.holdUntilDone)
                return;

            if (Time.unscaledTime >= _nextSwapAt)
                ShowAtIndex(_currentIndex + 1, bumpNav: false);
            return;
        }

        if (_phase == TipPhase.Guide)
        {
            List<BootTipEntry> list = GuideTips;
            if (list.Count <= 1)
                return;
            if (Time.unscaledTime >= _nextSwapAt)
                ShowAtIndex((_currentIndex + 1) % list.Count, bumpNav: true);
        }
    }

    internal static bool TryGoBack()
    {
        List<BootTipEntry> list = ActiveList();
        if (list.Count == 0)
            return false;
        int prev = _currentIndex <= 0 ? list.Count - 1 : _currentIndex - 1;
        ShowAtIndex(prev, bumpNav: true);
        return true;
    }

    internal static bool TryGoForward()
    {
        List<BootTipEntry> list = ActiveList();
        if (list.Count == 0)
            return false;
        ShowAtIndex((_currentIndex + 1) % list.Count, bumpNav: true);
        return true;
    }

    internal static void ResetSession()
    {
        _currentIndex = -1;
        _current = BootTipEntry.Empty;
        _nextSwapAt = 0f;
        _phase = TipPhase.Loading;
        _navEpoch = 0;
    }

    internal static string ResolveHellGateSourcesMedia(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
            return "";

        try
        {
            string gameRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string sourcesRoot = Path.Combine(Path.Combine(gameRoot, "sources"), "HellGate_sources");
            string combined = Path.Combine(sourcesRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            return Path.GetFullPath(combined);
        }
        catch
        {
            return "";
        }
    }

    private static List<BootTipEntry> ActiveList()
    {
        return _phase == TipPhase.Guide ? GuideTips : LoadingTips;
    }

    private static void ShowAtIndex(int index, bool bumpNav)
    {
        List<BootTipEntry> list = ActiveList();
        if (list.Count == 0)
            return;

        _currentIndex = Mathf.Clamp(index, 0, list.Count - 1);
        _current = list[_currentIndex];

        float hold = Mathf.Max(3f, _intervalSeconds);
        _nextSwapAt = Time.unscaledTime + hold;

        if (bumpNav)
            _navEpoch++;
    }

    private static bool TryLoadFromJson(string lang)
    {
        try
        {
            string basePath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string path = Path.Combine(
                Path.Combine(Path.Combine(Path.Combine(basePath, "BepInEx"), "plugins"), "HellGateJson"),
                Path.Combine(lang, "BootLoadingTips.json"));
            if (!File.Exists(path))
            {
                Plugin.Log?.LogWarning($"[HellGate Tips] Missing file: {path}");
                return false;
            }

            string json = File.ReadAllText(path, Encoding.UTF8);
            if (json.Length > 0 && json[0] == '\uFEFF')
                json = json.Substring(1);

            ParseUiAndInterval(json);

            int loading = 0;
            int guide = 0;
            foreach (string obj in ExtractJsonObjectsInArray(json, "tips"))
            {
                BootTipEntry? entry = ParseTipObject(obj);
                if (entry == null || string.IsNullOrEmpty(entry.text))
                    continue;

                if (string.Equals(entry.phase, "guide", StringComparison.OrdinalIgnoreCase))
                {
                    GuideTips.Add(entry);
                    guide++;
                }
                else
                {
                    LoadingTips.Add(entry);
                    loading++;
                }
            }

            Plugin.Log?.LogInfo($"[HellGate Tips] JSON {lang}: loading={loading}, guide={guide} from {path}");
            return loading > 0 || guide > 0;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[HellGate Tips] Load failed ({lang}): {ex.Message}");
            return false;
        }
    }

    private static void ParseUiAndInterval(string json)
    {
        Match iv = Regex.Match(json, "\"intervalSeconds\"\\s*:\\s*([0-9.]+)", RegexOptions.CultureInvariant);
        if (iv.Success
            && float.TryParse(iv.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds)
            && seconds > 0.5f)
            _intervalSeconds = seconds;

        string uiBlock = ExtractObjectBody(json, "ui");
        if (string.IsNullOrEmpty(uiBlock))
            return;

        string caching = ExtractJsonString(uiBlock, "cachingMessage");
        string done = ExtractJsonString(uiBlock, "doneButton");
        if (string.IsNullOrEmpty(done))
            done = ExtractJsonString(uiBlock, "nextButton"); // legacy key
        string title = ExtractJsonString(uiBlock, "tutorialTitle");
        if (!string.IsNullOrEmpty(caching))
            _cachingMessage = caching;
        if (!string.IsNullOrEmpty(done))
            _doneButton = done;
        if (!string.IsNullOrEmpty(title))
            _tutorialTitle = title;
    }

    private static BootTipEntry? ParseTipObject(string obj)
    {
        string text = ExtractJsonString(obj, "text");
        if (string.IsNullOrEmpty(text))
            return null;

        string phase = ExtractJsonString(obj, "phase");
        if (string.IsNullOrEmpty(phase))
            phase = "loading";

        string holdRaw = ExtractJsonRaw(obj, "holdUntilDone");
        bool hold = string.Equals(holdRaw, "true", StringComparison.OrdinalIgnoreCase);

        return new BootTipEntry
        {
            id = ExtractJsonString(obj, "id") ?? "",
            phase = phase.Trim().ToLowerInvariant(),
            text = text.Trim(),
            video = (ExtractJsonString(obj, "video") ?? "").Trim().Replace('\\', '/'),
            holdUntilDone = hold
        };
    }

    private static List<string> ExtractJsonObjectsInArray(string json, string arrayName)
    {
        var list = new List<string>(32);
        Match m = Regex.Match(json, "\"" + arrayName + "\"\\s*:\\s*\\[");
        if (!m.Success)
            return list;

        int i = m.Index + m.Length;
        while (i < json.Length)
        {
            while (i < json.Length && char.IsWhiteSpace(json[i]))
                i++;
            if (i >= json.Length || json[i] == ']')
                break;
            if (json[i] == ',')
            {
                i++;
                continue;
            }
            if (json[i] != '{')
                break;

            int start = i;
            int depth = 0;
            bool inStr = false;
            bool esc = false;
            for (; i < json.Length; i++)
            {
                char c = json[i];
                if (inStr)
                {
                    if (esc)
                        esc = false;
                    else if (c == '\\')
                        esc = true;
                    else if (c == '"')
                        inStr = false;
                    continue;
                }

                if (c == '"')
                    inStr = true;
                else if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        i++;
                        list.Add(json.Substring(start, i - start));
                        break;
                    }
                }
            }
        }

        return list;
    }

    private static string ExtractObjectBody(string json, string objectName)
    {
        Match m = Regex.Match(json, "\"" + objectName + "\"\\s*:\\s*\\{");
        if (!m.Success)
            return "";

        int start = m.Index + m.Length - 1;
        int depth = 0;
        bool inStr = false;
        bool esc = false;
        for (int i = start; i < json.Length; i++)
        {
            char c = json[i];
            if (inStr)
            {
                if (esc)
                    esc = false;
                else if (c == '\\')
                    esc = true;
                else if (c == '"')
                    inStr = false;
                continue;
            }

            if (c == '"')
                inStr = true;
            else if (c == '{')
                depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                    return json.Substring(start + 1, i - start - 1);
            }
        }

        return "";
    }

    private static string ExtractJsonString(string json, string key)
    {
        Match m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"");
        if (!m.Success)
            return "";

        int i = m.Index + m.Length;
        var sb = new StringBuilder(256);
        while (i < json.Length)
        {
            char c = json[i++];
            if (c == '\\' && i < json.Length)
            {
                char n = json[i++];
                switch (n)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'u':
                        if (i + 3 < json.Length
                            && int.TryParse(json.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int cp))
                        {
                            sb.Append((char)cp);
                            i += 4;
                        }
                        break;
                    default:
                        sb.Append(n);
                        break;
                }
                continue;
            }

            if (c == '"')
                break;
            sb.Append(c);
        }

        return sb.ToString();
    }

    private static string ExtractJsonRaw(string json, string key)
    {
        Match m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*([^,}\\s]+)");
        return m.Success ? m.Groups[1].Value.Trim() : "";
    }

    private static void AddBuiltInLoadingTips()
    {
        LoadingTips.Add(new BootTipEntry
        {
            phase = "loading",
            id = "manifesto",
            text = "HellGate lets you tune every module to your taste.\n\nSee the Manifesto in HELLGATE_README."
        });
        LoadingTips.Add(new BootTipEntry
        {
            phase = "loading",
            id = "controls",
            holdUntilDone = true,
            text = "<b>Basic controls</b>\n\n[LMB] attack · [RMB] block · [F] dodge · [G] Rage · [H] factions"
        });
    }

    private static void AddBuiltInGuideTips()
    {
        GuideTips.Add(new BootTipEntry
        {
            id = "parry",
            phase = "guide",
            text = "Hold block and press W before the hit lands to stun the enemy.\n\nPress E nearby for Vengeance Strike.",
            video = "Tutorial Guide source/Parry.mp4"
        });
        GuideTips.Add(new BootTipEntry
        {
            id = "rage",
            phase = "guide",
            text = "Activate RAGE (G) for 9-hit sword combos with no fatigue.",
            video = "Tutorial Guide source/Rage.mp4"
        });
        GuideTips.Add(new BootTipEntry
        {
            id = "block",
            phase = "guide",
            text = "Block (RMB) cuts damage and stops knockdowns and grabs.",
            video = "Tutorial Guide source/Block.mp4"
        });
    }

    /// <summary>
    /// Highlight [Key] tokens. Manual scan — static Regex.Compiled blows the type initializer on Unity 5.6 Mono.
    /// Never nests &lt;b&gt; inside an already-open bold span (Unity IMGUI rich text then prints tags raw).
    /// Bold-wrap rules:
    /// - <c>&lt;b&gt;[G]&lt;/b&gt;</c> → consume both tags, re-emit cream+bold key
    /// - <c>&lt;b&gt;[RMB]+[W]&lt;/b&gt;</c> → consume opening with first key, closing with last
    /// - <c>&lt;b&gt;Rage Mode [G]&lt;/b&gt;</c> → leave outer bold intact; cream on key only
    /// </summary>
    private static string StyleKeyHints(string text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('[') < 0)
            return text;

        StringBuilder sb = new StringBuilder(text.Length + 64);
        int i = 0;
        // Opens from <b>[Key]… that still need a matching </b> consumed later (e.g. <b>[RMB]+[W]</b>).
        int pendingConsumedBoldCloses = 0;
        while (i < text.Length)
        {
            int open = text.IndexOf('[', i);
            if (open < 0)
            {
                sb.Append(text, i, text.Length - i);
                break;
            }

            int close = text.IndexOf(']', open + 1);
            if (close < 0 || close - open > 25)
            {
                sb.Append(text, i, open - i + 1);
                i = open + 1;
                continue;
            }

            string inner = text.Substring(open + 1, close - open - 1);
            if (inner.IndexOf('<') >= 0 || inner.IndexOf('\n') >= 0 || inner.IndexOf('\r') >= 0)
            {
                sb.Append(text, i, close - i + 1);
                i = close + 1;
                continue;
            }

            int start = open;
            int end = close + 1;
            bool consumedAdjacentBold = false;
            if (start >= 3 && string.CompareOrdinal(text, start - 3, "<b>", 0, 3) == 0)
            {
                start -= 3;
                consumedAdjacentBold = true;
            }

            bool canConsumeTrailingBoldClose = consumedAdjacentBold || pendingConsumedBoldCloses > 0;
            bool consumedTrailingBoldClose = false;
            if (canConsumeTrailingBoldClose
                && end + 4 <= text.Length
                && string.CompareOrdinal(text, end, "</b>", 0, 4) == 0)
            {
                end += 4;
                consumedTrailingBoldClose = true;
            }

            // Already color-wrapped — leave as-is (avoids nesting on repeat reads).
            if (start >= 8)
            {
                int look = Math.Min(start, 28);
                int colorAt = text.LastIndexOf("<color=", start - 1, look);
                if (colorAt >= 0)
                {
                    if (end + 8 <= text.Length && string.CompareOrdinal(text, end, "</color>", 0, 8) == 0)
                        end += 8;
                    sb.Append(text, i, end - i);
                    i = end;
                    continue;
                }
            }

            if (consumedTrailingBoldClose && !consumedAdjacentBold)
                pendingConsumedBoldCloses--;
            else if (consumedAdjacentBold && !consumedTrailingBoldClose)
                pendingConsumedBoldCloses++;

            // Bare [G] or <b>[G]</b> → cream + bold. [G] inside a larger <b>…</b> → cream only
            // (nested <b> breaks Unity 5.6 IMGUI rich text and shows tags as plain text).
            bool addBold = consumedAdjacentBold || !IsInsideUnclosedBold(text, open);

            sb.Append(text, i, start - i);
            sb.Append(KeyHintColorOpen);
            if (addBold)
                sb.Append(KeyHintBoldOpen);
            sb.Append('[');
            sb.Append(inner);
            sb.Append(']');
            if (addBold)
                sb.Append(KeyHintBoldClose);
            sb.Append(KeyHintColorClose);
            i = end;
        }

        return sb.ToString();
    }

    private static bool IsInsideUnclosedBold(string text, int index)
    {
        int depth = 0;
        int i = 0;
        while (i < index)
        {
            if (i + 3 <= text.Length && string.CompareOrdinal(text, i, "<b>", 0, 3) == 0)
            {
                depth++;
                i += 3;
                continue;
            }

            if (i + 4 <= text.Length && string.CompareOrdinal(text, i, "</b>", 0, 4) == 0)
            {
                if (depth > 0)
                    depth--;
                i += 4;
                continue;
            }

            i++;
        }

        return depth > 0;
    }

    private sealed class BootTipEntry
    {
        public string id = "";
        public string phase = "loading";
        public string text = "";
        public string video = "";
        public bool holdUntilDone;

        internal static readonly BootTipEntry Empty = new BootTipEntry();
    }
}
