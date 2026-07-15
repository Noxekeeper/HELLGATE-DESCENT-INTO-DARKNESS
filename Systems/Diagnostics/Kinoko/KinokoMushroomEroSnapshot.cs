using System;
using System.Reflection;
using System.Text;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Systems.Diagnostics.Kinoko;

internal static class KinokoMushroomEroSnapshot
{
    private static readonly FieldInfo MyspineField =
        typeof(MushroomERO).GetField("myspine", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? typeof(MushroomERO).GetField("myspine", BindingFlags.Instance | BindingFlags.Public);

    private static readonly FieldInfo GaMyspineField =
        typeof(GAMushroomERO).GetField("myspine", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? typeof(GAMushroomERO).GetField("myspine", BindingFlags.Instance | BindingFlags.Public);

    private static readonly FieldInfo OyaField =
        typeof(MushroomERO).GetField("oya", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? typeof(MushroomERO).GetField("oya", BindingFlags.Instance | BindingFlags.Public);

    public static SkeletonAnimation GetSpine(object instance)
    {
        if (instance == null)
            return null;
        try
        {
            if (instance is MushroomERO && MyspineField != null)
                return MyspineField.GetValue(instance) as SkeletonAnimation;
            if (instance is GAMushroomERO && GaMyspineField != null)
                return GaMyspineField.GetValue(instance) as SkeletonAnimation;
        }
        catch
        {
            // Ignore.
        }

        return null;
    }

    public static string Describe(
        string phase,
        object instance,
        SkeletonAnimation spine,
        Spine.Event e,
        int seCount,
        int count)
    {
        if (spine == null)
            spine = GetSpine(instance);

        var sb = new StringBuilder(256);
        sb.Append(phase);
        sb.Append(" type=").Append(instance != null ? instance.GetType().Name : "null");
        sb.Append(" ev=").Append(EventName(e));
        sb.Append(" anim=").Append(spine != null ? (spine.AnimationName ?? "null") : "null");
        sb.Append(" ts=").Append(spine != null ? spine.timeScale.ToString("0.###") : "?");
        sb.Append(" se=").Append(seCount);
        sb.Append(" count=").Append(count);
        AppendTrack(sb, spine);
        sb.Append(" oya=").Append(HasOya(instance) ? "ok" : "null");
        return sb.ToString();
    }

    public static string DescribeHeartbeat(object instance, SkeletonAnimation spine, int seCount, int count, float sinceEvent)
    {
        if (spine == null)
            spine = GetSpine(instance);

        var sb = new StringBuilder(256);
        sb.Append("HB sinceEvt=").Append(sinceEvent.ToString("0.00"));
        sb.Append(" type=").Append(instance != null ? instance.GetType().Name : "null");
        sb.Append(" anim=").Append(spine != null ? (spine.AnimationName ?? "null") : "null");
        sb.Append(" ts=").Append(spine != null ? spine.timeScale.ToString("0.###") : "?");
        sb.Append(" se=").Append(seCount);
        sb.Append(" count=").Append(count);
        AppendTrack(sb, spine);
        return sb.ToString();
    }

    public static string EventName(Spine.Event e)
    {
        if (e == null)
            return "null";
        try
        {
            // Match NoREroMod / Spine 3.6: Event.ToString() == event data name.
            string s = e.ToString();
            if (!string.IsNullOrEmpty(s))
                return s;
        }
        catch
        {
            // Ignore.
        }

        return "?";
    }

    public static bool LooksStuck(SkeletonAnimation spine)
    {
        if (spine == null || spine.state == null)
            return false;

        string anim = spine.AnimationName ?? string.Empty;
        if (!KinokoMushroomEroDiagnosticsConfig.IsInterestingEventOrAnim(string.Empty, anim))
            return false;

        if (spine.timeScale <= 0.0001f)
            return true;

        try
        {
            TrackEntry entry = spine.state.GetCurrent(0);
            if (entry == null)
                return true;

            // Older Spine (3.6): Time / Animation.Duration (not TrackTime / AnimationEnd).
            float time = entry.Time;
            float duration = entry.Animation != null ? entry.Animation.Duration : 0f;
            if (!entry.Loop && duration > 0f && time >= duration - 0.05f)
                return true;
        }
        catch
        {
            // Ignore Spine API quirks.
        }

        return false;
    }

    private static void AppendTrack(StringBuilder sb, SkeletonAnimation spine)
    {
        if (spine == null || spine.state == null)
        {
            sb.Append(" track=?");
            return;
        }

        try
        {
            TrackEntry entry = spine.state.GetCurrent(0);
            if (entry == null)
            {
                sb.Append(" track=null");
                return;
            }

            float time = entry.Time;
            float duration = entry.Animation != null ? entry.Animation.Duration : 0f;
            sb.Append(" t=").Append(time.ToString("0.00"));
            sb.Append("/").Append(duration.ToString("0.00"));
            sb.Append(" loop=").Append(entry.Loop);
            bool done = !entry.Loop && duration > 0f && time >= duration - 0.02f;
            sb.Append(" done=").Append(done);
        }
        catch
        {
            sb.Append(" track=?");
        }
    }

    private static bool HasOya(object instance)
    {
        if (!(instance is MushroomERO) || OyaField == null)
            return false;
        try
        {
            return OyaField.GetValue(instance) != null;
        }
        catch
        {
            return false;
        }
    }
}
