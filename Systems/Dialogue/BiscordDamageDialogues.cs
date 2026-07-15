using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using NoREroMod.Patches.Enemy.HeckGateEnemy;

namespace NoREroMod.Systems.Dialogue;

internal enum BiscordDialogueBranch
{
    Move,
    Attack,
    Damage
}

/// <summary>
/// biscord dialogue system loaded from HellGateJson.
/// Supports branch-based lines: move/attack/damage.
/// </summary>
internal static class BiscordDialogues
{
    private static readonly List<string> _moveLines = new List<string>();
    private static readonly List<string> _attackLines = new List<string>();
    private static readonly List<string> _damageLines = new List<string>();
    private static readonly Dictionary<string, float> _lastShownTimeByBranchKey = new Dictionary<string, float>();
    private static readonly Dictionary<int, float> _lastAnyPhraseTimeByInstance = new Dictionary<int, float>();
    private static readonly Dictionary<int, int> _pendingMaskByInstance = new Dictionary<int, int>();
    private static readonly Dictionary<int, int> _forcedMaskByInstance = new Dictionary<int, int>();
    private static readonly Dictionary<int, int> _nextBranchIndexByInstance = new Dictionary<int, int>();
    private static readonly Dictionary<string, List<int>> _lineOrderByBranchKey = new Dictionary<string, List<int>>();
    private static readonly Dictionary<string, int> _lineCursorByBranchKey = new Dictionary<string, int>();
    private static readonly Dictionary<int, int> _lastStateByInstance = new Dictionary<int, int>();
    private static readonly Dictionary<string, AudioClip> _voiceClipCache = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> _voicePathByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static bool _initialized;
    private static bool _loggedMissingDisplay;
    private static bool _voiceIndexBuilt;
    private static float _cooldownSeconds = 0.9f;
    private static float _moveCooldownSeconds = 3.2f;
    private static float _attackCooldownSeconds = 2.4f;
    private static float _damageCooldownSeconds = 0.7f;
    /// <summary>After any branch line is shown, no line (move/attack/damage) may fire for this many seconds. Not bypassed by ignoreCooldown.</summary>
    private static float _globalPhraseCooldownSeconds = 3f;
    private static float _displayDuration = 1.25f;
    private static float _verticalOffset = 150f;
    private static string _boneName = "body_ue";
    private static Color _textColor = new Color(1f, 0.55f, 0.85f, 1f);
    private static Color _outlineColor = Color.black;
    private static AudioSource _voiceAudioSource;
    private static BiscordVoiceRunner _voiceRunner;

    internal static void Initialize()
    {
        if (_initialized)
            return;

        LoadFromJson();
        EnsureVoiceRuntime();
        BuildVoiceIndex();
        _initialized = true;
    }

    internal static void Reset()
    {
        _initialized = false;
        _loggedMissingDisplay = false;
        _moveLines.Clear();
        _attackLines.Clear();
        _damageLines.Clear();
        _lastShownTimeByBranchKey.Clear();
        _lastAnyPhraseTimeByInstance.Clear();
        _pendingMaskByInstance.Clear();
        _forcedMaskByInstance.Clear();
        _nextBranchIndexByInstance.Clear();
        _lineOrderByBranchKey.Clear();
        _lineCursorByBranchKey.Clear();
        _lastStateByInstance.Clear();
        _voiceClipCache.Clear();
        _voicePathByKey.Clear();
        _voiceIndexBuilt = false;
        if (_voiceAudioSource != null)
            _voiceAudioSource.Stop();
    }

