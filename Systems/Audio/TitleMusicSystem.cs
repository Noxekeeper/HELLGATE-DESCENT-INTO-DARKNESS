using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NoREroMod.Systems.Audio;

/// <summary>
/// Replaces the vanilla title theme (<c>Gametitle</c> → <c>BGM</c> AudioSource, Maintheme_1) with random
/// tracks from <c>sources/HellGate_sources/Audio/Main</c> (.wav / .ogg). Reuses the vanilla AudioSource so
/// the game's own BGM volume handling still applies. Each title visit picks one random track and loops it.
/// </summary>
internal static class TitleMusicSystem
{
    private const string TitleSceneName = "Gametitle";
    private const string BgmObjectName = "BGM";

    private static TitleMusicRunner _runner;

    internal static void Initialize()
    {
        if (_runner != null)
            return;

        try
        {
            GameObject host = new GameObject("TitleMusicSystem_XUAIGNORE");
            Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            _runner = host.AddComponent<TitleMusicRunner>();
            Plugin.Log?.LogInfo("[TitleMusic] Initialized, folder: " + GetMusicFolder());
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[TitleMusic] Init failed: " + ex.Message);
        }
    }

    internal static bool IsEnabled => Plugin.enableCustomTitleMusic == null || Plugin.enableCustomTitleMusic.Value;

    internal static string GetMusicFolder()
    {
        string gameRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.Combine(Path.Combine(Path.Combine(Path.Combine(gameRoot, "sources"), "HellGate_sources"), "Audio"), "Main");
    }

    internal static bool IsTitleScene(Scene scene) =>
        string.Equals(scene.name, TitleSceneName, StringComparison.OrdinalIgnoreCase);

    internal static AudioSource FindTitleBgm(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        GameObject[] roots = scene.GetRootGameObjects();
        AudioSource fallback = null;
        for (int i = 0; i < roots.Length; i++)
        {
            AudioSource[] sources = roots[i].GetComponentsInChildren<AudioSource>(true);
            for (int s = 0; s < sources.Length; s++)
            {
                AudioSource src = sources[s];
                if (src == null)
                    continue;
                if (string.Equals(src.gameObject.name, BgmObjectName, StringComparison.Ordinal))
                    return src;
                if (fallback == null && src.clip != null
                    && src.clip.name.StartsWith("Maintheme", StringComparison.OrdinalIgnoreCase))
                    fallback = src;
            }
        }

        return fallback;
    }
}

internal sealed class TitleMusicRunner : MonoBehaviour
{
    private static readonly string[] SupportedExtensions = { ".wav", ".ogg" };

    private readonly Dictionary<string, AudioClip> _clipCache = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
    private readonly System.Random _random = new System.Random();

    private AudioSource _bgm;
    private string[] _tracks = new string[0];
    private string _lastTrack;
    private bool _loading;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (TitleMusicSystem.IsTitleScene(scene) && scene.isLoaded)
            {
                OnSceneLoaded(scene, LoadSceneMode.Single);
                break;
            }
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!TitleMusicSystem.IsTitleScene(scene) || !TitleMusicSystem.IsEnabled)
            return;

        _tracks = ScanTracks();
        if (_tracks.Length == 0)
            return;

        AudioSource bgm = TitleMusicSystem.FindTitleBgm(scene);
        if (bgm == null)
        {
            Plugin.Log?.LogWarning("[TitleMusic] Gametitle loaded but BGM AudioSource not found — keeping vanilla theme.");
            return;
        }

        _bgm = bgm;
        _bgm.Stop();
        StartCoroutine(PlayNext());
    }

    private IEnumerator PlayNext()
    {
        if (_loading)
            yield break;
        _loading = true;

        string path = PickTrack();
        AudioClip clip = null;
        if (path != null && !_clipCache.TryGetValue(path, out clip))
        {
            yield return LoadClip(path, loaded => clip = loaded);
            if (clip != null)
                _clipCache[path] = clip;
        }

        _loading = false;
        if (_bgm == null || clip == null)
            yield break;

        _lastTrack = path;
        _bgm.clip = clip;
        _bgm.loop = true;
        _bgm.time = 0f;
        _bgm.Play();
        Plugin.Log?.LogInfo("[TitleMusic] Playing " + Path.GetFileName(path));
    }

    private string PickTrack()
    {
        if (_tracks.Length == 0)
            return null;
        if (_tracks.Length == 1)
            return _tracks[0];

        string pick;
        do
        {
            pick = _tracks[_random.Next(_tracks.Length)];
        }
        while (string.Equals(pick, _lastTrack, StringComparison.OrdinalIgnoreCase));
        return pick;
    }

    private static string[] ScanTracks()
    {
        string folder = TitleMusicSystem.GetMusicFolder();
        if (!Directory.Exists(folder))
            return new string[0];

        var result = new List<string>();
        string[] files = Directory.GetFiles(folder);
        for (int i = 0; i < files.Length; i++)
        {
            string ext = Path.GetExtension(files[i]);
            for (int e = 0; e < SupportedExtensions.Length; e++)
            {
                if (string.Equals(ext, SupportedExtensions[e], StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(files[i]);
                    break;
                }
            }
        }

        return result.ToArray();
    }

    private static IEnumerator LoadClip(string path, Action<AudioClip> done)
    {
        AudioType type = string.Equals(Path.GetExtension(path), ".ogg", StringComparison.OrdinalIgnoreCase)
            ? AudioType.OGGVORBIS
            : AudioType.WAV;

        WWW www = new WWW("file:///" + path.Replace("\\", "/"));
        yield return www;

        if (!string.IsNullOrEmpty(www.error))
        {
            Plugin.Log?.LogWarning("[TitleMusic] Failed to load " + path + ": " + www.error);
            done(null);
            yield break;
        }

        AudioClip clip = www.GetAudioClip(false, false, type);
        if (clip == null)
        {
            Plugin.Log?.LogWarning("[TitleMusic] Unsupported or empty audio file: " + path);
            done(null);
            yield break;
        }

        while (clip.loadState == AudioDataLoadState.Loading)
            yield return null;

        clip.name = Path.GetFileNameWithoutExtension(path);
        done(clip);
    }
}
