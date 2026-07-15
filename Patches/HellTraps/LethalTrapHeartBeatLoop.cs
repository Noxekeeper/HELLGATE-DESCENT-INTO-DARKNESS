using System;
using System.Collections;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoREroMod.Patches.HellTraps;

/// <summary>Shared loop for CustomDeath/HeartBeat.wav (vengeance shock + lethal trap thoughts).</summary>
internal static class LethalTrapHeartBeatLoop
{
    private const string LoopHostName = "LethalTrapHeartBeatLoop_XUAIGNORE";
    private const string HeartBeatFileName = "HeartBeat.wav";

    private static AudioClip _heartBeatClip;
    private static bool _loadStarted;
    private static GameObject _loopHost;
    private static AudioSource _loopSource;
    private static int _loopRefCount;

    internal static void Initialize(MonoBehaviour host)
    {
        if ((!Plugin.enableLethalMagicTrap.Value && !Plugin.enableLethalCocoonTrap.Value) ||
            host == null ||
            _loadStarted)
            return;

        _loadStarted = true;
        host.StartCoroutine(LoadHeartBeatClipCoroutine());
    }

    internal static bool CanPlay()
    {
        if (!(Plugin.lethalTrapVengeanceShockSoundEnable?.Value ?? true))
            return false;

        float volume = Mathf.Clamp01(Plugin.lethalTrapVengeanceShockHeartBeatVolume?.Value ?? 1f);
        return volume > 0.0001f && _heartBeatClip != null;
    }

    internal static void AcquireLoop()
    {
        if (!CanPlay())
            return;

        _loopRefCount++;
        if (_loopRefCount != 1)
            return;

        float volume = Mathf.Clamp01(Plugin.lethalTrapVengeanceShockHeartBeatVolume?.Value ?? 1f);
        StopLoopInternal();

        _loopHost = new GameObject(LoopHostName);
        Object.DontDestroyOnLoad(_loopHost);
        _loopHost.hideFlags = HideFlags.HideAndDontSave;

        _loopSource = _loopHost.AddComponent<AudioSource>();
        _loopSource.clip = _heartBeatClip;
        _loopSource.spatialBlend = 0f;
        _loopSource.playOnAwake = false;
        _loopSource.loop = true;
        _loopSource.priority = 0;
        _loopSource.volume = volume;
        _loopSource.bypassEffects = true;
        _loopSource.bypassListenerEffects = true;
        _loopSource.bypassReverbZones = true;
        _loopSource.outputAudioMixerGroup = null;
        _loopSource.Play();

        Plugin.Log?.LogInfo(
            "[LethalTrapHeartBeat] Loop started vol="
            + volume.ToString("0.##"));
    }

    internal static void ReleaseLoop()
    {
        if (_loopRefCount <= 0)
            return;

        _loopRefCount--;
        if (_loopRefCount > 0)
            return;

        StopLoopInternal();
        Plugin.Log?.LogInfo("[LethalTrapHeartBeat] Loop stopped.");
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

    private static IEnumerator LoadHeartBeatClipCoroutine()
    {
        string directory = LethalMagicTrapPaths.ResolveVengeanceShockAudioDirectory();
        if (!Directory.Exists(directory))
        {
            Plugin.Log?.LogInfo(
                "[LethalTrapHeartBeat] Audio folder missing (optional): "
                + directory);
            yield break;
        }

        string path = Path.Combine(directory, HeartBeatFileName);
        if (!File.Exists(path))
        {
            Plugin.Log?.LogInfo("[LethalTrapHeartBeat] Optional WAV not found: " + path);
            yield break;
        }

        string fileUri = new Uri(Path.GetFullPath(path)).AbsoluteUri;
        WWW www = new WWW(fileUri);
        yield return www;

        if (!string.IsNullOrEmpty(www.error))
        {
            Plugin.Log?.LogWarning(
                "[LethalTrapHeartBeat] Failed to load WAV: "
                + path
                + " ("
                + www.error
                + ")");
            yield break;
        }

        AudioClip clip = www.GetAudioClip(false, false, AudioType.WAV);
        if (clip == null)
        {
            Plugin.Log?.LogWarning("[LethalTrapHeartBeat] GetAudioClip returned null: " + path);
            yield break;
        }

        clip.name = Path.GetFileNameWithoutExtension(path);

        int wait = 0;
        while (clip.loadState == AudioDataLoadState.Loading && wait++ < 600)
            yield return null;

        if (clip.loadState != AudioDataLoadState.Loaded)
        {
            Plugin.Log?.LogWarning(
                "[LethalTrapHeartBeat] Clip did not finish loading: "
                + path
                + " state="
                + clip.loadState);
            yield break;
        }

        _heartBeatClip = clip;
        Plugin.Log?.LogInfo("[LethalTrapHeartBeat] Loaded WAV: " + HeartBeatFileName);
    }
}
