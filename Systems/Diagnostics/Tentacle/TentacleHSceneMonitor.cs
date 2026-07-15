using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NoREroMod.Systems.Diagnostics.Tentacle;

/// <summary>
/// MonoBehaviour driver for tentacle H-scene diagnostics.
/// Polls every active <see cref="global::Tentacle"/> and <see cref="global::Trap_TentacleIronmaiden"/>
/// in <see cref="LateUpdate"/> (after all gameplay systems have ticked) and logs:
///
///   - any state-field transition while <c>actor.eroflag == true</c>
///     (erodata going inactive, erospine animation jumping, Hp dropping, player erodown changing, …)
///   - a periodic heartbeat line at <see cref="TentacleDiagnosticsConfig.HeartbeatSec"/> intervals
///     so absence of events can be distinguished from absence of polling.
///
/// All output is prefixed <c>[TentacleDiag]</c> for easy filtering. Hard log cap at
/// <see cref="TentacleDiagnosticsConfig.MaxLogsPerSession"/> to prevent runaway log files
/// if a soft-lock loops forever.
/// </summary>
internal sealed class TentacleHSceneMonitor : MonoBehaviour
{
    private const string TAG = "[TentacleDiag]";

    private static TentacleHSceneMonitor s_instance;

    private readonly Dictionary<int, TentacleHSceneSnapshot> _prevSnaps = new Dictionary<int, TentacleHSceneSnapshot>();
    private float _heartbeatNextAt;
    private int _logsThisSession;

    public static void Ensure()
    {
        if (s_instance != null && s_instance.gameObject != null) return;

        try
        {
            GameObject host = new GameObject("TentacleHSceneMonitor_XUAIGNORE");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            s_instance = host.AddComponent<TentacleHSceneMonitor>();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning(TAG + " Ensure failed: " + ex.Message);
        }
    }