    internal static void ClearInstance(int instanceId)
    {
        _lastShownTimeByBranchKey.Remove(BuildBranchKey(instanceId, BiscordDialogueBranch.Move));
        _lastShownTimeByBranchKey.Remove(BuildBranchKey(instanceId, BiscordDialogueBranch.Attack));
        _lastShownTimeByBranchKey.Remove(BuildBranchKey(instanceId, BiscordDialogueBranch.Damage));
        _lastStateByInstance.Remove(instanceId);
        _lastAnyPhraseTimeByInstance.Remove(instanceId);
        _pendingMaskByInstance.Remove(instanceId);
        _forcedMaskByInstance.Remove(instanceId);
        _nextBranchIndexByInstance.Remove(instanceId);
        _lineOrderByBranchKey.Remove(BuildBranchKey(instanceId, BiscordDialogueBranch.Move));
        _lineOrderByBranchKey.Remove(BuildBranchKey(instanceId, BiscordDialogueBranch.Attack));
        _lineOrderByBranchKey.Remove(BuildBranchKey(instanceId, BiscordDialogueBranch.Damage));
        _lineCursorByBranchKey.Remove(BuildBranchKey(instanceId, BiscordDialogueBranch.Move));
        _lineCursorByBranchKey.Remove(BuildBranchKey(instanceId, BiscordDialogueBranch.Attack));
        _lineCursorByBranchKey.Remove(BuildBranchKey(instanceId, BiscordDialogueBranch.Damage));
    }

    internal static void TryShowOnDamageForce(suraimu slime)
    {
        EnqueueBranch(slime, BiscordDialogueBranch.Damage, forceBranchCooldown: true);
        TryShowQueued(slime);
    }

    internal static void TryShowOnStateUpdate(suraimu slime)
    {
        if (slime == null) return;
        if (!_initialized) Initialize();

        // Keep move events flowing continuously; queue/cooldown logic handles final cadence.
        EnqueueBranch(slime, BiscordDialogueBranch.Move, forceBranchCooldown: false);

        int id = slime.GetInstanceID();
        int currentState = (int)slime.state;
        if (!_lastStateByInstance.TryGetValue(id, out int prevState))
        {
            _lastStateByInstance[id] = currentState;
            return;
        }
        _lastStateByInstance[id] = currentState;
        if (currentState == prevState) return;

        bool isAttack =
            slime.state == suraimu.enemystate.ATK1 ||
            slime.state == suraimu.enemystate.ATK2 ||
            slime.state == suraimu.enemystate.ATK3 ||
            slime.state == suraimu.enemystate.ATK4;
        if (isAttack)
        {
            EnqueueBranch(slime, BiscordDialogueBranch.Attack, forceBranchCooldown: false);
        }

        TryShowQueued(slime);
    }

    private static void EnqueueBranch(suraimu slime, BiscordDialogueBranch branch, bool forceBranchCooldown)
    {
        if (slime == null)
            return;

        if (!_initialized)
            Initialize();

        if (GetLines(branch).Count == 0)
            return;

        int id = slime.GetInstanceID();
        int branchBit = 1 << (int)branch;

        int pendingMask = 0;
        _pendingMaskByInstance.TryGetValue(id, out pendingMask);
        _pendingMaskByInstance[id] = pendingMask | branchBit;

        if (forceBranchCooldown)
        {
            int forcedMask = 0;
            _forcedMaskByInstance.TryGetValue(id, out forcedMask);
            _forcedMaskByInstance[id] = forcedMask | branchBit;
        }
    }

