using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NoREroMod.Patches.Enemy.UndeadSkeletonsModCustom;
using UnityEngine;

namespace NoREroMod.Systems.Audio;

internal static class AttackSoundSystem
{
    private static AudioSource _audioSource;
    private static AudioSource _threatAudioSource;
    private static AudioSource _deathAudioSource;
    private static int _currentThreatEnemyId;
    private static AttackSoundCoroutineRunner _runner;
    private static bool _initialized;
    private static float _lastPlayTime;
    private static float _lastGlobalDeathSoundTime = -999f;
    private static readonly Dictionary<int, float> _lastPlayTimeByAttacker = new Dictionary<int, float>();
    /// <summary>Lifetime latch: each enemy instance plays death voice at most once.</summary>
    private static readonly HashSet<int> _deathSoundLatchedEnemyIds = new HashSet<int>();
    private const float DeathSoundGlobalGapSeconds = 0.35f;

    private static float _lastGlobalThreatSoundTime = -999f;
    private static readonly Dictionary<int, float> _lastThreatTimeByEnemy = new Dictionary<int, float>();
    private static readonly List<ThreatSoundQueued> _threatSoundQueue = new List<ThreatSoundQueued>();
    private const int MAX_QUEUED_THREATS = 3;

    private struct ThreatSoundQueued
    {
        public float PlayAt;
        public AudioClip Clip;
        public int EnemyId;
    }