    public static void Destroy()
    {
        if (s_instance != null && s_instance.gameObject != null)
        {
            try { UnityEngine.Object.Destroy(s_instance.gameObject); } catch { }
            s_instance = null;
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        Plugin.Log?.LogInfo(TAG + " monitor host ready (diagnostics gated by TentacleDiagnostics.json Enable)");
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _prevSnaps.Clear();
        TentacleHSceneReflection.ResetCachedPlayer();
        if (!TentacleDiagnosticsConfig.Enable) return;
        Log("scene loaded: " + scene.name);
    }

    private readonly HashSet<int> _seenThisFrame = new HashSet<int>();

    private void LateUpdate()
    {
        if (!TentacleDiagnosticsConfig.Enable) return;

        try
        {
            _seenThisFrame.Clear();
            ScanType(typeof(global::Tentacle), CaptureTentacle);
            ScanType(typeof(global::Trap_TentacleIronmaiden), CaptureTrap);
            DetectVanishedInstances();

            if (Time.unscaledTime >= _heartbeatNextAt)
            {
                _heartbeatNextAt = Time.unscaledTime + TentacleDiagnosticsConfig.HeartbeatSec;
                Heartbeat();
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning(TAG + " LateUpdate threw: " + ex.Message);
        }
    }

    /// <summary>
    /// Detect instances that were tracked last frame but are missing from
    /// <see cref="UnityEngine.Object.FindObjectsOfType"/> this frame — i.e. destroyed.
    /// Emits a CRITICAL log if the last-known state had <c>eroflag=true</c>, since that
    /// means the player will be soft-locked unless a safety net releases them.
    /// </summary>
    private void DetectVanishedInstances()
    {
        List<int> vanishedIds = null;
        foreach (KeyValuePair<int, TentacleHSceneSnapshot> kvp in _prevSnaps)
        {
            if (_seenThisFrame.Contains(kvp.Key)) continue;
            if (vanishedIds == null) vanishedIds = new List<int>();
            vanishedIds.Add(kvp.Key);
        }

        if (vanishedIds == null) return;

        for (int i = 0; i < vanishedIds.Count; i++)
        {
            int id = vanishedIds[i];
            TentacleHSceneSnapshot last = _prevSnaps[id];
            _prevSnaps.Remove(id);

            if (last.ActorEroflag || last.PlayerEroflag)
                Log("!! actor vanished from scene WHILE H-scene was active !! last=" + last);
        }
    }

    private static TentacleHSceneSnapshot CaptureTentacle(UnityEngine.Object o)
        => TentacleHSceneReflection.CaptureFromTentacle(o as global::Tentacle);

    private static TentacleHSceneSnapshot CaptureTrap(UnityEngine.Object o)
        => TentacleHSceneReflection.CaptureFromTrap(o as global::Trap_TentacleIronmaiden);

    private void ScanType(Type t, Func<UnityEngine.Object, TentacleHSceneSnapshot> capture)
    {
        UnityEngine.Object[] found = UnityEngine.Object.FindObjectsOfType(t);
        if (found == null) return;

        for (int i = 0; i < found.Length; i++)
        {
            UnityEngine.Object obj = found[i];
            if (obj == null) continue;

            TentacleHSceneSnapshot now = capture(obj);
            if (now.InstanceId == 0) continue;
            _seenThisFrame.Add(now.InstanceId);

            TentacleHSceneSnapshot prev;
            bool hadPrev = _prevSnaps.TryGetValue(now.InstanceId, out prev);

            if (!hadPrev)
            {
                if (now.ActorEroflag)
                    Log("first-seen-active " + now);
            }
            else if (!prev.IsEqual(now))
            {
                if (now.ActorEroflag || prev.ActorEroflag)
                    LogTransition(prev, now);
            }

            _prevSnaps[now.InstanceId] = now;
        }
    }

    private void LogTransition(TentacleHSceneSnapshot prev, TentacleHSceneSnapshot now)
    {
        // Highlight the most likely-suspect transitions in their own log lines so they jump
        // out of the heartbeat noise.
        if (prev.ErodataActive && !now.ErodataActive && now.ActorEroflag)
        {
            string trace = TentacleDiagnosticsConfig.LogStackTraceOnErodataDeactivate
                ? "\n" + Environment.StackTrace
                : string.Empty;
            Log("!! erodata deactivated DURING H-scene !! " + now + trace);
            return;
        }

        if (prev.ActorAlive && !now.ActorAlive && (prev.ActorEroflag || now.PlayerEroflag))
        {
            Log("!! actor became inactive DURING H-scene !! prev=" + prev + " now=" + now);
            return;
        }

        if (prev.ErospineEnabled && !now.ErospineEnabled && now.ActorEroflag)
        {
            Log("!! erospine disabled DURING H-scene !! " + now);
            return;
        }

        if (prev.ActorHp > 0f && now.ActorHp <= 0f)
        {
            Log("Hp -> 0 (eroflag=" + now.ActorEroflag + "): " + now);
            return;
        }

        if (prev.PlayerErodown != now.PlayerErodown)
        {
            Log("player erodown " + prev.PlayerErodown + " -> " + now.PlayerErodown + " :: " + now);
            return;
        }

        if (prev.PlayerEroflag != now.PlayerEroflag)
        {
            Log("player eroflag " + prev.PlayerEroflag + " -> " + now.PlayerEroflag + " :: " + now);
            return;
        }

        if (prev.ActorEroflag != now.ActorEroflag)
        {
            Log("actor eroflag " + prev.ActorEroflag + " -> " + now.ActorEroflag + " :: " + now);
            return;
        }

        if (!string.Equals(prev.ErospineAnim, now.ErospineAnim))
        {
            Log("erospine anim '" + prev.ErospineAnim + "' -> '" + now.ErospineAnim + "' :: " + now);
            return;
        }

        if (!string.Equals(prev.MyspineAnim, now.MyspineAnim))
        {
            Log("myspine anim '" + prev.MyspineAnim + "' -> '" + now.MyspineAnim + "' :: " + now);
            return;
        }

        if (Mathf.Abs(prev.TimeScale - now.TimeScale) > 0.01f)
        {
            Log("Time.timeScale " + prev.TimeScale.ToString("0.##") + " -> " + now.TimeScale.ToString("0.##") + " :: " + now);
            return;
        }
    }

    private void Heartbeat()
    {
        bool anyActive = false;
        foreach (KeyValuePair<int, TentacleHSceneSnapshot> kvp in _prevSnaps)
        {
            if (kvp.Value.ActorEroflag)
            {
                anyActive = true;
                Log("heartbeat " + kvp.Value);
            }
        }

        if (!anyActive) return; // quiet when nothing is happening
    }

    private void Log(string message)
    {
        if (_logsThisSession >= TentacleDiagnosticsConfig.MaxLogsPerSession) return;
        _logsThisSession++;
        Plugin.Log?.LogInfo(TAG + " " + message);
        if (_logsThisSession == TentacleDiagnosticsConfig.MaxLogsPerSession)
            Plugin.Log?.LogWarning(TAG + " log cap reached for this session; further events suppressed");
    }
}