    private static void TryShowQueued(suraimu slime)
    {
        if (slime == null)
            return;
        if (!_initialized)
            Initialize();

        int id = slime.GetInstanceID();
        float now = Time.unscaledTime;

        // Lock window = phrase visible time + post-phrase cooldown.
        // This guarantees: "show text first, then cooldown, then next phrase".
        float fullLockDuration = Mathf.Max(0f, _displayDuration) + Mathf.Max(0f, _globalPhraseCooldownSeconds);
        if (_lastAnyPhraseTimeByInstance.TryGetValue(id, out float lastAnyPhraseTime) &&
            now - lastAnyPhraseTime < fullLockDuration)
            return;

        int pendingMask = 0;
        _pendingMaskByInstance.TryGetValue(id, out pendingMask);
        if (pendingMask == 0)
            return;

        int forcedMask = 0;
        _forcedMaskByInstance.TryGetValue(id, out forcedMask);
        int nextBranch = 0;
        _nextBranchIndexByInstance.TryGetValue(id, out nextBranch);

        BiscordDialogueBranch selectedBranch = BiscordDialogueBranch.Move;
        bool found = false;
        for (int i = 0; i < 3; i++)
        {
            int idx = (nextBranch + i) % 3;
            int bit = 1 << idx;
            if ((pendingMask & bit) == 0)
                continue;

            BiscordDialogueBranch branch = (BiscordDialogueBranch)idx;
            bool ignoreBranchCooldown = (forcedMask & bit) != 0;
            if (!ignoreBranchCooldown && !IsBranchCooldownReady(id, branch, now))
                continue;

            selectedBranch = branch;
            found = true;
            break;
        }
        if (!found)
            return;

        List<string> lines = GetLines(selectedBranch);
        if (lines.Count == 0)
            return;

        DialogueDisplay display = DialogueFramework.GetDisplay();
        if (display == null)
        {
            if (!_loggedMissingDisplay)
            {
                _loggedMissingDisplay = true;
                Plugin.Log?.LogWarning("[biscord-dialogues] Dialogue display is null.");
            }
            return;
        }

        string line = GetNextLine(id, selectedBranch, lines);
        BonePosition bonePos = new BonePosition
        {
            BoneName = string.IsNullOrEmpty(_boneName) ? "body_ue" : _boneName,
            UseScreenCenter = false
        };

        DialogueStyle style = new DialogueStyle
        {
            FontSize = Plugin.dialogueFontSize.Value,
            Color = _textColor,
            IsBold = true,
            IsItalic = false,
            VerticalOffset = _verticalOffset,
            HorizontalOffset = 0f,
            FollowBone = true,
            UseOutline = true,
            OutlineColor = _outlineColor,
            OutlineDistance = new Vector2(1f, -1f)
        };

        display.ShowStaticThreat(slime, line, bonePos, style, _displayDuration);
        TryPlayVoice(selectedBranch, line);

        string branchKey = BuildBranchKey(id, selectedBranch);
        _lastShownTimeByBranchKey[branchKey] = now;
        _lastAnyPhraseTimeByInstance[id] = now;

        int selectedBit = 1 << (int)selectedBranch;
        _pendingMaskByInstance[id] = pendingMask & ~selectedBit;
        _forcedMaskByInstance[id] = forcedMask & ~selectedBit;
        _nextBranchIndexByInstance[id] = ((int)selectedBranch + 1) % 3;
    }

    private static void LoadFromJson()
    {
        _moveLines.Clear();
        _attackLines.Clear();
        _damageLines.Clear();

        string jsonPath = ResolveJsonPath();
        if (!File.Exists(jsonPath))
            return;

        try
        {
            string json = File.ReadAllText(jsonPath);
            ParseSettings(json);
            ParseBranchLines(json, "move", _moveLines);
            ParseBranchLines(json, "attack", _attackLines);
            ParseBranchLines(json, "damage", _damageLines);
            int total = _moveLines.Count + _attackLines.Count + _damageLines.Count;
            Plugin.Log?.LogInfo($"[biscord-dialogues] Loaded lines: move={_moveLines.Count}, attack={_attackLines.Count}, damage={_damageLines.Count}, total={total} from {jsonPath}");
        }
        catch
        {
            _moveLines.Clear();
            _attackLines.Clear();
            _damageLines.Clear();
            Plugin.Log?.LogWarning($"[biscord-dialogues] Failed to load JSON: {jsonPath}");
        }
    }

