using System;
using System.Collections;
using System.IO;
using NoREroMod.Systems.Economy;
using UnityEngine;

namespace NoREroMod.Patches.HellTraps;

/// <summary>Loads and plays MindShock.wav during Take Vengeance shock sequence.</summary>
internal static class LethalTrapVengeanceShockAudio
{
    private static AudioClip _shockClip;
    private static bool _loadStarted;

    internal static void Initialize(MonoBehaviour host)
    {
        LethalTrapHeartBeatLoop.Initialize(host);

        if ((!Plugin.enableLethalMagicTrap.Value && !Plugin.enableLethalCocoonTrap.Value) ||
            host == null ||
            _loadStarted)
            return;

        if (!IsSoundEnabled())
            return;

        _loadStarted = true;
        host.StartCoroutine(LoadMindShockClipCoroutine());
    }

    internal static bool IsSoundEnabled()
    {
        return Plugin.lethalTrapVengeanceShockSoundEnable?.Value ?? true;
    }

    internal static void TryPlayMindShockSound()
    {
        if (!IsSoundEnabled())
            return;

        if (_shockClip == null)
            return;

        if (!LethalTrapVengeanceMindBrokenShock.IsFeatureEnabled())
            return;

        float volume = Mathf.Clamp01(Plugin.lethalTrapVengeanceShockMindShockVolume?.Value ?? 1f);
        if (volume <= 0.0001f)
            return;

        GoldAudioPlayer.Play2D(_shockClip, volume);
        Plugin.Log?.LogInfo(
            "[LethalTrapVengeanceShockAudio] Playing "
            + LethalTrapVengeanceShockTuning.ShockSoundFileName
            + " vol="
            + volume.ToString("0.##"));
    }

    internal static void StartHeartBeatLoop()
    {
        if (!LethalTrapVengeanceMindBrokenShock.IsFeatureEnabled())
            return;

        LethalTrapHeartBeatLoop.AcquireLoop();
    }

    internal static void StopHeartBeatLoop()
    {
        LethalTrapHeartBeatLoop.ReleaseLoop();
    }

    private static IEnumerator LoadMindShockClipCoroutine()
    {
        string directory = LethalMagicTrapPaths.ResolveVengeanceShockAudioDirectory();
        if (!Directory.Exists(directory))
        {
            Plugin.Log?.LogInfo(
                "[LethalTrapVengeanceShockAudio] Audio folder missing (optional): "
                + directory);
            yield break;
        }

        yield return LoadWavFromDirectory(
            directory,
            LethalTrapVengeanceShockTuning.ShockSoundFileName,
            clip => _shockClip = clip);
    }

    private static IEnumerator LoadWavFromDirectory(
        string directory,
        string fileName,
        Action<AudioClip> assignClip)
    {
        string path = Path.Combine(directory, fileName);
        if (!File.Exists(path))
        {
            Plugin.Log?.LogInfo(
                "[LethalTrapVengeanceShockAudio] Optional WAV not found: "
                + path);
            yield break;
        }

        string fileUri = new Uri(Path.GetFullPath(path)).AbsoluteUri;
        WWW www = new WWW(fileUri);
        yield return www;

        if (!string.IsNullOrEmpty(www.error))
        {
            Plugin.Log?.LogWarning(
                "[LethalTrapVengeanceShockAudio] Failed to load WAV: "
                + path
                + " ("
                + www.error
                + ")");
            yield break;
        }

        AudioClip clip = www.GetAudioClip(false, false, AudioType.WAV);
        if (clip == null)
        {
            Plugin.Log?.LogWarning(
                "[LethalTrapVengeanceShockAudio] GetAudioClip returned null: "
                + path);
            yield break;
        }

        clip.name = Path.GetFileNameWithoutExtension(path);

        int wait = 0;
        while (clip.loadState == AudioDataLoadState.Loading && wait++ < 600)
            yield return null;

        if (clip.loadState != AudioDataLoadState.Loaded)
        {
            Plugin.Log?.LogWarning(
                "[LethalTrapVengeanceShockAudio] Clip did not finish loading: "
                + path
                + " state="
                + clip.loadState);
            yield break;
        }

        assignClip(clip);
        Plugin.Log?.LogInfo("[LethalTrapVengeanceShockAudio] Loaded WAV: " + fileName);
    }
}
