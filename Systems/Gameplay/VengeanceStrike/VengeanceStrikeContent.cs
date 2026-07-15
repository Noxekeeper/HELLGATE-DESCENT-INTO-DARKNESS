using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace NoREroMod.Systems.Gameplay;

/// <summary>Loads optional strike WAV from <see cref="VengeanceStrikePaths"/> (WWW + <see cref="AudioType.WAV"/>).</summary>
internal static class VengeanceStrikeContent
{
    private static bool _initialized;
    private static AudioClip _strikeClip;
    private static AudioSource _previewSource;

    internal static bool HasStrikeClip => _strikeClip != null;

    internal static void Initialize(MonoBehaviour host)
    {
        if (_initialized || host == null) return;
        _initialized = true;

        string dir = VengeanceStrikePaths.GetVengeanceStrikeContentDirectory();
        Plugin.Log?.LogInfo("[VengeanceStrike] Content directory (resolved): " + dir);

        if (!(Plugin.enableVengeanceStrikeAssets?.Value ?? true))
        {
            Plugin.Log?.LogInfo("[VengeanceStrike] Assets disabled in config.");
            return;
        }

        if (!Directory.Exists(dir))
        {
            Plugin.Log?.LogWarning("[VengeanceStrike] Folder missing — create it and add WAV: " + dir);
            return;
        }

        string fileName = Plugin.vengeanceStrikeSoundFile?.Value?.Trim();
        if (string.IsNullOrEmpty(fileName))
        {
            Plugin.Log?.LogInfo("[VengeanceStrike] SoundFile is empty — skip loading.");
            return;
        }

        // Only a leaf filename (portable; blocks ..\ escapes from config).
        fileName = Path.GetFileName(fileName);
        if (string.IsNullOrEmpty(fileName))
        {
            Plugin.Log?.LogInfo("[VengeanceStrike] SoundFile is not a valid filename — skip loading.");
            return;
        }

        string path = Path.GetFullPath(Path.Combine(Path.GetFullPath(dir), fileName));

        if (!File.Exists(path))
        {
            Plugin.Log?.LogWarning("[VengeanceStrike] WAV not found: " + path);
            return;
        }

        host.StartCoroutine(LoadStrikeClipCoroutine(path));
    }

    private static IEnumerator LoadStrikeClipCoroutine(string filePath)
    {
        string fullPath = Path.GetFullPath(filePath);
        // Proper file:// URI (spaces, unicode, drive letters) across Windows/Linux/macOS.
        string fileUri = new Uri(fullPath).AbsoluteUri;

        WWW www = new WWW(fileUri);
        yield return www;

        if (!string.IsNullOrEmpty(www.error))
        {
            Plugin.Log?.LogWarning("[VengeanceStrike] Failed to load wav: " + fullPath + " (" + www.error + ")");
            yield break;
        }

        AudioClip clip = www.GetAudioClip(false, false, AudioType.WAV);
        if (clip == null)
        {
            Plugin.Log?.LogWarning("[VengeanceStrike] GetAudioClip(WAV) returned null: " + fullPath);
            yield break;
        }

        clip.name = Path.GetFileNameWithoutExtension(fullPath);

        int wait = 0;
        while (clip.loadState == AudioDataLoadState.Loading && wait++ < 600)
            yield return null;

        if (clip.loadState != AudioDataLoadState.Loaded)
        {
            Plugin.Log?.LogWarning("[VengeanceStrike] Clip did not finish loading: " + fullPath + " state=" + clip.loadState);
            yield break;
        }

        _strikeClip = clip;
        Plugin.Log?.LogInfo("[VengeanceStrike] Loaded strike sound: " + Path.GetFileName(fullPath) + " (ready for Stab_fun)");
    }

    internal static void TryPlayStrikeSound(float volume = 1f)
    {
        if (_strikeClip == null) return;

        try
        {
            if (_previewSource == null)
            {
                var go = new GameObject("VengeanceStrike_Audio_XUAIGNORE");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _previewSource = go.AddComponent<AudioSource>();
                _previewSource.playOnAwake = false;
                _previewSource.loop = false;
                _previewSource.spatialBlend = 0f;
                _previewSource.ignoreListenerPause = true;
            }

            float v = Mathf.Clamp01(volume);
            _previewSource.volume = 1f;
            _previewSource.PlayOneShot(_strikeClip, v);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[VengeanceStrike] TryPlayStrikeSound: " + ex.Message);
        }
    }
}
