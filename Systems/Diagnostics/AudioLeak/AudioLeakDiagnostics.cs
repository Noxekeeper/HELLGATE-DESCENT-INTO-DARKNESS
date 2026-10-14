using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using DarkTonic.MasterAudio;
using HarmonyLib;
using NoREroMod.Systems.Cache;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NoREroMod.Systems.Diagnostics.AudioLeak;

/// <summary>
/// Leaked / looping audio diagnostics (e.g. a sound that keeps repeating in a cutscene).
/// Logs every MasterAudio PlaySound* call with its caller, every AudioSource that starts playing,
/// TrapNormal hits, active DontDestroyOnLoad roots and scene load/unload. No gameplay changes.
/// Enable via <c>HellGateJson/Diagnostics/AudioLeakDiagnostics.json</c> before launch — when off at boot
/// nothing is patched (the toggle can still switch logging off at runtime).
/// Dedicated log: <c>BepInEx/LogOutput/HellGate_AudioLeakDiag.log</c>.
/// </summary>
internal static class AudioLeakDiagnostics
{
    public static void Initialize(Harmony harmony)
    {
        if (!AudioLeakDiagnosticsConfig.Enable)
            return;

        int patched = PatchMasterAudio(harmony);
        PatchTrapNormal(harmony);
        AudioLeakMonitor.Ensure();
        Plugin.Log?.LogInfo(
            "[AudioLeakDiag] module initialized (MasterAudio methods patched=" + patched +
            "; JSON: HellGateJson/Diagnostics/AudioLeakDiagnostics.json; log: LogOutput/HellGate_AudioLeakDiag.log)");
    }

    private static int PatchMasterAudio(Harmony harmony)
    {
        if (harmony == null)
            return 0;

        var prefix = new HarmonyMethod(typeof(AudioLeakDiagnostics).GetMethod(
            nameof(PlaySoundPrefix), BindingFlags.Static | BindingFlags.NonPublic));
        int count = 0;
        MethodInfo[] methods = typeof(MasterAudio).GetMethods(BindingFlags.Public | BindingFlags.Static);
        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo m = methods[i];
            if (!m.Name.StartsWith("PlaySound", StringComparison.Ordinal) || m.IsGenericMethod)
                continue;
            ParameterInfo[] ps = m.GetParameters();
            if (ps.Length == 0 || ps[0].ParameterType != typeof(string))
                continue;

            try
            {
                harmony.Patch(m, prefix: prefix);
                count++;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("[AudioLeakDiag] patch " + m.Name + " failed: " + ex.Message);
            }
        }

        return count;
    }

    private static readonly HashSet<long> s_trapPairsLogged = new HashSet<long>();

    private static void PatchTrapNormal(Harmony harmony)
    {
        if (harmony == null)
            return;
        try
        {
            MethodInfo target = typeof(TrapNormal).GetMethod(
                "OnTriggerEnter2D", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (target == null)
                return;
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(AudioLeakDiagnostics).GetMethod(
                nameof(TrapEnterPrefix), BindingFlags.Static | BindingFlags.NonPublic)));
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[AudioLeakDiag] patch TrapNormal failed: " + ex.Message);
        }
    }

    private static void TrapEnterPrefix(TrapNormal __instance, Collider2D __0)
    {
        try
        {
            if (!AudioLeakDiagnosticsConfig.Enable || __instance == null || __0 == null)
                return;
            if (__0.gameObject.tag != "EnemyCol")
                return;
            long key = ((long)__instance.GetInstanceID() << 32) ^ (uint)__0.gameObject.GetInstanceID();
            if (!s_trapPairsLogged.Add(key))
                return;
            AudioLeakDiagLog.Write("TRAP_HIT trap=" + DescribeObject(__instance.gameObject)
                + " | enemy=" + DescribeObject(__0.gameObject));
        }
        catch
        {
        }
    }

    private static string DescribeObject(GameObject go)
    {
        var sb = new StringBuilder();
        Transform t = go.transform;
        sb.Append(t.name);
        for (Transform p = t.parent; p != null; p = p.parent)
            sb.Insert(0, p.name + "/");
        Vector3 pos = t.position;
        sb.Append(" scene=").Append(go.scene.name)
          .Append(" active=").Append(go.activeInHierarchy)
          .Append(" pos=(").Append(pos.x.ToString("F1")).Append(',').Append(pos.y.ToString("F1")).Append(')')
          .Append(" id=").Append(go.GetInstanceID());
        return sb.ToString();
    }

    // Harmony on this runtime fails IL compile for __originalMethod / __args — positional __0 only.
    private static void PlaySoundPrefix(string __0)
    {
        try
        {
            if (!AudioLeakDiagnosticsConfig.Enable)
                return;
            AudioLeakMonitor.OnMasterAudioPlay(__0 ?? "null", DescribeCaller());
        }
        catch
        {
        }
    }

    private static string DescribeCaller()
    {
        var trace = new StackTrace(2, false);
        int depth = AudioLeakDiagnosticsConfig.CallerDepth;
        var sb = new StringBuilder();
        int added = 0;
        for (int i = 0; i < trace.FrameCount && added < depth; i++)
        {
            MethodBase method = trace.GetFrame(i)?.GetMethod();
            Type type = method?.DeclaringType;
            if (type == null)
                continue;
            if (type == typeof(MasterAudio) || type == typeof(AudioLeakDiagnostics))
                continue;
            if (type.Namespace != null && type.Namespace.StartsWith("HarmonyLib", StringComparison.Ordinal))
                continue;

            if (added > 0)
                sb.Append(" < ");
            sb.Append(type.Name).Append('.').Append(method.Name);
            added++;
        }

        return added == 0 ? "?" : sb.ToString();
    }
}