    private static string ResolveJsonPath()
    {
        string dataPath = GetDataPath();
        string currentLangPath = Path.Combine(dataPath, "BiscordDialoguesData.json");
        if (File.Exists(currentLangPath))
            return currentLangPath;

        try
        {
            string basePath = Path.Combine(Application.dataPath, "..");
            string enPath = Path.Combine(
                Path.Combine(Path.Combine(Path.Combine(basePath, "BepInEx"), "plugins"), "HellGateJson"),
                "EN");
            string enJsonPath = Path.Combine(enPath, "BiscordDialoguesData.json");
            if (File.Exists(enJsonPath))
                return enJsonPath;
        }
        catch
        {
        }

        return currentLangPath;
    }

    private static string GetDataPath()
    {
        try
        {
            string basePath = Path.Combine(Application.dataPath, "..");
            string hellGateJson = Path.Combine(Path.Combine(Path.Combine(basePath, "BepInEx"), "plugins"), "HellGateJson");
            string languageCode = Plugin.hellGateLanguage?.Value ?? "EN";
            if (string.IsNullOrEmpty(languageCode))
                languageCode = "EN";

            string langPath = Path.Combine(hellGateJson, languageCode);
            if (Directory.Exists(langPath))
                return langPath;

            string enPath = Path.Combine(hellGateJson, "EN");
            if (Directory.Exists(enPath))
                return enPath;

            return hellGateJson;
        }
        catch
        {
            string fallbackBasePath = Path.Combine(Application.dataPath, "..");
            return Path.Combine(Path.Combine(Path.Combine(fallbackBasePath, "BepInEx"), "plugins"), "HellGateJson");
        }
    }

    private static void ParseSettings(string json)
    {
        Match cooldownMatch = Regex.Match(json, "\"cooldownSeconds\"\\s*:\\s*([0-9.]+)");
        if (cooldownMatch.Success && float.TryParse(cooldownMatch.Groups[1].Value, out float cooldown))
            _cooldownSeconds = Mathf.Max(0f, cooldown);
        _moveCooldownSeconds = _cooldownSeconds;
        _attackCooldownSeconds = _cooldownSeconds;
        _damageCooldownSeconds = _cooldownSeconds;

        Match durationMatch = Regex.Match(json, "\"displayDurationSeconds\"\\s*:\\s*([0-9.]+)");
        if (durationMatch.Success && float.TryParse(durationMatch.Groups[1].Value, out float duration))
            _displayDuration = Mathf.Max(0.1f, duration);

        Match moveCooldownMatch = Regex.Match(json, "\"moveCooldownSeconds\"\\s*:\\s*([0-9.]+)");
        if (moveCooldownMatch.Success && float.TryParse(moveCooldownMatch.Groups[1].Value, out float moveCd))
            _moveCooldownSeconds = Mathf.Max(0f, moveCd);

        Match attackCooldownMatch = Regex.Match(json, "\"attackCooldownSeconds\"\\s*:\\s*([0-9.]+)");
        if (attackCooldownMatch.Success && float.TryParse(attackCooldownMatch.Groups[1].Value, out float attackCd))
            _attackCooldownSeconds = Mathf.Max(0f, attackCd);

        Match damageCooldownMatch = Regex.Match(json, "\"damageCooldownSeconds\"\\s*:\\s*([0-9.]+)");
        if (damageCooldownMatch.Success && float.TryParse(damageCooldownMatch.Groups[1].Value, out float damageCd))
            _damageCooldownSeconds = Mathf.Max(0f, damageCd);

        Match globalPhraseCdMatch = Regex.Match(json, "\"globalPhraseCooldownSeconds\"\\s*:\\s*([0-9.]+)");
        if (globalPhraseCdMatch.Success && float.TryParse(globalPhraseCdMatch.Groups[1].Value, out float globalCd))
            _globalPhraseCooldownSeconds = Mathf.Max(0f, globalCd);

        Match offsetMatch = Regex.Match(json, "\"verticalOffset\"\\s*:\\s*([0-9.\\-]+)");
        if (offsetMatch.Success && float.TryParse(offsetMatch.Groups[1].Value, out float offset))
            _verticalOffset = offset;

        Match boneMatch = Regex.Match(json, "\"boneName\"\\s*:\\s*\"([^\"]+)\"");
        if (boneMatch.Success)
            _boneName = boneMatch.Groups[1].Value;

        _textColor = ParseColor(json, "textColor", _textColor);
        _outlineColor = ParseColor(json, "outlineColor", _outlineColor);
    }

