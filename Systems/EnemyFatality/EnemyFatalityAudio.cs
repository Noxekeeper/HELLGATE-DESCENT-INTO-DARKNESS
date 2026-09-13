using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NoREroMod.Systems.Economy;
using UnityEngine;

namespace NoREroMod.Systems.EnemyFatality;

/// <summary>
/// Per-clip-folder fatality SFX: Hit.wav + death sound.wav together, optional mid-clip
/// cue (e.g. bone-crack.wav), then Final.wav at clip end.
/// HeavyCritical uses a start pool (Quick Death 1/2.wav) — one random cue at clip begin.
/// Packs are keyed by clip relative path so shared pools (lost_leg / lost_head) reuse audio.
/// </summary>
internal static class EnemyFatalityAudio
{
    private const string HitFileName = "Hit.wav";
    private const string DeathFileName = "death sound.wav";
    private const string FinalFileName = "Final.wav";

    /// <summary>HeavyCritical-only start cues (random pick at fatality begin).</summary>
    private static readonly string[] HeavyCriticalStartSfxFileNames =
    {
        "Quick Death 1.wav",
        "Quick Death 2.wav",
    };

    private sealed class Pack
    {
        internal AudioClip Hit;
        internal AudioClip Death;
        internal AudioClip Final;
        internal AudioClip MidClip;
        internal AudioClip[] StartPool;
        internal string MidClipFileName = string.Empty;
        internal bool LoadStarted;
        internal bool LoadFinished;
        internal bool FinalPlayed;
        internal bool MidClipPlayed;
        internal Coroutine SequenceRoutine;
    }

    private static MonoBehaviour _host;
    private static readonly Dictionary<string, Pack> Packs =
        new Dictionary<string, Pack>(StringComparer.OrdinalIgnoreCase);

    internal static void Initialize(MonoBehaviour host)
    {
        if (host == null)
            return;

        _host = host;

        List<IEnemyFatalityProfile> profiles = EnemyFatalityRegistry.All;
        for (int i = 0; i < profiles.Count; i++)
        {
            IEnemyFatalityProfile profile = profiles[i];
            if (profile == null || !profile.IsModuleEnabled)
                continue;

            string[] relatives = EnemyFatalityClipPicker.GetPreloadRelatives(profile);
            for (int c = 0; c < relatives.Length; c++)
                EnsureLoading(profile, relatives[c]);
        }
    }

    internal static void PlayHitThenDeath(IEnemyFatalityProfile profile, string clipRelative = null)
    {
        if (profile == null || _host == null)
            return;

        string rel = ResolveActiveClipRelative(profile, clipRelative);
        Pack pack = EnsureLoading(profile, rel);
        pack.FinalPlayed = false;
        pack.MidClipPlayed = false;

        if (pack.SequenceRoutine != null)
            _host.StopCoroutine(pack.SequenceRoutine);

        pack.SequenceRoutine = _host.StartCoroutine(HitThenDeathCoroutine(profile, pack));
    }

    internal static void PlayFinalOnce(IEnemyFatalityProfile profile)
    {
        if (profile == null)
            return;

        string rel = ResolveActiveClipRelative(profile, null);
        Pack pack = EnsureLoading(profile, rel);
        if (pack.FinalPlayed)
            return;

        pack.FinalPlayed = true;
        PlayClip(pack.Final, "Final", profile);
    }

    /// <summary>Optional mid-clip cue (armed by ClipPlayer after a chosen PNG frame + delay).</summary>
    internal static void PlayMidClipOnce(IEnemyFatalityProfile profile)
    {
        if (profile == null)
            return;

        string rel = ResolveActiveClipRelative(profile, null);
        Pack pack = EnsureLoading(profile, rel);
        if (pack.MidClipPlayed)
            return;

        pack.MidClipPlayed = true;

        string label = string.IsNullOrEmpty(pack.MidClipFileName)
            ? "mid-clip"
            : pack.MidClipFileName;
        PlayClip(pack.MidClip, label, profile);
    }