    internal static void Initialize(MonoBehaviour owner)
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            CreateAudioObjects(owner);
            AttackSoundRegistry.Clear();
            RegisterDefaultHumanPrefabs();
            LoadHumanPrefabList();
            LoadThreatPrefabList();
            RegisterBigAxeThreatPrefabs();
            LoadDeathPrefabList();
            _runner.StartCoroutine(LoadAllWavClipsCoroutine());
            _runner.StartCoroutine(ThreatSoundQueueProcessorCoroutine());
            Plugin.Log?.LogInfo("[AttackSoundSystem] Initialized");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[AttackSoundSystem] Init failed: " + ex.Message);
        }
    }

    internal static void Cleanup()
    {
        _lastPlayTimeByAttacker.Clear();
        _lastThreatTimeByEnemy.Clear();
        _deathSoundLatchedEnemyIds.Clear();
        _threatSoundQueue.Clear();
        _lastPlayTime = 0f;
        _lastGlobalDeathSoundTime = -999f;
        AttackSoundRegistry.Clear();
        _initialized = false;
    }

    /// <summary>
    /// Play threat sound matching the displayed phrase. Called from GrabThreatDialogues after ShowThreat.
    /// If global cooldown blocks, queues the sound to play later so each phrase gets voiced in turn.
    /// </summary>
    internal static void TryPlayThreatSound(EnemyDate enemy, string displayedPhrase)
    {
        if (!(_initialized && Plugin.enableAttackSounds != null && Plugin.enableAttackSounds.Value)) return;
        if (Plugin.enableThreatSounds != null && !Plugin.enableThreatSounds.Value) return;
        if (_threatAudioSource == null || enemy == null) return;
        if (!AttackSoundRegistry.IsThreatEnabledPrefab(enemy)) return;
        if (NoREroMod.Systems.Dialogue.GrabThreatDialogues.ShouldSuppressCombatThreats(enemy)) return;

        float now = Time.time;
        float globalCd = Plugin.threatSoundsGlobalCooldown != null ? Mathf.Max(0.5f, Plugin.threatSoundsGlobalCooldown.Value) : 2.5f;
        float perEnemyCd = Plugin.threatSoundsPerEnemyCooldown != null ? Mathf.Max(2f, Plugin.threatSoundsPerEnemyCooldown.Value) : 10f;
        int enemyId = enemy.GetInstanceID();

        if (_lastThreatTimeByEnemy.TryGetValue(enemyId, out float lastTime) && (now - lastTime) < perEnemyCd)
            return;

        AudioClip clip = !string.IsNullOrEmpty(displayedPhrase)
            ? AttackSoundRegistry.GetClipForPhrase(displayedPhrase)
            : null;
        // No random fallback: wrong-language text must not play an unrelated clip.
        if (clip == null) return;

        // Only play if global CD passed. Do NOT queue - queued sounds play without visible text (desync).
        if (now - _lastGlobalThreatSoundTime >= globalCd)
        {
            PlayThreatSoundNow(clip, enemyId, now);
        }
        // else: skip - keeps text and sound 1:1 synchronized
    }

    private static void PlayThreatSoundNow(AudioClip clip, int enemyId, float now)
    {
        if (_threatAudioSource == null || clip == null) return;
        float volume = Plugin.threatSoundsVolume != null ? Mathf.Clamp01(Plugin.threatSoundsVolume.Value) : 0.9f;
        _threatAudioSource.Stop();
        _threatAudioSource.clip = clip;
        _threatAudioSource.volume = volume;
        _threatAudioSource.Play();
        _currentThreatEnemyId = enemyId;
        _lastGlobalThreatSoundTime = now;
        _lastThreatTimeByEnemy[enemyId] = now;
    }

    private static IEnumerator ThreatSoundQueueProcessorCoroutine()
    {
        var wait = new WaitForSeconds(0.25f);
        while (true)
        {
            yield return wait;
            if (!_initialized || _audioSource == null) continue;
            if (_threatAudioSource != null && !_threatAudioSource.isPlaying && _currentThreatEnemyId != 0)
                _currentThreatEnemyId = 0;
            float now = Time.time;
            while (_threatSoundQueue.Count > 0 && now >= _threatSoundQueue[0].PlayAt)
            {
                var item = _threatSoundQueue[0];
                _threatSoundQueue.RemoveAt(0);
                PlayThreatSoundNow(item.Clip, item.EnemyId, now);
                now = Time.time;
            }
        }
    }

    internal static void TryPlayForHit(EnemyDate attacker, int kickbackkind, bool wasRanged)
    {
        if (!(_initialized && Plugin.enableAttackSounds != null && Plugin.enableAttackSounds.Value)) return;
        if (_audioSource == null) return;
        if (attacker == null) return;
        if (wasRanged) return;
        if (!AttackSoundRegistry.IsHumanPrefab(attacker)) return;

        float now = Time.unscaledTime;

        float globalInterval = Plugin.attackSoundsGlobalInterval != null ? Mathf.Max(0.04f, Plugin.attackSoundsGlobalInterval.Value) : 0.12f;
        if (now - _lastPlayTime < globalInterval) return;

        float perAttackerInterval = Plugin.attackSoundsPerAttackerInterval != null ? Mathf.Max(0.05f, Plugin.attackSoundsPerAttackerInterval.Value) : 0.2f;
        int attackerId = attacker.GetInstanceID();
        if (_lastPlayTimeByAttacker.TryGetValue(attackerId, out float attackerLast))
        {
            if (now - attackerLast < perAttackerInterval) return;
        }

        AttackSoundCategory category = IsPowerAttack(kickbackkind) ? AttackSoundCategory.Power : AttackSoundCategory.Regular;
        AudioClip clip = AttackSoundRegistry.GetRandomClip(category);
        if (clip == null) return;

        float volume = Plugin.attackSoundsVolume != null ? Mathf.Clamp01(Plugin.attackSoundsVolume.Value) : 0.85f;
        _audioSource.PlayOneShot(clip, volume);
        _lastPlayTime = now;
        _lastPlayTimeByAttacker[attackerId] = now;
    }

    private static bool IsPowerAttack(int kickbackkind)
    {
        return kickbackkind == 3 || kickbackkind == 4 || kickbackkind == 6;
    }

    private static void CreateAudioObjects(MonoBehaviour owner)
    {
        GameObject go = new GameObject("AttackSoundSystem_XUAIGNORE");
        UnityEngine.Object.DontDestroyOnLoad(go);

        _audioSource = go.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.loop = false;
        _audioSource.spatialBlend = 0f;

        _threatAudioSource = go.AddComponent<AudioSource>();
        _threatAudioSource.playOnAwake = false;
        _threatAudioSource.loop = false;
        _threatAudioSource.spatialBlend = 0f;

        // Dedicated channel: Stop+Play so death voices never PlayOneShot-stack into a folder loop.
        _deathAudioSource = go.AddComponent<AudioSource>();
        _deathAudioSource.playOnAwake = false;
        _deathAudioSource.loop = false;
        _deathAudioSource.spatialBlend = 0f;

        _runner = go.AddComponent<AttackSoundCoroutineRunner>();
        _runner.SetOwner(owner);
    }

    private static string GetHellGateSourcesPath()
    {
        try
        {
            string gameRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(Path.Combine(gameRoot, "sources"), "HellGate_sources");
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Per-language threat WAV folder under AttackSounds/Human/, e.g. ThreatsEN, ThreatsRU (matches HellGateJson language codes).
    /// </summary>
    private static string GetThreatsSubfolderName()
    {
        string code = Plugin.hellGateLanguage?.Value;
        if (string.IsNullOrEmpty(code))
            code = "EN";
        else
        {
            code = code.Trim();
            if (code.Length == 0)
                code = "EN";
        }
        return "Threats" + code.ToUpperInvariant();
    }

    private static void LoadHumanPrefabList()
    {
        string basePath = GetHellGateSourcesPath();
        if (string.IsNullOrEmpty(basePath)) return;

        string humanDir = Path.Combine(Path.Combine(basePath, "AttackSounds"), "Human");
        string pathTypo = Path.Combine(humanDir, "HumansPrephabs.txt");
        string pathCorrect = Path.Combine(humanDir, "HumansPrefabs.txt");
        string path = File.Exists(pathTypo) ? pathTypo : pathCorrect;

        if (!File.Exists(path))
        {
            Plugin.Log?.LogWarning("[AttackSoundSystem] Humans prefab list not found. Checked: " + pathTypo + " and " + pathCorrect);
            return;
        }

        try
        {
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrEmpty(line)) continue;
                string trimmed = line.Trim();
                if (trimmed.Length == 0) continue;
                if (trimmed.StartsWith("#")) continue;
                AttackSoundRegistry.AddHumanPrefab(trimmed);
            }

            Plugin.Log?.LogInfo("[AttackSoundSystem] Loaded human prefab names: " + AttackSoundRegistry.HumanPrefabs.Count + " from " + Path.GetFileName(path));
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[AttackSoundSystem] Failed to read human prefab list: " + ex.Message);
        }
    }

    private static void LoadThreatPrefabList()
    {
        string basePath = GetHellGateSourcesPath();
        if (string.IsNullOrEmpty(basePath)) return;

        string humanRoot = Path.Combine(Path.Combine(basePath, "AttackSounds"), "Human");
        string threatsDir = Path.Combine(humanRoot, GetThreatsSubfolderName());
        string path = Path.Combine(threatsDir, "ThreatsPrephabsHuman.txt");
        if (!File.Exists(path))
        {
            string enFallback = Path.Combine(Path.Combine(humanRoot, "ThreatsEN"), "ThreatsPrephabsHuman.txt");
            if (File.Exists(enFallback))
                path = enFallback;
        }

        if (!File.Exists(path))
        {
            RegisterDefaultThreatPrefabs();
            Plugin.Log?.LogWarning("[AttackSoundSystem] Threat prefab list not found, using defaults: " + path);
            return;
        }

        try
        {
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrEmpty(line)) continue;
                string trimmed = line.Trim();
                if (trimmed.Length == 0) continue;
                if (trimmed.StartsWith("#")) continue;
                AttackSoundRegistry.AddThreatPrefab(trimmed);
            }

            Plugin.Log?.LogInfo("[AttackSoundSystem] Loaded threat prefab names: " + AttackSoundRegistry.ThreatPrefabs.Count + " from ThreatsPrephabsHuman.txt");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[AttackSoundSystem] Failed to read threat prefab list: " + ex.Message);
        }
    }

    /// <summary>
    /// Big axe enemies use the BigSlaveAxe phrase pack only; not listed in ThreatsPrephabsHuman.txt with other humans.
    /// </summary>
    private static void RegisterBigAxeThreatPrefabs()
    {
        AttackSoundRegistry.AddThreatPrefab("SlaveBigAxe");
        AttackSoundRegistry.AddThreatPrefab("OtherSlavebigAxe");
    }

    private static IEnumerator LoadAllWavClipsCoroutine()
    {
        string basePath = GetHellGateSourcesPath();
        if (string.IsNullOrEmpty(basePath)) yield break;

        string root = Path.Combine(Path.Combine(basePath, "AttackSounds"), "Human");
        yield return LoadCategoryCoroutine(Path.Combine(root, "RegularAttack"), AttackSoundCategory.Regular);
        yield return LoadCategoryCoroutine(Path.Combine(root, "PowerAttack"), AttackSoundCategory.Power);
        string threatFolder = Path.Combine(root, GetThreatsSubfolderName());
        yield return LoadCategoryCoroutine(threatFolder, AttackSoundCategory.Threat, SearchOption.AllDirectories);
        yield return LoadCategoryCoroutine(Path.Combine(root, "Death"), AttackSoundCategory.Death);

        string undeadRoot = Path.Combine(Path.Combine(basePath, "AttackSounds"), "Undead");
        string undeadBoneDeath = Path.Combine(Path.Combine(undeadRoot, "Death"), "BoneDeath");
        yield return LoadCategoryCoroutine(undeadBoneDeath, AttackSoundCategory.UndeadDeath);
        // Fallback pool if BoneDeath is empty / only unsupported formats: Undead/*.wav|mp3
        if (AttackSoundRegistry.GetClipCount(AttackSoundCategory.UndeadDeath) == 0)
            yield return LoadCategoryCoroutine(undeadRoot, AttackSoundCategory.UndeadDeath);
    }

    private static void LoadDeathPrefabList()
    {
        string basePath = GetHellGateSourcesPath();
        if (string.IsNullOrEmpty(basePath)) return;

        string deathDir = Path.Combine(Path.Combine(Path.Combine(basePath, "AttackSounds"), "Human"), "Death");
        string path = Path.Combine(deathDir, "HumansPrephabs.txt");

        if (!File.Exists(path))
        {
            RegisterDefaultDeathPrefabs();
            Plugin.Log?.LogWarning("[AttackSoundSystem] Death prefab list not found, using defaults: " + path);
            return;
        }

        try
        {
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrEmpty(line)) continue;
                string trimmed = line.Trim();
                if (trimmed.Length == 0) continue;
                if (trimmed.StartsWith("#")) continue;
                AttackSoundRegistry.AddDeathPrefab(trimmed);
            }
            Plugin.Log?.LogInfo("[AttackSoundSystem] Loaded death prefab names: " + AttackSoundRegistry.DeathPrefabs.Count + " from Death/HumansPrephabs.txt");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[AttackSoundSystem] Failed to read death prefab list: " + ex.Message);
            RegisterDefaultDeathPrefabs();
        }
    }

    private static void RegisterDefaultDeathPrefabs()
    {
        string[] defaults = new[]
        {
            "Dorei", "Inquisition", "InquisitionRED", "InquisitionWhite", "BlackMafia", "Mafia", "Mafiamuscle", "MafiaBossCustom",
            "OtherSlavebigAxe", "SlaveBigAxe", "TouzokuAxe", "TouzokuNormal", "Vagrant", "VagrantGuard", "VagrantThrow"
        };
        foreach (string name in defaults)
            AttackSoundRegistry.AddDeathPrefab(name);
    }

    /// <summary>
    /// Play death sound when enemy dies (setanimation("DEATH")). Called from DeathSoundPatch.
    /// Humans: AttackSounds/Human/Death. Undead skeletons: AttackSounds/Undead/Death/BoneDeath.
    /// Once per enemy lifetime; dedicated AudioSource so voices never stack into a folder loop.
    /// </summary>
    internal static void TryPlayDeathSound(EnemyDate enemy)
    {
        if (!(_initialized && Plugin.enableAttackSounds != null && Plugin.enableAttackSounds.Value)) return;
        if (Plugin.enableDeathSounds != null && !Plugin.enableDeathSounds.Value) return;
        if (_deathAudioSource == null || enemy == null) return;

        string typeName = enemy.GetType() != null ? enemy.GetType().Name : string.Empty;
        string goName = enemy.gameObject != null ? enemy.gameObject.name : string.Empty;
        bool undead = IsUndeadDeathSource(enemy, goName, typeName);
        if (!undead && !AttackSoundRegistry.IsDeathEnabledPrefab(enemy)) return;

        int enemyId = enemy.GetInstanceID();
        // Lifetime latch — DEATH anim re-fires every frame / after 2s would otherwise re-roll the folder.
        if (!_deathSoundLatchedEnemyIds.Add(enemyId))
            return;

        if (_threatAudioSource != null && _threatAudioSource.isPlaying && _currentThreatEnemyId == enemyId)
        {
            _threatAudioSource.Stop();
            _currentThreatEnemyId = 0;
        }

        AttackSoundCategory category = undead ? AttackSoundCategory.UndeadDeath : AttackSoundCategory.Death;
        AudioClip clip = AttackSoundRegistry.GetRandomClip(category);
        if (clip == null)
            return;

        float now = Time.unscaledTime;
        if (now - _lastGlobalDeathSoundTime < DeathSoundGlobalGapSeconds)
            return;

        float volume = Plugin.deathSoundsVolume != null ? Mathf.Clamp01(Plugin.deathSoundsVolume.Value * 2f) : 1f;
        _deathAudioSource.Stop();
        _deathAudioSource.clip = clip;
        _deathAudioSource.volume = volume;
        _deathAudioSource.loop = false;
        _deathAudioSource.Play();
        _lastGlobalDeathSoundTime = now;
    }

    /// <summary>
    /// Undead skeleton marker, or name/type that must never use Human/Death voices.
    /// </summary>
    private static bool IsUndeadDeathSource(EnemyDate enemy, string goName, string typeName)
    {
        if (UndeadSkeletonsRuntime.IsUndeadSkeleton(enemy))
            return true;
        if (!string.IsNullOrEmpty(goName) &&
            goName.IndexOf("Undead", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        if (!string.IsNullOrEmpty(typeName) &&
            (typeName.Equals("Undead", StringComparison.OrdinalIgnoreCase) ||
             typeName.Equals("Sisiruirui", StringComparison.OrdinalIgnoreCase) ||
             typeName.Equals("SkeltonOoze", StringComparison.OrdinalIgnoreCase)))
            return true;
        return false;
    }

    private static IEnumerator LoadCategoryCoroutine(string folderPath, AttackSoundCategory category, SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        if (!Directory.Exists(folderPath))
        {
            Plugin.Log?.LogWarning("[AttackSoundSystem] Folder not found: " + folderPath);
            yield break;
        }

        List<string> files = new List<string>();
        CollectAudioFiles(folderPath, "*.wav", searchOption, files);
        CollectAudioFiles(folderPath, "*.mp3", searchOption, files);

        int loadedCount = 0;
        for (int i = 0; i < files.Count; i++)
        {
            string filePath = files[i];
            if (string.IsNullOrEmpty(filePath)) continue;

            string ext = Path.GetExtension(filePath);
            bool isMp3 = ext != null && ext.Equals(".mp3", StringComparison.OrdinalIgnoreCase);
            bool isWav = ext != null && ext.Equals(".wav", StringComparison.OrdinalIgnoreCase);
            if (!isMp3 && !isWav)
                continue;

            string normalized = filePath.Replace("\\", "/");
            if (!normalized.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
            {
                normalized = "file:///" + normalized;
            }

            WWW www = new WWW(normalized);
            yield return www;

            if (!string.IsNullOrEmpty(www.error))
            {
                Plugin.Log?.LogWarning("[AttackSoundSystem] Failed to load audio: " + filePath + " (" + www.error + ")");
                continue;
            }

            AudioClip clip = null;
            if (isWav)
                clip = www.GetAudioClip(false, false, AudioType.WAV);
            else
            {
                // Some Unity builds reject MPEG; try UNKNOWN then MPEG.
                clip = www.GetAudioClip(false, false, AudioType.UNKNOWN);
                if (clip == null)
                    clip = www.GetAudioClip(false, false, AudioType.MPEG);
            }
            if (clip == null)
            {
                // Unity often returns null for .mp3 — skip quietly; empty-category note below covers it.
                continue;
            }

            clip.name = Path.GetFileNameWithoutExtension(filePath);
            AttackSoundRegistry.AddClip(category, clip);
            loadedCount++;
        }

        if (loadedCount == 0)
        {
            // Expected for folders with only unloadable .mp3 (e.g. Undead/BoneDeath) until wavs exist.
            Plugin.Log?.LogInfo("[AttackSoundSystem] No playable clips for " + category + " (" + folderPath + ")");
        }
        else
        {
            Plugin.Log?.LogInfo("[AttackSoundSystem] Loaded " + loadedCount + " clips for " + category);
        }
    }

    private static void CollectAudioFiles(string folderPath, string pattern, SearchOption searchOption, List<string> into)
    {
        try
        {
            string[] found = Directory.GetFiles(folderPath, pattern, searchOption);
            if (found == null) return;
            for (int i = 0; i < found.Length; i++)
            {
                if (!string.IsNullOrEmpty(found[i]))
                    into.Add(found[i]);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[AttackSoundSystem] Scan failed (" + pattern + ") in " + folderPath + ": " + ex.Message);
        }
    }

    private static void RegisterDefaultHumanPrefabs()
    {
        // Built-in baseline so system works even if HumansPrephabs.txt is missing/wrong.
        string[] defaults = new[]
        {
            "Dorei",
            "Inquisition",
            "InquisitionRED",
            "InquisitionWhite",
            "Mafiamuscle",
            "MafiaBossCustom",
            "OtherSlavebigAxe",
            "SlaveBigAxe",
            "TouzokuAxe",
            "TouzokuNormal",
            "Vagrant",
            "VagrantGuard",
            "VagrantThrow"
        };

        for (int i = 0; i < defaults.Length; i++)
        {
            AttackSoundRegistry.AddHumanPrefab(defaults[i]);
        }
    }

    private static void RegisterDefaultThreatPrefabs()
    {
        string[] defaults = new[]
        {
            "Dorei", "Mafiamuscle", "MafiaBossCustom", "BlackMafia", "Mafia",
            "TouzokuAxe", "TouzokuNormal", "Vagrant", "VagrantGuard", "VagrantThrow"
        };
        for (int i = 0; i < defaults.Length; i++)
        {
            AttackSoundRegistry.AddThreatPrefab(defaults[i]);
        }
    }

    private class AttackSoundCoroutineRunner : MonoBehaviour
    {
        internal void SetOwner(MonoBehaviour owner) { }
    }
}
