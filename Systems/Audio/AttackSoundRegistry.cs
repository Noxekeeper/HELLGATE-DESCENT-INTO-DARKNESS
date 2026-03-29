using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NoREroMod.Systems.Audio;

internal enum AttackSoundCategory
{
    Regular,
    Power,
    Threat,
    Death
}

internal static class AttackSoundRegistry
{
    private static readonly Dictionary<AttackSoundCategory, List<AudioClip>> _clipsByCategory = new Dictionary<AttackSoundCategory, List<AudioClip>>();
    private static readonly Dictionary<AttackSoundCategory, int> _lastPlayedIndex = new Dictionary<AttackSoundCategory, int>();
    private static readonly HashSet<string> _humanRuntimeAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> _threatRuntimeAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, List<AudioClip>> _threatClipsByPhrase = new Dictionary<string, List<AudioClip>>(StringComparer.OrdinalIgnoreCase);

    internal static HashSet<string> HumanPrefabs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    /// <summary>Prefabs that can show grab threats and play threat sounds. Loaded from ThreatsPrephabsHuman.txt next to language-specific threat WAV folders (ThreatsEN, etc.).</summary>
    internal static HashSet<string> ThreatPrefabs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    /// <summary>Prefabs that play death sounds. Loaded from Death/HumansPrephabs.txt</summary>
    internal static HashSet<string> DeathPrefabs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> _deathRuntimeAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    internal static void Clear()
    {
        _clipsByCategory.Clear();
        _lastPlayedIndex.Clear();
        _threatClipsByPhrase.Clear();
        HumanPrefabs.Clear();
        _humanRuntimeAliases.Clear();
        ThreatPrefabs.Clear();
        _threatRuntimeAliases.Clear();
        DeathPrefabs.Clear();
        _deathRuntimeAliases.Clear();
    }

