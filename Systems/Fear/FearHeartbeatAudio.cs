using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoREroMod.Systems.Fear;

/// <summary>
/// Loads <c>sources/HellGate_sources/Fear/HeartBeat.wav</c> (fallback CustomDeath).
/// </summary>
internal static class FearHeartbeatAudio
{
    private const string RelativeFearDir = "sources/HellGate_sources/Fear";
    private const string RelativeFallbackDir = "sources/HellGate_sources/CustomDeath";
    private const string FileName = "HeartBeat.wav";
    private const string OneShotHostName = "FearHeartbeatOneShot_XUAIGNORE";
    private const string LoopHostName = "FearHeartbeatLoop_XUAIGNORE";

    private static AudioClip _clip;
    private static bool _loadStarted;
    private static GameObject _oneShotHost;
    private static AudioSource _oneShotSource;
    private static GameObject _loopHost;
    private static AudioSource _loopSource;
    private static bool _loopPlaying;

    internal static void Initialize(MonoBehaviour host)
    {
        if (host == null || _loadStarted)
            return;
        _loadStarted = true;
        host.StartCoroutine(LoadClipCoroutine());
    }

    internal static bool IsReady => _clip != null;

    internal static void PlayOneShot()
    {
        if (_clip == null)
            return;
        float vol = Mathf.Clamp01(FearConfig.HeartbeatVolume?.Value ?? 0.85f);
        if (vol <= 0.0001f)
            return;

        EnsureOneShotSource();
        if (_oneShotSource == null)
            return;

        // WWW-loaded clips are more reliable with Play() than PlayOneShot on some Unity 5 builds.
        _oneShotSource.Stop();
        _oneShotSource.clip = _clip;
        _oneShotSource.loop = false;
        _oneShotSource.volume = vol;
        _oneShotSource.Play();
    }

    internal static void StartLoop()
    {
        if (_clip == null || _loopPlaying)
            return;
        float vol = Mathf.Clamp01(FearConfig.HeartbeatVolume?.Value ?? 0.85f);
        if (vol <= 0.0001f)
            return;

        StopLoopInternal();

        _loopHost = new GameObject(LoopHostName);
        Object.DontDestroyOnLoad(_loopHost);
        _loopHost.hideFlags = HideFlags.HideAndDontSave;

        _loopSource = _loopHost.AddComponent<AudioSource>();
        _loopSource.clip = _clip;
        _loopSource.spatialBlend = 0f;
        _loopSource.playOnAwake = false;
        _loopSource.loop = true;
        _loopSource.priority = 0;
        _loopSource.volume = vol;
        _loopSource.bypassEffects = true;
        _loopSource.bypassListenerEffects = true;
        _loopSource.bypassReverbZones = true;
        _loopSource.Play();
        _loopPlaying = true;
    }

    internal static void StopLoop()
    {
        if (!_loopPlaying)
            return;
        StopLoopInternal();
        _loopPlaying = false;
    }

    internal static void StopAll()
    {
        StopLoop();
        if (_oneShotSource != null && _oneShotSource.isPlaying)
            _oneShotSource.Stop();
    }

    internal static bool IsLoopPlaying => _loopPlaying;

    internal static bool IsOneShotPlaying
    {
        get
        {
            try { return _oneShotSource != null && _oneShotSource.isPlaying; }
            catch { return false; }
        }
    }

    private static void EnsureOneShotSource()
    {
        if (_oneShotSource != null)
            return;

        _oneShotHost = new GameObject(OneShotHostName);
        Object.DontDestroyOnLoad(_oneShotHost);
        _oneShotHost.hideFlags = HideFlags.HideAndDontSave;
        _oneShotSource = _oneShotHost.AddComponent<AudioSource>();
        _oneShotSource.spatialBlend = 0f;
        _oneShotSource.playOnAwake = false;
        _oneShotSource.loop = false;
        _oneShotSource.priority = 0;
        _oneShotSource.bypassEffects = true;
        _oneShotSource.bypassListenerEffects = true;
        _oneShotSource.bypassReverbZones = true;
    }

    private static void StopLoopInternal()
    {
        if (_loopSource != null)
        {
            _loopSource.Stop();
            _loopSource = null;
        }

        if (_loopHost != null)
        {
            Object.Destroy(_loopHost);
            _loopHost = null;
        }
    }

    private static IEnumerator LoadClipCoroutine()
    {
        string path = ResolveHeartBeatPath();
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            Plugin.Log?.LogInfo("[FearHeartbeat] HeartBeat.wav not found under Fear/ or CustomDeath/.");
            yield break;
        }

        string fileUri = new Uri(Path.GetFullPath(path)).AbsoluteUri;
        WWW www = new WWW(fileUri);
        yield return www;

        if (!string.IsNullOrEmpty(www.error))
        {
            Plugin.Log?.LogWarning("[FearHeartbeat] Failed to load: " + path + " (" + www.error + ")");
            yield break;
        }

        AudioClip clip = www.GetAudioClip(false, false, AudioType.WAV);
        if (clip == null)
        {
            Plugin.Log?.LogWarning("[FearHeartbeat] GetAudioClip null: " + path);
            yield break;
        }

        clip.name = "Fear_HeartBeat";
        int wait = 0;
        while (clip.loadState == AudioDataLoadState.Loading && wait++ < 600)
            yield return null;

        if (clip.loadState != AudioDataLoadState.Loaded)
        {
            Plugin.Log?.LogWarning("[FearHeartbeat] Clip not loaded: " + path + " state=" + clip.loadState);
            yield break;
        }

        _clip = clip;
        Plugin.Log?.LogInfo("[FearHeartbeat] Loaded: " + path);
    }

    private static string ResolveHeartBeatPath()
    {
        string fearDir = ResolveRelativeDirectory(RelativeFearDir);
        string fearPath = Path.Combine(fearDir, FileName);
        if (File.Exists(fearPath))
            return fearPath;

        string fallbackDir = ResolveRelativeDirectory(RelativeFallbackDir);
        string fallbackPath = Path.Combine(fallbackDir, FileName);
        if (File.Exists(fallbackPath))
            return fallbackPath;

        return fearPath;
    }

    private static string ResolveRelativeDirectory(string rel)
    {
        rel = rel.Replace('/', Path.DirectorySeparatorChar).Trim(Path.DirectorySeparatorChar);
        string gameRoot = Application.dataPath;
        if (gameRoot.EndsWith("_Data"))
            gameRoot = gameRoot.Substring(0, gameRoot.Length - 5);

        var candidates = new List<string>(4);
        AddCandidate(candidates, Path.Combine(gameRoot, rel));
        AddCandidate(candidates, Path.Combine(Path.GetFullPath(Path.Combine(gameRoot, "..")), rel));

        for (int i = 0; i < candidates.Count; i++)
        {
            string full = Path.GetFullPath(candidates[i]);
            if (Directory.Exists(full))
                return full;
        }

        return Path.GetFullPath(candidates[0]);
    }

    private static void AddCandidate(List<string> list, string path)
    {
        if (string.IsNullOrEmpty(path))
            return;
        string full = Path.GetFullPath(path);
        for (int i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i], full, StringComparison.OrdinalIgnoreCase))
                return;
        }
        list.Add(full);
    }
}
