using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using DarkTonic.MasterAudio;
using HarmonyLib;
using NoREroMod.Systems.Economy;
using UnityEngine;

namespace NoREroMod.Patches.HellTraps;

/// <summary>Custom WAV death SFX and player vocal suppression during lethal trap custom death.</summary>
internal static class LethalMagicTrapDeathAudio
{
    private const float MoanSuppressIntervalSeconds = 0.25f;

    private static readonly HashSet<string> BlockedMasterAudioSounds =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "act_down_s",
            "act_downup",
            "down_aegi",
            "dame_kuu",
            "snd_down",
            "ero_start1",
            "ero_Unconscious",
            "death1",
        };

    private static AudioClip _deathClip;
    private static AudioClip _meatFallClip;
    private static bool _loadStarted;
    private static bool _startedThisDeath;
    private static bool _playedDeathSound;
    private static bool _playedMeatFallSound;
    private static bool _busesPaused;
    private static float _nextMoanSuppressUnscaledTime;

    internal static void ApplyPatches(Harmony harmony)
    {
        if (harmony == null)
            return;

        harmony.PatchAll(typeof(LethalMagicTrapDeathAudio));
    }

    internal static void Initialize(MonoBehaviour host)
    {
        if ((!Plugin.enableLethalMagicTrap.Value && !Plugin.enableLethalCocoonTrap.Value) ||
            host == null ||
            _loadStarted)
            return;

        _loadStarted = true;
        host.StartCoroutine(LoadAudioClipsCoroutine());
    }

    internal static void OnSuppressionEnabled()
    {
        if (!ShouldSuppressPlayerMoans())
            return;

        SuppressPlayerMoans(forceImmediate: true);
    }

    internal static void OnCustomDeathStarted()
    {
        if (_startedThisDeath)
            return;

        if (!Plugin.enableLethalMagicTrap.Value && !Plugin.enableLethalCocoonTrap.Value)
            return;

        _startedThisDeath = true;
        SuppressPlayerMoans(forceImmediate: true);
        TryPlayDeathSound();
    }

    internal static void TryPlayMeatFallSound()
    {
        if (_playedMeatFallSound || _meatFallClip == null)
            return;

        if (!Plugin.enableLethalMagicTrap.Value && !Plugin.enableLethalCocoonTrap.Value)
            return;

        GoldAudioPlayer.Play2D(_meatFallClip, LethalMagicTrapDeathTuning.MeatFallSoundVolume);
        _playedMeatFallSound = true;

        Plugin.Log?.LogInfo(
            "[LethalMagicTrapDeathAudio] Playing meat fall: "
            + LethalMagicTrapDeathTuning.MeatFallSoundFileName);
    }

    internal static void OnCustomDeathEnded()
    {
        _startedThisDeath = false;
        _playedDeathSound = false;
        _playedMeatFallSound = false;
        _nextMoanSuppressUnscaledTime = 0f;
        RestoreMasterAudioBuses();
    }

    internal static bool ShouldBlockMasterAudioSound(string soundName)
    {
        if (!ShouldSuppressPlayerMoans() || string.IsNullOrEmpty(soundName))
            return false;

        if (BlockedMasterAudioSounds.Contains(soundName))
            return true;

        return soundName.StartsWith("dame_", StringComparison.OrdinalIgnoreCase);
    }

    internal static void MaintainDuringSuppression()
    {
        if (!ShouldSuppressPlayerMoans())
            return;

        if (Time.unscaledTime < _nextMoanSuppressUnscaledTime)
            return;

        _nextMoanSuppressUnscaledTime = Time.unscaledTime + MoanSuppressIntervalSeconds;
        SuppressPlayerMoans(forceImmediate: false);
    }

    private static bool ShouldSuppressPlayerMoans()
    {
        if (!Plugin.enableLethalMagicTrap.Value && !Plugin.enableLethalCocoonTrap.Value)
            return false;

        if (!LethalMagicTrapDeathTuning.SuppressPlayerVoiceDuringDeath)
            return false;

        return LethalMagicTrapDeathContext.IsEroSuppressionActive ||
               LethalCocoonTrapDeathContext.IsEroSuppressionActive;
    }

    private static void TryPlayDeathSound()
    {
        if (_playedDeathSound || _deathClip == null)
            return;

        GoldAudioPlayer.Play2D(_deathClip, LethalMagicTrapDeathTuning.DeathSoundVolume);
        _playedDeathSound = true;

        Plugin.Log?.LogInfo(
            "[LethalMagicTrapDeathAudio] Playing death sound: "
            + LethalMagicTrapDeathTuning.DeathSoundFileName);
    }

    private static void SuppressPlayerMoans(bool forceImmediate)
    {
        if (!ShouldSuppressPlayerMoans())
            return;

        if (!forceImmediate && Time.unscaledTime < _nextMoanSuppressUnscaledTime)
            return;

        try
        {
            MasterAudio.StopBus("EroVoice");
            MasterAudio.StopBus("EroSE");
            MasterAudio.StopBus("Voice");

            if (!_busesPaused)
            {
                MasterAudio.PauseBus("EroVoice");
                MasterAudio.PauseBus("EroSE");
                MasterAudio.PauseBus("Voice");
                _busesPaused = true;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[LethalMagicTrapDeathAudio] StopBus failed: " + ex.Message);
        }
    }

    private static void RestoreMasterAudioBuses()
    {
        if (!_busesPaused)
            return;

        try
        {
            MasterAudio.UnpauseBus("EroVoice");
            MasterAudio.UnpauseBus("EroSE");
            MasterAudio.UnpauseBus("Voice");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[LethalMagicTrapDeathAudio] UnpauseBus failed: " + ex.Message);
        }
        finally
        {
            _busesPaused = false;
        }
    }

    [HarmonyPatch(typeof(MasterAudio), "PlaySound", new[] { typeof(string), typeof(float), typeof(Transform), typeof(float), typeof(Transform), typeof(bool), typeof(bool) })]
    internal static class MasterAudioPlaySoundBlockPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(string soundName)
        {
            if (!ShouldBlockMasterAudioSound(soundName))
                return true;

            return false;
        }
    }

    [HarmonyPatch(typeof(MasterAudio), "PlaySound", new[] { typeof(string), typeof(float) })]
    internal static class MasterAudioPlaySound2BlockPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(string soundName)
        {
            if (!ShouldBlockMasterAudioSound(soundName))
                return true;

            return false;
        }
    }

    private static IEnumerator LoadAudioClipsCoroutine()
    {
        string directory = LethalMagicTrapPaths.ResolveDeathClipDirectory(
            Plugin.lethalMagicTrapDeathClipPath.Value);
        if (!Directory.Exists(directory))
        {
            Plugin.Log?.LogInfo(
                "[LethalMagicTrapDeathAudio] Death audio folder missing (optional): "
                + directory);
            yield break;
        }

        yield return LoadWavFromDirectory(
            directory,
            LethalMagicTrapDeathTuning.DeathSoundFileName,
            clip => _deathClip = clip);

        yield return LoadWavFromDirectory(
            directory,
            LethalMagicTrapDeathTuning.MeatFallSoundFileName,
            clip => _meatFallClip = clip);
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
                "[LethalMagicTrapDeathAudio] Optional WAV not found: "
                + fileName);
            yield break;
        }

        string fileUri = new Uri(Path.GetFullPath(path)).AbsoluteUri;
        WWW www = new WWW(fileUri);
        yield return www;

        if (!string.IsNullOrEmpty(www.error))
        {
            Plugin.Log?.LogWarning(
                "[LethalMagicTrapDeathAudio] Failed to load WAV: "
                + path
                + " ("
                + www.error
                + ")");
            yield break;
        }

        AudioClip clip = www.GetAudioClip(false, false, AudioType.WAV);
        if (clip == null)
        {
            Plugin.Log?.LogWarning("[LethalMagicTrapDeathAudio] GetAudioClip returned null: " + path);
            yield break;
        }

        clip.name = Path.GetFileNameWithoutExtension(path);

        int wait = 0;
        while (clip.loadState == AudioDataLoadState.Loading && wait++ < 600)
            yield return null;

        if (clip.loadState != AudioDataLoadState.Loaded)
        {
            Plugin.Log?.LogWarning(
                "[LethalMagicTrapDeathAudio] Clip did not finish loading: "
                + path
                + " state="
                + clip.loadState);
            yield break;
        }

        assignClip(clip);
        Plugin.Log?.LogInfo("[LethalMagicTrapDeathAudio] Loaded WAV: " + fileName);
    }
}