    private static string ResolveActiveClipRelative(IEnemyFatalityProfile profile, string clipRelative)
    {
        if (!string.IsNullOrEmpty(clipRelative))
            return clipRelative.Trim();

        if (!string.IsNullOrEmpty(EnemyFatalitySession.ActiveClipRelative))
            return EnemyFatalitySession.ActiveClipRelative;

        return profile.ClipPathRelative;
    }

    private static Pack EnsureLoading(IEnemyFatalityProfile profile, string clipRelative)
    {
        string key = string.IsNullOrEmpty(clipRelative) ? profile.Id : clipRelative.Trim();
        Pack pack = GetOrCreatePack(key);
        if (!pack.LoadStarted && _host != null)
        {
            pack.LoadStarted = true;
            _host.StartCoroutine(LoadWavsCoroutine(profile, clipRelative, pack));
        }

        return pack;
    }

    private static Pack GetOrCreatePack(string key)
    {
        if (!Packs.TryGetValue(key, out Pack pack))
        {
            pack = new Pack();
            Packs[key] = pack;
        }

        return pack;
    }

    private static IEnumerator HitThenDeathCoroutine(IEnemyFatalityProfile profile, Pack pack)
    {
        float wait = 0f;
        while (!pack.LoadFinished && wait < 5f)
        {
            wait += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!TryPlayRandomStart(pack, profile))
        {
            PlayClip(pack.Hit, "Hit", profile);
            PlayClip(pack.Death, "death sound", profile);
        }

        pack.SequenceRoutine = null;
    }

    private static bool TryPlayRandomStart(Pack pack, IEnemyFatalityProfile profile)
    {
        if (pack == null || pack.StartPool == null || pack.StartPool.Length == 0)
            return false;

        int loaded = 0;
        for (int i = 0; i < pack.StartPool.Length; i++)
        {
            if (pack.StartPool[i] != null)
                loaded++;
        }

        if (loaded <= 0)
            return false;

        int pick = UnityEngine.Random.Range(0, loaded);
        for (int i = 0; i < pack.StartPool.Length; i++)
        {
            AudioClip clip = pack.StartPool[i];
            if (clip == null)
                continue;
            if (pick == 0)
            {
                PlayClip(clip, clip.name, profile);
                return true;
            }

            pick--;
        }

        return false;
    }