    private static void ParseBranchLines(string json, string branchName, List<string> target)
    {
        target.Clear();
        Match linesMatch = Regex.Match(json, $"\"{branchName}\"\\s*:\\s*\\[([\\s\\S]*?)\\]");
        if (!linesMatch.Success)
            return;

        string linesContent = linesMatch.Groups[1].Value;
        MatchCollection textMatches = Regex.Matches(linesContent, "\"([^\"]+)\"");
        for (int i = 0; i < textMatches.Count; i++)
        {
            string line = textMatches[i].Groups[1].Value;
            if (!string.IsNullOrEmpty(line))
                target.Add(line);
        }
    }

    private static List<string> GetLines(BiscordDialogueBranch branch)
    {
        switch (branch)
        {
            case BiscordDialogueBranch.Move:
                return _moveLines;
            case BiscordDialogueBranch.Attack:
                return _attackLines;
            default:
                return _damageLines;
        }
    }

    private static string BuildBranchKey(int instanceId, BiscordDialogueBranch branch)
    {
        return instanceId.ToString() + ":" + ((int)branch).ToString();
    }

    private static float GetBranchCooldown(BiscordDialogueBranch branch)
    {
        switch (branch)
        {
            case BiscordDialogueBranch.Move:
                return _moveCooldownSeconds;
            case BiscordDialogueBranch.Attack:
                return _attackCooldownSeconds;
            case BiscordDialogueBranch.Damage:
                return _damageCooldownSeconds;
            default:
                return _cooldownSeconds;
        }
    }

    private static bool IsBranchCooldownReady(int instanceId, BiscordDialogueBranch branch, float now)
    {
        string branchKey = BuildBranchKey(instanceId, branch);
        if (_lastShownTimeByBranchKey.TryGetValue(branchKey, out float lastTime))
        {
            float branchCooldown = GetBranchCooldown(branch);
            return now - lastTime >= branchCooldown;
        }
        return true;
    }

    private static string GetNextLine(int instanceId, BiscordDialogueBranch branch, List<string> lines)
    {
        string branchKey = BuildBranchKey(instanceId, branch);
        if (!_lineOrderByBranchKey.TryGetValue(branchKey, out List<int> order) || order == null || order.Count != lines.Count)
        {
            order = new List<int>(lines.Count);
            for (int i = 0; i < lines.Count; i++)
                order.Add(i);
            Shuffle(order);
            _lineOrderByBranchKey[branchKey] = order;
            _lineCursorByBranchKey[branchKey] = 0;
        }

        int cursor = 0;
        _lineCursorByBranchKey.TryGetValue(branchKey, out cursor);
        if (cursor >= order.Count)
        {
            Shuffle(order);
            cursor = 0;
        }

        int index = order[cursor];
        _lineCursorByBranchKey[branchKey] = cursor + 1;
        return lines[Mathf.Clamp(index, 0, lines.Count - 1)];
    }

    private static void Shuffle(List<int> values)
    {
        if (values == null || values.Count <= 1)
            return;
        for (int i = values.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            int temp = values[i];
            values[i] = values[j];
            values[j] = temp;
        }
    }

