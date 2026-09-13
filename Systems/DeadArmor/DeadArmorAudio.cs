using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NoREroMod.Systems.Economy;
using UnityEngine;

namespace NoREroMod.Systems.DeadArmor;

/// <summary>Loads and plays a random WAV from DeadArmor/DeathSounds.</summary>
internal static class DeadArmorAudio
{
    private static readonly List<AudioClip> Clips = new List<AudioClip>(8);
    private static bool _loadStarted;
    private static bool _loadFinished;

    internal static void Initialize(MonoBehaviour host)
    {
        if (_loadStarted || host == null)
            return;

        _loadStarted = true;
        host.StartCoroutine(LoadWavsCoroutine());
    }

    internal static void PlayRandom()
    {
        if (!_loadFinished || Clips.Count == 0)
            return;

        float volume = DeadArmorConfig.SoundVolume != null ? DeadArmorConfig.SoundVolume.Value : 1f;
        if (volume <= 0.001f)
            return;

        AudioClip clip = Clips[UnityEngine.Random.Range(0, Clips.Count)];
        GoldAudioPlayer.Play2D(clip, volume);

        DeadArmorConfig.LogDebug("[DeadArmor] Death sound: " + (clip != null ? clip.name : "?"));
    }

    private static IEnumerator LoadWavsCoroutine()
    {
        string folder = DeadArmorPaths.ResolveDeathSoundsDirectory();
        if (!Directory.Exists(folder))
        {
            Plugin.Log?.LogWarning("[DeadArmor] DeathSounds folder missing: " + folder);
            _loadFinished = true;
            yield break;
        }

        string[] wavFiles = Directory.GetFiles(folder, "*.wav", SearchOption.TopDirectoryOnly);
        int loaded = 0;
        for (int i = 0; i < wavFiles.Length; i++)
        {
            string filePath = wavFiles[i];
            string normalized = filePath.Replace("\\", "/");
            if (!normalized.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
                normalized = "file:///" + normalized;

            WWW www = new WWW(normalized);
            yield return www;

            if (!string.IsNullOrEmpty(www.error))
            {
                Plugin.Log?.LogWarning("[DeadArmor] WAV load failed: " + filePath + " (" + www.error + ")");
                continue;
            }

            AudioClip clip = www.GetAudioClip(false, false, AudioType.WAV);
            if (clip == null)
                continue;

            clip.name = Path.GetFileNameWithoutExtension(filePath);
            Clips.Add(clip);
            loaded++;
        }

        _loadFinished = true;
        DeadArmorConfig.LogDebug("[DeadArmor] Loaded " + loaded + " death sounds from " + folder);
    }
}
