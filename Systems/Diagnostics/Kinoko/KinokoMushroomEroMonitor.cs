using System;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Systems.Diagnostics.Kinoko;

/// <summary>
/// Heartbeat while a MushroomERO / GAMushroomERO spine is active.
/// Warns when START6/ERO (or any watched clip) looks stuck waiting for the next event.
/// </summary>
internal sealed class KinokoMushroomEroMonitor : MonoBehaviour
{
    private static KinokoMushroomEroMonitor s_instance;

    private bool _wasEnabled;
    private float _heartbeatNextAt;
    private float _lastEventAt = -999f;
    private float _stuckWarnedAt = -999f;
    private object _lastInstance;
    private SkeletonAnimation _lastSpine;

    public static void Ensure()
    {
        if (s_instance != null)
            return;

        try
        {
            GameObject host = new GameObject("KinokoMushroomEroMonitor_XUAIGNORE");
            DontDestroyOnLoad(host);
            s_instance = host.AddComponent<KinokoMushroomEroMonitor>();
            Plugin.Log?.LogInfo(
                "[KinokoEroDiag] monitor ready (toggle HellGateJson/Diagnostics/KinokoMushroomEroDiagnostics.json Enable);" +
                " file=" + KinokoMushroomEroDiagnosticsConfig.GetLogFilePath());
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[KinokoEroDiag] Ensure failed: " + ex.Message);
        }
    }

    public static void NotifyEvent(object instance, SkeletonAnimation spine)
    {
        if (s_instance == null)
            Ensure();
        if (s_instance == null)
            return;

        s_instance._lastEventAt = Time.unscaledTime;
        s_instance._lastInstance = instance;
        s_instance._lastSpine = spine != null ? spine : KinokoMushroomEroSnapshot.GetSpine(instance);
        s_instance._stuckWarnedAt = -999f;
    }

    private void LateUpdate()
    {
        try
        {
            bool enabled = KinokoMushroomEroDiagnosticsConfig.Enable;
            if (enabled != _wasEnabled)
            {
                _wasEnabled = enabled;
                if (enabled)
                {
                    KinokoMushroomEroDiagLog.ResetSession();
                    KinokoMushroomEroDiagLog.Info("ENABLED");
                }
                else
                {
                    Plugin.Log?.LogInfo("[KinokoEroDiag] DISABLED");
                }
            }

            if (!enabled)
                return;

            if (Time.unscaledTime < _heartbeatNextAt)
                return;

            _heartbeatNextAt = Time.unscaledTime + KinokoMushroomEroDiagnosticsConfig.HeartbeatSec;
            TickHeartbeat();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[KinokoEroDiag] LateUpdate: " + ex.Message);
        }
    }

    private void TickHeartbeat()
    {
        object instance = _lastInstance;
        SkeletonAnimation spine = _lastSpine;

        if (spine == null || !spine.isActiveAndEnabled)
        {
            MushroomERO mush = UnityEngine.Object.FindObjectOfType<MushroomERO>();
            if (mush != null && mush.isActiveAndEnabled)
            {
                instance = mush;
                spine = KinokoMushroomEroSnapshot.GetSpine(mush);
            }
            else
            {
                GAMushroomERO ga = UnityEngine.Object.FindObjectOfType<GAMushroomERO>();
                if (ga != null && ga.isActiveAndEnabled)
                {
                    instance = ga;
                    spine = KinokoMushroomEroSnapshot.GetSpine(ga);
                }
            }
        }

        if (spine == null || !spine.isActiveAndEnabled)
            return;

        string anim = spine.AnimationName ?? string.Empty;
        if (!KinokoMushroomEroDiagnosticsConfig.IsInterestingEventOrAnim(string.Empty, anim))
            return;

        int se = 0;
        int count = 0;
        try
        {
            if (instance is MushroomERO m)
            {
                se = m.se_count;
                count = m.count;
            }
            else if (instance is GAMushroomERO g)
            {
                se = g.se_count;
                count = g.count;
            }
        }
        catch
        {
            // Ignore.
        }

        float sinceEvt = _lastEventAt < 0f ? -1f : (Time.unscaledTime - _lastEventAt);
        KinokoMushroomEroDiagLog.Info(
            KinokoMushroomEroSnapshot.DescribeHeartbeat(instance, spine, se, count, sinceEvt));

        if (KinokoMushroomEroSnapshot.LooksStuck(spine)
            && sinceEvt >= KinokoMushroomEroDiagnosticsConfig.StuckWarnSec
            && Time.unscaledTime - _stuckWarnedAt >= KinokoMushroomEroDiagnosticsConfig.StuckWarnSec)
        {
            _stuckWarnedAt = Time.unscaledTime;
            KinokoMushroomEroDiagLog.Warn(
                "STUCK? " + KinokoMushroomEroSnapshot.DescribeHeartbeat(instance, spine, se, count, sinceEvt)
                + " (no OnEvent for " + sinceEvt.ToString("0.00") + "s while clip looks finished/paused)");
        }
    }
}