    private static void EnsureVoiceRuntime()
    {
        if (_voiceRunner != null && _voiceAudioSource != null)
            return;

        GameObject go = new GameObject("BiscordDialogueVoice_XUAIGNORE");
        UnityEngine.Object.DontDestroyOnLoad(go);
        _voiceAudioSource = go.AddComponent<AudioSource>();
        _voiceAudioSource.playOnAwake = false;
        _voiceAudioSource.loop = false;
        _voiceAudioSource.spatialBlend = 0f;
        _voiceAudioSource.volume = 1f;
        _voiceRunner = go.AddComponent<BiscordVoiceRunner>();
    }

    private static void BuildVoiceIndex()
    {
        if (_voiceIndexBuilt)
            return;
        _voiceIndexBuilt = true;
        _voicePathByKey.Clear();

        string root = GetBiscordVoiceRoot();
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            return;

        IndexBranchFolder(root, "Move", BiscordDialogueBranch.Move);
        IndexBranchFolder(root, "Attack", BiscordDialogueBranch.Attack);
        IndexBranchFolder(root, "Damage", BiscordDialogueBranch.Damage);
    }

    private static void IndexBranchFolder(string root, string folderName, BiscordDialogueBranch branch)
    {
        string folder = Path.Combine(root, folderName);
        if (!Directory.Exists(folder))
            return;

        string[] wavFiles = Directory.GetFiles(folder, "*.wav", SearchOption.TopDirectoryOnly);
        for (int i = 0; i < wavFiles.Length; i++)
        {
            string fullPath = wavFiles[i];
            string name = Path.GetFileNameWithoutExtension(fullPath);
            if (string.IsNullOrEmpty(name))
                continue;

            RegisterVoicePath(branch, name, fullPath);
        }
    }

    private static void TryPlayVoice(BiscordDialogueBranch branch, string phrase)
    {
        if (string.IsNullOrEmpty(phrase))
            return;
        if (_voiceRunner == null || _voiceAudioSource == null)
            return;

        BuildVoiceIndex();
        string path = ResolveVoicePath(branch, phrase);
        if (string.IsNullOrEmpty(path))
            return;

        string cacheKey = BuildVoiceKey(branch, path);
        if (_voiceClipCache.TryGetValue(cacheKey, out AudioClip cached) && cached != null)
        {
            _voiceAudioSource.PlayOneShot(cached, 1f);
            return;
        }

        _voiceRunner.StartCoroutine(LoadAndPlayVoiceCoroutine(branch, path, cacheKey));
    }

    private static IEnumerator LoadAndPlayVoiceCoroutine(BiscordDialogueBranch branch, string fullPath, string cacheKey)
    {
        string normalized = fullPath.Replace("\\", "/");
        if (!normalized.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
            normalized = "file:///" + normalized;

        WWW www = new WWW(normalized);
        yield return www;

        if (!string.IsNullOrEmpty(www.error))
            yield break;

        AudioClip clip = www.GetAudioClip(false, false, AudioType.WAV);
        if (clip == null)
            yield break;

        clip.name = Path.GetFileNameWithoutExtension(fullPath);
        _voiceClipCache[cacheKey] = clip;
        if (_voiceAudioSource != null)
            _voiceAudioSource.PlayOneShot(clip, 1f);
    }

    private static string ResolveVoicePath(BiscordDialogueBranch branch, string phrase)
    {
        if (TryResolveVoicePathByPhrase(branch, phrase, out string path))
            return path;
        return null;
    }

    private static string BuildVoiceKey(BiscordDialogueBranch branch, string phrase)
    {
        return ((int)branch).ToString() + ":" + (phrase ?? string.Empty);
    }

    private static string NormalizeVoicePhrase(string phrase)
    {
        if (string.IsNullOrEmpty(phrase))
            return string.Empty;

        string s = phrase.Trim().ToLowerInvariant();
        s = s.Replace("...", " ");
        s = s.Replace("\u2026", " ");
        s = s.Replace("\"", "");
        s = s.Replace("?", "");
        s = s.Replace("!", "");
        s = s.Replace(",", "");
        s = s.Replace(".", "");
        s = s.Replace(";", "");
        s = s.Replace("'", "");
        s = s.Replace("(", "");
        s = s.Replace(")", "");
        s = s.Replace("[", "");
        s = s.Replace("]", "");
        s = s.Replace("{", "");
        s = s.Replace("}", "");
        s = s.Replace("-", " ");
        s = s.Replace("_", " ");
        s = s.Replace(":", "");
        s = s.Replace("*", "");
        s = s.Replace("<", "");
        s = s.Replace(">", "");
        s = s.Replace("|", "");
        s = s.Replace("/", " ");
        s = s.Replace("\\", " ");
        s = Regex.Replace(s, "\\s+", " ").Trim();
        return s;
    }

    private static void RegisterVoicePath(BiscordDialogueBranch branch, string name, string fullPath)
    {
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(fullPath))
            return;

        AddVoicePathIfMissing(branch, name, fullPath);

        string normalized = NormalizeVoicePhrase(name);
        AddVoicePathIfMissing(branch, normalized, fullPath);

        string compact = normalized.Replace(" ", string.Empty);
        AddVoicePathIfMissing(branch, compact, fullPath);
    }