    internal static bool IsHeavyCriticalClip(string clipRelative)
    {
        if (string.IsNullOrEmpty(clipRelative))
            return false;

        string name = Path.GetFileName(clipRelative.Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar));
        return string.Equals(name, "HeavyCritical", StringComparison.OrdinalIgnoreCase);
    }

    private static void PlayClip(AudioClip clip, string label, IEnemyFatalityProfile profile)
    {
        if (clip == null)
        {
            Plugin.Log?.LogWarning("[" + profile.Id + "] Missing audio: " + label);
            return;
        }

        float volume = profile.SoundVolume;
        if (volume <= 0.001f)
            return;

        GoldAudioPlayer.Play2D(clip, volume);
        EnemyFatalityConfig.LogDebug("[" + profile.Id + "] Play SFX: " + label);
    }

    private static IEnumerator LoadWavsCoroutine(
        IEnemyFatalityProfile profile,
        string clipRelative,
        Pack pack)
    {
        string folder = string.IsNullOrEmpty(clipRelative)
            ? EnemyFatalityPaths.ResolveClipDirectory(profile)
            : EnemyFatalityPaths.ResolveRelativeDirectory(clipRelative);

        string logTag = profile.Id + (string.IsNullOrEmpty(clipRelative)
            ? string.Empty
            : ":" + Path.GetFileName(clipRelative.Replace('/', Path.DirectorySeparatorChar)));

        if (IsHeavyCriticalClip(clipRelative))
        {
            pack.StartPool = new AudioClip[HeavyCriticalStartSfxFileNames.Length];
            for (int i = 0; i < HeavyCriticalStartSfxFileNames.Length; i++)
            {
                int index = i;
                yield return LoadNamed(
                    folder,
                    HeavyCriticalStartSfxFileNames[index],
                    c => pack.StartPool[index] = c,
                    logTag);
            }
        }
        else
        {
            pack.StartPool = null;
        }

        yield return LoadNamed(folder, HitFileName, c => pack.Hit = c, logTag);
        yield return LoadNamed(folder, DeathFileName, c => pack.Death = c, logTag);

        if (pack.Death == null && pack.StartPool == null)
        {
            string alt = ResolveDeathSoundFallback();
            if (!string.IsNullOrEmpty(alt))
            {
                yield return LoadNamed(
                    Path.GetDirectoryName(alt),
                    Path.GetFileName(alt),
                    c => pack.Death = c,
                    logTag);
            }
        }

        yield return LoadNamed(folder, FinalFileName, c => pack.Final = c, logTag);

        pack.MidClipFileName = profile.MidClipSfxFileName ?? string.Empty;
        if (!string.IsNullOrEmpty(pack.MidClipFileName))
            yield return LoadNamed(folder, pack.MidClipFileName, c => pack.MidClip = c, logTag);
        else
            pack.MidClip = null;

        pack.LoadFinished = true;

        int startLoaded = 0;
        if (pack.StartPool != null)
        {
            for (int i = 0; i < pack.StartPool.Length; i++)
            {
                if (pack.StartPool[i] != null)
                    startLoaded++;
            }
        }

        Plugin.Log?.LogInfo(
            "[" + logTag + "] Audio loaded — Hit="
            + (pack.Hit != null)
            + " death="
            + (pack.Death != null)
            + " Final="
            + (pack.Final != null)
            + " Mid="
            + (pack.MidClip != null)
            + (string.IsNullOrEmpty(pack.MidClipFileName) ? string.Empty : " (" + pack.MidClipFileName + ")")
            + " StartPool="
            + startLoaded
            + " dir="
            + folder);
    }

    private static string ResolveDeathSoundFallback()
    {
        string gameRoot = Application.dataPath;
        if (gameRoot.EndsWith("_Data"))
            gameRoot = gameRoot.Substring(0, gameRoot.Length - 5);

        var candidates = new List<string>(4)
        {
            Path.Combine(Path.Combine(Path.Combine(Path.Combine(gameRoot, "sources"), "HellGate_sources"), "DeadArmor"), Path.Combine("DeathSounds", DeathFileName)),
            Path.Combine(Path.Combine(Path.Combine(Path.Combine(Path.GetFullPath(Path.Combine(gameRoot, "..")), "sources"), "HellGate_sources"), "DeadArmor"), Path.Combine("DeathSounds", DeathFileName)),
        };

        for (int i = 0; i < candidates.Count; i++)
        {
            string full = Path.GetFullPath(candidates[i]);
            if (File.Exists(full))
                return full;
        }

        return null;
    }

    private static IEnumerator LoadNamed(string directory, string fileName, Action<AudioClip> assign, string logTag)
    {
        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
        {
            assign(null);
            yield break;
        }

        string filePath = Path.Combine(directory, fileName);
        if (!File.Exists(filePath))
        {
            assign(null);
            yield break;
        }

        string normalized = filePath.Replace("\\", "/");
        if (!normalized.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
            normalized = "file:///" + normalized;

        WWW www = new WWW(normalized);
        yield return www;

        if (!string.IsNullOrEmpty(www.error))
        {
            Plugin.Log?.LogWarning(
                "[" + logTag + "] WAV load failed: " + filePath + " (" + www.error + ")");
            assign(null);
            yield break;
        }

        AudioClip clip = www.GetAudioClip(false, false, AudioType.WAV);
        if (clip != null)
            clip.name = Path.GetFileNameWithoutExtension(filePath);
        assign(clip);
    }
}