internal sealed class AudioLeakMonitor : MonoBehaviour
{
    private static AudioLeakMonitor s_instance;

    private struct PlayingSource
    {
        public float StartedAt;
        public string Describe;
        public bool Loop;
    }

    private struct RepeatStat
    {
        public int Count;
        public float FirstAt;
    }

    private readonly Dictionary<string, PlayingSource> _playing = new Dictionary<string, PlayingSource>();
    private static readonly Dictionary<string, RepeatStat> s_repeats = new Dictionary<string, RepeatStat>();
    private static readonly HashSet<string> s_seenPlays = new HashSet<string>();

    private float _nextPollAt;
    private float _nextSummaryAt;
    private bool _wasEnabled;
    private string _lastContext = "";

    public static void Ensure()
    {
        if (s_instance != null && s_instance.gameObject != null)
            return;

        try
        {
            GameObject host = new GameObject("AudioLeakMonitor_XUAIGNORE");
            Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            s_instance = host.AddComponent<AudioLeakMonitor>();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[AudioLeakDiag] Ensure failed: " + ex.Message);
        }
    }

    internal static void OnMasterAudioPlay(string sound, string caller)
    {
        string key = sound + " | " + caller;
        if (s_seenPlays.Add(key))
        {
            AudioLeakDiagLog.Write("MA_PLAY sound=" + sound + " caller=" + caller + " | " + DescribeContext());
            return;
        }

        s_repeats.TryGetValue(key, out RepeatStat stat);
        if (stat.Count == 0)
            stat.FirstAt = Time.unscaledTime;
        stat.Count++;
        s_repeats[key] = stat;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!AudioLeakDiagnosticsConfig.Enable)
            return;
        AudioLeakDiagLog.Write("SCENE_LOADED " + scene.name + " mode=" + mode + " | " + DescribeContext());
        s_seenPlays.Clear();
        LogActivePersistentRoots();
    }

    private void LogActivePersistentRoots()
    {
        try
        {
            GameObject[] roots = gameObject.scene.GetRootGameObjects();
            var sb = new StringBuilder();
            int active = 0;
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject r = roots[i];
                if (r == null || !r.activeInHierarchy)
                    continue;
                active++;
                if (sb.Length > 0)
                    sb.Append(", ");
                sb.Append(r.name);
                if (r.GetComponentInChildren<TrapNormal>() != null || r.GetComponentInChildren<EnemyDate>() != null)
                    sb.Append("[ENEMY/TRAP @").Append(r.transform.position.x.ToString("F0"))
                      .Append(',').Append(r.transform.position.y.ToString("F0")).Append(']');
            }
            AudioLeakDiagLog.Write("DDOL_ACTIVE total=" + roots.Length + " active=" + active + " : " + sb);
        }
        catch (Exception ex)
        {
            AudioLeakDiagLog.Write("DDOL_ACTIVE failed: " + ex.Message);
        }
    }

    private void OnSceneUnloaded(Scene scene)
    {
        if (!AudioLeakDiagnosticsConfig.Enable)
            return;
        AudioLeakDiagLog.Write("SCENE_UNLOADED " + scene.name + " | " + DescribeContext());
    }

    private void LateUpdate()
    {
        bool enabled = AudioLeakDiagnosticsConfig.Enable;
        if (enabled && !_wasEnabled)
        {
            AudioLeakDiagLog.ResetSession();
            _playing.Clear();
            s_repeats.Clear();
            s_seenPlays.Clear();
            AudioLeakDiagLog.Write("ENABLED | " + DescribeContext());
        }

        _wasEnabled = enabled;
        if (!enabled)
            return;

        try
        {
            float now = Time.unscaledTime;
            if (now >= _nextPollAt)
            {
                _nextPollAt = now + AudioLeakDiagnosticsConfig.PollIntervalSec;
                LogContextChange();
                PollAudioSources(now);
            }

            if (now >= _nextSummaryAt)
            {
                _nextSummaryAt = now + AudioLeakDiagnosticsConfig.RepeatSummarySec;
                FlushRepeats(now);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[AudioLeakDiag] LateUpdate: " + ex.Message);
        }
    }

    private void LogContextChange()
    {
        string context = DescribeContext();
        if (context == _lastContext)
            return;
        _lastContext = context;
        AudioLeakDiagLog.Write("CONTEXT " + context);
    }

    private void PollAudioSources(float now)
    {
        AudioSource[] sources = Object.FindObjectsOfType<AudioSource>();
        var stillPlaying = new HashSet<string>();
        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource src = sources[i];
            if (src == null || !src.isPlaying)
                continue;

            string clip = src.clip != null ? src.clip.name : "null";
            string key = src.GetInstanceID() + ":" + clip;
            stillPlaying.Add(key);
            if (_playing.ContainsKey(key))
                continue;

            string describe = "clip=" + clip +
                              " loop=" + src.loop +
                              " vol=" + src.volume.ToString("0.00") +
                              " mute=" + src.mute +
                              " path=" + GetPath(src.transform) +
                              " scene=" + (src.gameObject.scene.IsValid() ? src.gameObject.scene.name : "?");
            _playing[key] = new PlayingSource { StartedAt = now, Describe = describe, Loop = src.loop };
            AudioLeakDiagLog.Write("AS_START " + describe);
        }

        var ended = new List<string>();
        foreach (KeyValuePair<string, PlayingSource> pair in _playing)
        {
            if (!stillPlaying.Contains(pair.Key))
                ended.Add(pair.Key);
        }

        for (int i = 0; i < ended.Count; i++)
        {
            PlayingSource ps = _playing[ended[i]];
            float duration = now - ps.StartedAt;
            if (ps.Loop || duration >= 5f)
                AudioLeakDiagLog.Write("AS_STOP after " + duration.ToString("0.0") + "s " + ps.Describe);
            _playing.Remove(ended[i]);
        }
    }

    private static void FlushRepeats(float now)
    {
        if (s_repeats.Count == 0)
            return;

        foreach (KeyValuePair<string, RepeatStat> pair in s_repeats)
        {
            if (pair.Value.Count <= 0)
                continue;
            AudioLeakDiagLog.Write(
                "MA_REPEAT x" + pair.Value.Count + " in " + (now - pair.Value.FirstAt).ToString("0.0") + "s " +
                pair.Key + " | " + DescribeContext());
        }

        s_repeats.Clear();
    }

    private static string DescribeContext()
    {
        string active = SceneManager.GetActiveScene().name;
        var loaded = new StringBuilder();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (!s.isLoaded)
                continue;
            if (loaded.Length > 0)
                loaded.Append(',');
            loaded.Append(s.name);
        }

        string player = "player=null";
        try
        {
            playercon pc = UnifiedPlayerCacheManager.GetPlayer();
            PlayerStatus st = UnifiedPlayerCacheManager.GetPlayerStatus();
            if (pc != null)
            {
                player = "eroflag=" + pc.eroflag +
                         " SOUSA=" + (st != null && st._SOUSA) +
                         " Death=" + pc._Death;
            }
        }
        catch
        {
        }

        return "active=" + active + " loaded=[" + loaded + "] " + player;
    }

    private static string GetPath(Transform t)
    {
        if (t == null)
            return "?";
        var parts = new List<string>();
        int guard = 0;
        while (t != null && guard++ < 8)
        {
            parts.Add(t.name);
            t = t.parent;
        }

        parts.Reverse();
        return string.Join("/", parts.ToArray());
    }
}