    private static void AddVoicePathIfMissing(BiscordDialogueBranch branch, string keyPhrase, string fullPath)
    {
        if (string.IsNullOrEmpty(keyPhrase))
            return;

        string key = BuildVoiceKey(branch, keyPhrase);
        if (!_voicePathByKey.ContainsKey(key))
            _voicePathByKey[key] = fullPath;
    }

    private static bool TryResolveVoicePathByPhrase(BiscordDialogueBranch branch, string phrase, out string path)
    {
        string directKey = BuildVoiceKey(branch, phrase);
        if (_voicePathByKey.TryGetValue(directKey, out path))
            return true;

        string normalizedPhrase = NormalizeVoicePhrase(phrase);
        string normalizedKey = BuildVoiceKey(branch, normalizedPhrase);
        if (_voicePathByKey.TryGetValue(normalizedKey, out path))
            return true;

        string compactPhrase = normalizedPhrase.Replace(" ", string.Empty);
        string compactKey = BuildVoiceKey(branch, compactPhrase);
        if (_voicePathByKey.TryGetValue(compactKey, out path))
            return true;

        return false;
    }

    private static string GetBiscordVoiceRoot()
    {
        try
        {
            string gameRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(Path.Combine(gameRoot, "sources"), @"HellGate_sources\BiscordSounds");
        }
        catch
        {
            return null;
        }
    }

    private static Color ParseColor(string content, string key, Color fallback)
    {
        Match colorMatch = Regex.Match(content, $"\"{key}\"\\s*:\\s*\\{{([\\s\\S]*?)\\}}");
        if (!colorMatch.Success)
            return fallback;

        string colorContent = colorMatch.Groups[1].Value;
        float r = fallback.r;
        float g = fallback.g;
        float b = fallback.b;
        float a = fallback.a;

        Match rMatch = Regex.Match(colorContent, "\"r\"\\s*:\\s*([0-9.]+)");
        if (rMatch.Success) float.TryParse(rMatch.Groups[1].Value, out r);
        Match gMatch = Regex.Match(colorContent, "\"g\"\\s*:\\s*([0-9.]+)");
        if (gMatch.Success) float.TryParse(gMatch.Groups[1].Value, out g);
        Match bMatch = Regex.Match(colorContent, "\"b\"\\s*:\\s*([0-9.]+)");
        if (bMatch.Success) float.TryParse(bMatch.Groups[1].Value, out b);
        Match aMatch = Regex.Match(colorContent, "\"a\"\\s*:\\s*([0-9.]+)");
        if (aMatch.Success) float.TryParse(aMatch.Groups[1].Value, out a);

        return new Color(r, g, b, a);
    }
}

internal sealed class BiscordVoiceRunner : MonoBehaviour
{
}
