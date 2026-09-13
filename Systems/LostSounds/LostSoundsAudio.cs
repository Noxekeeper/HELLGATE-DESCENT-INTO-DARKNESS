using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NoREroMod.Systems.Economy;
using UnityEngine;

namespace NoREroMod.Systems.LostSounds;

/// <summary>
/// Generic WAV registry for LostSounds.
/// Loads every <c>*.wav</c> in the assets folder; play by file name without extension
/// (e.g. <c>Play("jump1")</c> for <c>jump1.wav</c>).
/// </summary>
internal static class LostSoundsAudio
{
    private static readonly Dictionary<string, AudioClip> Clips =
        new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);

    private static bool _loadStarted;
    private static bool _loadFinished;

    internal static bool IsReady => _loadFinished;

    internal static void Initialize(MonoBehaviour host)
    {
        if (_loadStarted || host == null)
            return;

        _loadStarted = true;
        host.StartCoroutine(LoadAllWavsCoroutine());
    }

    /// <summary>Play a loaded cue by file stem (no extension). No-op if missing / disabled volume.</summary>
    internal static void Play(string cueName, float volumeScale = 1f)
    {
        if (!_loadFinished || string.IsNullOrEmpty(cueName))
            return;

        if (!Clips.TryGetValue(cueName, out AudioClip clip) || clip == null)
        {
            LostSoundsConfig.LogDebug("[LostSounds] Play skipped — cue not loaded: " + cueName);
            return;
        }

        float master = LostSoundsConfig.MasterVolume != null ? LostSoundsConfig.MasterVolume.Value : 1f;
        float volume = Mathf.Clamp01(master * volumeScale);
        if (volume <= 0.001f)
            return;

        GoldAudioPlayer.Play2D(clip, volume);
        LostSoundsConfig.LogDebug("[LostSounds] Play '" + cueName + "' vol=" + volume.ToString("0.##"));
    }

    internal static bool HasCue(string cueName)
    {
        return !string.IsNullOrEmpty(cueName) && Clips.ContainsKey(cueName);
    }

    private static IEnumerator LoadAllWavsCoroutine()
    {
        string folder = LostSoundsPaths.ResolveAssetsDirectory();
        if (!Directory.Exists(folder))
        {
            Plugin.Log?.LogWarning("[LostSounds] Assets folder missing: " + folder);
            _loadFinished = true;
            yield break;
        }

        string[] wavFiles;
        try
        {
            wavFiles = Directory.GetFiles(folder, "*.wav", SearchOption.TopDirectoryOnly);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[LostSounds] Cannot list WAVs: " + ex.Message);
            _loadFinished = true;
            yield break;
        }

        int loaded = 0;
        for (int i = 0; i < wavFiles.Length; i++)
        {
            string filePath = wavFiles[i];
            string stem = Path.GetFileNameWithoutExtension(filePath);
            if (string.IsNullOrEmpty(stem))
                continue;

            string normalized = filePath.Replace("\\", "/");
            if (!normalized.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
                normalized = "file:///" + normalized;

            WWW www = new WWW(normalized);
            yield return www;

            if (!string.IsNullOrEmpty(www.error))
            {
                Plugin.Log?.LogWarning("[LostSounds] WAV load failed: " + filePath + " (" + www.error + ")");
                continue;
            }

            AudioClip clip = www.GetAudioClip(false, false, AudioType.WAV);
            if (clip == null)
                continue;

            clip.name = stem;
            Clips[stem] = clip;
            loaded++;
        }

        _loadFinished = true;
        if (loaded == 0)
            LostSoundsConfig.LogDebug("[LostSounds] No WAVs in " + folder + " (scaffold OK)");
        else
            Plugin.Log?.LogInfo("[LostSounds] Loaded " + loaded + " cue(s) from " + folder);
    }
}