    internal static void AddDeathPrefab(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName)) return;
        string key = prefabName.Trim();
        DeathPrefabs.Add(key);
        RegisterDeathAlias(key);
    }

    private static void RegisterDeathAlias(string configKey)
    {
        _deathRuntimeAliases.Add(configKey);
        switch (configKey)
        {
            case "Dorei":
                _deathRuntimeAliases.Add("SinnerslaveCrossbow");
                break;
            case "Inquisition":
                _deathRuntimeAliases.Add("InquisitionBlack");
                break;
            case "InquisitionRED":
                _deathRuntimeAliases.Add("Inquisition_RED");
                break;
            case "InquisitionWhite":
                _deathRuntimeAliases.Add("Inquisition_white");
                break;
            case "Mafia":
                _deathRuntimeAliases.Add("mafia_spine");
                break;
            case "Mafiamuscle":
            case "MafiaBossCustom":
            case "BlackMafia":
                _deathRuntimeAliases.Add("mafia_muscle");
                break;
            case "OtherSlavebigAxe":
                _deathRuntimeAliases.Add("Axe");
                break;
            case "SlaveBigAxe":
                _deathRuntimeAliases.Add("SlaveBigAxe");
                break;
            case "TouzokuAxe":
                _deathRuntimeAliases.Add("Touzoku_Axe");
                break;
            case "TouzokuNormal":
                _deathRuntimeAliases.Add("Touzoku");
                break;
            case "Vagrant":
                _deathRuntimeAliases.Add("Vagrant_spine");
                break;
            case "VagrantGuard":
                _deathRuntimeAliases.Add("Vagrant_Guard_spine");
                break;
            case "VagrantThrow":
                _deathRuntimeAliases.Add("Vagrant_Throw_spine");
                break;
        }
    }

    internal static bool IsDeathEnabledPrefab(EnemyDate enemy)
    {
        if (enemy == null || enemy.gameObject == null) return false;
        string goName = enemy.gameObject.name;
        if (string.IsNullOrEmpty(goName)) return false;
        string normalized = NormalizeName(goName);
        if (DeathPrefabs.Contains(normalized)) return true;
        if (_deathRuntimeAliases.Contains(normalized)) return true;
        foreach (string alias in _deathRuntimeAliases)
        {
            if (normalized.IndexOf(alias, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }

    internal static void AddThreatPrefab(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName)) return;
        string key = prefabName.Trim();
        ThreatPrefabs.Add(key);
        RegisterThreatAliasesForConfigKey(key);
    }

    internal static bool IsThreatEnabledPrefab(EnemyDate enemy)
    {
        if (enemy == null || enemy.gameObject == null) return false;
        string goName = enemy.gameObject.name;
        if (string.IsNullOrEmpty(goName)) return false;

        string normalized = NormalizeName(goName);
        if (ThreatPrefabs.Contains(normalized)) return true;
        if (_threatRuntimeAliases.Contains(normalized)) return true;

        foreach (string alias in _threatRuntimeAliases)
        {
            if (normalized.IndexOf(alias, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }

    private static void RegisterThreatAliasesForConfigKey(string configKey)
    {
        _threatRuntimeAliases.Add(configKey);
        switch (configKey)
        {
            case "Dorei":
                _threatRuntimeAliases.Add("SinnerslaveCrossbow");
                _threatRuntimeAliases.Add("Dorei");
                break;
            case "Mafiamuscle":
            case "MafiaBossCustom":
            case "BlackMafia":
            case "Mafia":
                _threatRuntimeAliases.Add("mafia_muscle");
                break;
            case "OtherSlavebigAxe":
                _threatRuntimeAliases.Add("Axe");
                break;
            case "SlaveBigAxe":
                _threatRuntimeAliases.Add("SlaveBigAxe");
                break;
            case "TouzokuAxe":
                _threatRuntimeAliases.Add("Touzoku_Axe");
                break;
            case "TouzokuNormal":
                _threatRuntimeAliases.Add("Touzoku");
                break;
            case "Vagrant":
                _threatRuntimeAliases.Add("Vagrant_spine");
                break;
            case "VagrantGuard":
                _threatRuntimeAliases.Add("Vagrant_Guard_spine");
                break;
            case "VagrantThrow":
                _threatRuntimeAliases.Add("Vagrant_Throw_spine");
                break;
        }
    }

    internal static void AddHumanPrefab(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName)) return;
        string key = prefabName.Trim();
        HumanPrefabs.Add(key);
        RegisterRuntimeAliasesForConfigKey(key);
    }

    internal static void AddClip(AttackSoundCategory category, AudioClip clip)
    {
        if (clip == null) return;

        List<AudioClip> list;
        if (!_clipsByCategory.TryGetValue(category, out list))
        {
            list = new List<AudioClip>();
            _clipsByCategory[category] = list;
        }

        list.Add(clip);

        if (category == AttackSoundCategory.Threat && !string.IsNullOrEmpty(clip.name))
        {
            string phraseKey = NormalizeThreatPhraseKey(clip.name);
            if (!string.IsNullOrEmpty(phraseKey))
            {
                if (!_threatClipsByPhrase.TryGetValue(phraseKey, out var phraseList))
                {
                    phraseList = new List<AudioClip>();
                    _threatClipsByPhrase[phraseKey] = phraseList;
                }
                phraseList.Add(clip);
            }
        }
    }

    private static string NormalizeThreatPhraseKey(string fileNameWithoutExt)
    {
        if (string.IsNullOrEmpty(fileNameWithoutExt)) return string.Empty;
        string s = fileNameWithoutExt.Trim();
        while (s.Length > 0 && s[0] == '[')
        {
            int bracketEnd = s.IndexOf(']');
            if (bracketEnd < 0) break;
            s = s.Substring(bracketEnd + 1).Trim();
        }
        if (s.EndsWith(".1") || s.EndsWith(".2") || s.EndsWith(".3"))
            s = s.Substring(0, s.Length - 2).Trim();
        if (s.Length >= 2 && s[s.Length - 2] == '!' && char.IsDigit(s[s.Length - 1]))
            s = s.Substring(0, s.Length - 1).Trim();
        while (s.EndsWith(".."))
            s = s.Substring(0, s.Length - 1);
        s = s.Replace("...", "\u2026").Replace("\u2026\u2026", "\u2026");
        return s.Trim();
    }

    internal static AudioClip GetClipForPhrase(string phrase)
    {
        if (string.IsNullOrEmpty(phrase)) return null;
        string key = phrase.Trim();
        if (_threatClipsByPhrase.TryGetValue(key, out var list) && list != null && list.Count > 0)
            return list[UnityEngine.Random.Range(0, list.Count)];
        key = key.Replace("...", "\u2026");
        if (_threatClipsByPhrase.TryGetValue(key, out list) && list != null && list.Count > 0)
            return list[UnityEngine.Random.Range(0, list.Count)];
        key = NormalizeThreatPhraseKey(phrase);
        if (!string.IsNullOrEmpty(key) && _threatClipsByPhrase.TryGetValue(key, out list) && list != null && list.Count > 0)
            return list[UnityEngine.Random.Range(0, list.Count)];
        return null;
    }

    internal static bool HasAny(AttackSoundCategory category)
    {
        List<AudioClip> list;
        if (!_clipsByCategory.TryGetValue(category, out list)) return false;
        return list.Count > 0;
    }

    internal static AudioClip GetRandomClip(AttackSoundCategory category)
    {
        List<AudioClip> list;
        if (!_clipsByCategory.TryGetValue(category, out list) || list.Count == 0)
        {
            return null;
        }

        if (list.Count == 1)
        {
            _lastPlayedIndex[category] = 0;
            return list[0];
        }

        int previousIndex = -1;
        if (_lastPlayedIndex.ContainsKey(category))
        {
            previousIndex = _lastPlayedIndex[category];
        }

        int nextIndex = UnityEngine.Random.Range(0, list.Count);
        if (nextIndex == previousIndex)
        {
            nextIndex = (nextIndex + 1) % list.Count;
        }

        _lastPlayedIndex[category] = nextIndex;
        return list[nextIndex];
    }

    internal static bool IsHumanPrefab(EnemyDate attacker)
    {
        if (attacker == null || attacker.gameObject == null) return false;
        string goName = attacker.gameObject.name;
        if (string.IsNullOrEmpty(goName)) return false;

        string normalized = NormalizeName(goName);
        if (HumanPrefabs.Contains(normalized)) return true;
        if (_humanRuntimeAliases.Contains(normalized)) return true;

        // Some runtime names include extra suffixes/prefixes; allow partial alias match.
        foreach (string alias in _humanRuntimeAliases)
        {
            if (normalized.IndexOf(alias, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }

    private static string NormalizeName(string rawName)
    {
        if (string.IsNullOrEmpty(rawName)) return string.Empty;
        string result = rawName.Trim();
        const string cloneSuffix = "(Clone)";
        if (result.EndsWith(cloneSuffix, StringComparison.OrdinalIgnoreCase))
        {
            result = result.Substring(0, result.Length - cloneSuffix.Length).Trim();
        }
        return result;
    }

    private static void RegisterRuntimeAliasesForConfigKey(string configKey)
    {
        // Keep a direct alias too (useful if runtime == config key).
        _humanRuntimeAliases.Add(configKey);

        // Map config-level names to actual runtime prefab names (based on EnemyPrefabRegistry mappings).
        switch (configKey)
        {
            case "Dorei":
                _humanRuntimeAliases.Add("SinnerslaveCrossbow");
                _humanRuntimeAliases.Add("Dorei");
                break;
            case "Inquisition":
                _humanRuntimeAliases.Add("InquisitionBlack");
                _humanRuntimeAliases.Add("Inquisition");
                break;
            case "InquisitionRED":
                _humanRuntimeAliases.Add("Inquisition_RED");
                break;
            case "InquisitionWhite":
                _humanRuntimeAliases.Add("Inquisition_white");
                break;
            case "Mafiamuscle":
            case "MafiaBossCustom":
                _humanRuntimeAliases.Add("mafia_muscle");
                break;
            case "OtherSlavebigAxe":
                _humanRuntimeAliases.Add("Axe");
                break;
            case "SlaveBigAxe":
                _humanRuntimeAliases.Add("SlaveBigAxe");
                break;
            case "TouzokuAxe":
                _humanRuntimeAliases.Add("Touzoku_Axe");
                break;
            case "TouzokuNormal":
                _humanRuntimeAliases.Add("Touzoku");
                break;
            case "Vagrant":
                _humanRuntimeAliases.Add("Vagrant_spine");
                break;
            case "VagrantGuard":
                _humanRuntimeAliases.Add("Vagrant_Guard_spine");
                break;
            case "VagrantThrow":
                _humanRuntimeAliases.Add("Vagrant_Throw_spine");
                break;
        }
    }
}
