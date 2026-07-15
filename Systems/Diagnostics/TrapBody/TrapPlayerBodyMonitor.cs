using System;
using NoREroMod.Systems.Cache;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NoREroMod.Systems.Diagnostics.TrapBody;

/// <summary>
/// Polls active <see cref="Trapdata"/> H-scenes and logs player body Y / physics / camera offsets.
/// No gameplay changes — diagnostics only.
/// </summary>
internal sealed class TrapPlayerBodyMonitor : MonoBehaviour
{
    private static TrapPlayerBodyMonitor s_instance;

    private float _heartbeatNextAt;
    private bool _wasEnabled;
    private bool _wasActiveH;
    private float _grabPlayerY = float.NaN;
    private float _prevPlayerY = float.NaN;
    private bool _prevSimulated;
    private bool _prevEroflag;
    private int _prevErodown = int.MinValue;
    private bool _prevSousa;
    private string _activeTrapName = "";

    public static void Ensure()
    {
        if (s_instance != null && s_instance.gameObject != null)
            return;

        try
        {
            GameObject host = new GameObject("TrapPlayerBodyMonitor_XUAIGNORE");
            Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            s_instance = host.AddComponent<TrapPlayerBodyMonitor>();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[TrapBodyDiag] Ensure failed: " + ex.Message);
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        Plugin.Log?.LogInfo(
            "[TrapBodyDiag] monitor ready (toggle HellGateJson/Diagnostics/TrapPlayerBodyDiagnostics.json Enable);" +
            " file=" + TrapPlayerBodyDiagnosticsConfig.GetLogFilePath());
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResetTracking("scene:" + scene.name);
        if (TrapPlayerBodyDiagnosticsConfig.Enable)
            TrapPlayerBodyDiagLog.Info("scene loaded: " + scene.name);
    }

    private void LateUpdate()
    {
        bool enabled = TrapPlayerBodyDiagnosticsConfig.Enable;
        if (enabled && !_wasEnabled)
        {
            TrapPlayerBodyDiagLog.ResetSession();
            TrapPlayerBodyDiagLog.Info("ENABLED");
        }
        else if (!enabled && _wasEnabled)
        {
            Plugin.Log?.LogInfo("[TrapBodyDiag] DISABLED");
        }

        _wasEnabled = enabled;
        if (!enabled)
            return;

        try
        {
            Poll();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[TrapBodyDiag] LateUpdate: " + ex.Message);
        }
    }

    private void Poll()
    {
        playercon player = UnifiedPlayerCacheManager.GetPlayer();
        PlayerStatus status = UnifiedPlayerCacheManager.GetPlayerStatus();
        Trapdata activeTrap = FindActiveTrap(player);

        bool activeH = activeTrap != null || (player != null && player.eroflag && HasAnyTrapVisual());
        if (activeH && !_wasActiveH)
            OnHStarted(player, status, activeTrap);
        else if (!activeH && _wasActiveH)
            OnHEnded(player, status);

        _wasActiveH = activeH;
        if (!activeH || player == null)
            return;

        DetectTransitions(player, status, activeTrap);

        if (Time.unscaledTime >= _heartbeatNextAt)
        {
            _heartbeatNextAt = Time.unscaledTime + TrapPlayerBodyDiagnosticsConfig.HeartbeatSec;
            LogHeartbeat(player, status, activeTrap, "HB");
        }
    }

    private void OnHStarted(playercon player, PlayerStatus status, Trapdata trap)
    {
        _grabPlayerY = player != null ? player.transform.position.y : float.NaN;
        _prevPlayerY = _grabPlayerY;
        _prevSimulated = player != null && player.rigi2d != null && player.rigi2d.simulated;
        _prevEroflag = player != null && player.eroflag;
        _prevErodown = player != null ? player.erodown : int.MinValue;
        _prevSousa = status != null && status._SOUSA;
        _activeTrapName = DescribeTrapName(trap);
        _heartbeatNextAt = 0f;

        TrapPlayerBodyDiagLog.Info(
            "H_START trap=" + _activeTrapName + " | " + DescribePlayer(player, status) +
            " | " + DescribeTrap(trap) + " | " + DescribeCamera());
    }

    private void OnHEnded(playercon player, PlayerStatus status)
    {
        float endY = player != null ? player.transform.position.y : float.NaN;
        float drop = (!float.IsNaN(_grabPlayerY) && !float.IsNaN(endY)) ? (_grabPlayerY - endY) : float.NaN;
        TrapPlayerBodyDiagLog.Info(
            "H_END trap=" + _activeTrapName +
            " grabY=" + F(_grabPlayerY) + " endY=" + F(endY) + " dropFromGrab=" + F(drop) +
            " | " + DescribePlayer(player, status) + " | " + DescribeCamera());
        ResetTracking("h_end");
    }

    private void DetectTransitions(playercon player, PlayerStatus status, Trapdata trap)
    {
        float y = player.transform.position.y;
        bool simulated = player.rigi2d != null && player.rigi2d.simulated;
        bool eroflag = player.eroflag;
        int erodown = player.erodown;
        bool sousa = status != null && status._SOUSA;

        if (simulated && !_prevSimulated)
        {
            string stack = TrapPlayerBodyDiagnosticsConfig.LogStackTraceOnSimulatedEnable
                ? "\n" + Environment.StackTrace
                : "";
            TrapPlayerBodyDiagLog.Warn(
                "SIMULATED false->true | " + DescribePlayer(player, status) +
                " | " + DescribeTrap(trap) + stack);
        }
        else if (!simulated && _prevSimulated)
        {
            TrapPlayerBodyDiagLog.Info(
                "SIMULATED true->false | " + DescribePlayer(player, status) + " | " + DescribeTrap(trap));
        }

        if (eroflag != _prevEroflag)
            TrapPlayerBodyDiagLog.Info("eroflag " + _prevEroflag + "->" + eroflag + " | " + DescribePlayer(player, status));

        if (erodown != _prevErodown)
            TrapPlayerBodyDiagLog.Info("erodown " + _prevErodown + "->" + erodown + " | " + DescribePlayer(player, status));

        if (sousa != _prevSousa)
            TrapPlayerBodyDiagLog.Info("SOUSA " + _prevSousa + "->" + sousa + " | " + DescribePlayer(player, status));

        if (!float.IsNaN(_prevPlayerY))
        {
            float dy = _prevPlayerY - y;
            if (dy >= TrapPlayerBodyDiagnosticsConfig.YDropWarnThreshold)
            {
                TrapPlayerBodyDiagLog.Warn(
                    "Y_DROP dy=" + F(dy) + " y=" + F(_prevPlayerY) + "->" + F(y) +
                    " sinceGrab=" + F(!float.IsNaN(_grabPlayerY) ? _grabPlayerY - y : float.NaN) +
                    " | " + DescribePlayer(player, status) + " | " + DescribeTrap(trap));
            }
        }

        _prevPlayerY = y;
        _prevSimulated = simulated;
        _prevEroflag = eroflag;
        _prevErodown = erodown;
        _prevSousa = sousa;
        if (trap != null)
            _activeTrapName = DescribeTrapName(trap);
    }

    private void LogHeartbeat(playercon player, PlayerStatus status, Trapdata trap, string tag)
    {
        float sinceGrab = (!float.IsNaN(_grabPlayerY))
            ? _grabPlayerY - player.transform.position.y
            : float.NaN;
        TrapPlayerBodyDiagLog.Info(
            tag + " sinceGrabDrop=" + F(sinceGrab) +
            " | " + DescribePlayer(player, status) +
            " | " + DescribeTrap(trap) +
            " | " + DescribeCamera());
    }

    internal static void LogEvent(string source, playercon player, string extra)
    {
        if (!TrapPlayerBodyDiagnosticsConfig.Enable)
            return;

        PlayerStatus status = UnifiedPlayerCacheManager.GetPlayerStatus();
        Trapdata trap = FindActiveTrap(player);
        TrapPlayerBodyDiagLog.Info(
            "EVT " + source +
            (string.IsNullOrEmpty(extra) ? "" : " " + extra) +
            " | " + DescribePlayer(player, status) +
            " | " + DescribeTrap(trap) +
            " | " + DescribeCamera());
    }

    internal static void LogEventWithOptionalStack(string source, playercon player, string extra, bool withStack)
    {
        if (!TrapPlayerBodyDiagnosticsConfig.Enable)
            return;

        string stack = withStack ? "\n" + Environment.StackTrace : "";
        PlayerStatus status = UnifiedPlayerCacheManager.GetPlayerStatus();
        Trapdata trap = FindActiveTrap(player);
        TrapPlayerBodyDiagLog.Warn(
            "EVT " + source +
            (string.IsNullOrEmpty(extra) ? "" : " " + extra) +
            " | " + DescribePlayer(player, status) +
            " | " + DescribeTrap(trap) +
            " | " + DescribeCamera() +
            stack);
    }

    private void ResetTracking(string reason)
    {
        _grabPlayerY = float.NaN;
        _prevPlayerY = float.NaN;
        _prevSimulated = false;
        _prevEroflag = false;
        _prevErodown = int.MinValue;
        _prevSousa = false;
        _activeTrapName = "";
        _wasActiveH = false;
        _heartbeatNextAt = 0f;
    }

    private static Trapdata FindActiveTrap(playercon player)
    {
        Trapdata[] traps;
        try
        {
            traps = Object.FindObjectsOfType<Trapdata>();
        }
        catch
        {
            return null;
        }

        if (traps == null || traps.Length == 0)
            return null;

        Trapdata best = null;
        float bestDist = float.MaxValue;
        Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;

        for (int i = 0; i < traps.Length; i++)
        {
            Trapdata trap = traps[i];
            if (trap == null || !IsTrapInActiveHScene(trap))
                continue;

            if (!TrapPlayerBodyDiagnosticsConfig.WatchAllTraps && !(trap is Ivy_monster))
                continue;

            float dist = player != null
                ? (trap.transform.position - playerPos).sqrMagnitude
                : 0f;
            if (best == null || dist < bestDist)
            {
                best = trap;
                bestDist = dist;
            }
        }

        return best;
    }

    private static bool HasAnyTrapVisual()
    {
        try
        {
            Trapdata[] traps = Object.FindObjectsOfType<Trapdata>();
            if (traps == null)
                return false;
            for (int i = 0; i < traps.Length; i++)
            {
                if (traps[i] != null && IsTrapInActiveHScene(traps[i]))
                    return true;
            }
        }
        catch
        {
        }

        return false;
    }

    private static bool IsTrapInActiveHScene(Trapdata trap)
    {
        if (trap.eroflag)
            return true;
        GameObject ero = trap.erodata;
        return ero != null && ero.activeSelf;
    }

    private static string DescribeTrapName(Trapdata trap)
    {
        if (trap == null)
            return "none";
        return trap.GetType().Name + "#" + trap.GetInstanceID();
    }

    private static string DescribePlayer(playercon player, PlayerStatus status)
    {
        if (player == null)
            return "player=null";

        Vector3 p = player.transform.position;
        Vector2 vel = player.rigi2d != null ? player.rigi2d.velocity : Vector2.zero;
        bool sim = player.rigi2d != null && player.rigi2d.simulated;
        float gravity = player.rigi2d != null ? player.rigi2d.gravityScale : float.NaN;
        bool sousa = status != null && status._SOUSA;
        bool sousamng = status != null && status._SOUSAMNG;

        return "playerY=" + F(p.y) +
               " pos=(" + F(p.x) + "," + F(p.y) + ")" +
               " sim=" + sim +
               " velY=" + F(vel.y) +
               " grav=" + F(gravity) +
               " eroflag=" + player.eroflag +
               " erodown=" + player.erodown +
               " state=" + player.state +
               " grounded=" + player.m_Grounded +
               " SOUSA=" + sousa +
               " SOUSAMNG=" + sousamng +
               " Death=" + player._Death;
    }

    private static string DescribeTrap(Trapdata trap)
    {
        if (trap == null)
            return "trap=none";

        Vector3 root = trap.transform.position;
        GameObject ero = trap.erodata;
        string eroInfo = "erodata=null";
        if (ero != null)
        {
            Vector3 ep = ero.transform.position;
            eroInfo = "erodataActive=" + ero.activeSelf +
                      " erodataY=" + F(ep.y) +
                      " erodataPos=(" + F(ep.x) + "," + F(ep.y) + ")" +
                      " rootToEroY=" + F(ep.y - root.y);
        }

        return "trap=" + DescribeTrapName(trap) +
               " trapEroflag=" + trap.eroflag +
               " rootY=" + F(root.y) +
               " rootPos=(" + F(root.x) + "," + F(root.y) + ")" +
               " " + eroInfo;
    }

    private static string DescribeCamera()
    {
        try
        {
            var cam = UnifiedCameraCacheManager.GetProCamera2D();
            if (cam == null)
                return "cam=null";

            Vector3 local = cam.LocalPosition;
            Vector2 offset = cam.OverallOffset;
            return "camLocalY=" + F(local.y) +
                   " camLocal=(" + F(local.x) + "," + F(local.y) + ")" +
                   " offsetY=" + F(offset.y) +
                   " hSmooth=" + F(cam.HorizontalFollowSmoothness) +
                   " vSmooth=" + F(cam.VerticalFollowSmoothness);
        }
        catch (Exception ex)
        {
            return "cam=err:" + ex.Message;
        }
    }

    private static string F(float v)
    {
        if (float.IsNaN(v))
            return "NaN";
        return v.ToString("0.000");
    }
}
