using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Spine;

namespace NoREroMod.Patches.Enemy;

/// <summary>
/// Rewrites Spine 3.7/3.8 skeleton JSON into the layout the game's Spine 3.4-era <see cref="SkeletonJson"/> reads:
/// skins as an object, explicit frame time/angle, bezier curves as [cx1, cy1, cx2, cy2], bone transform modes
/// as inheritRotation/inheritScale, scale keys defaulting to 1. Older exports are returned unchanged.
/// </summary>
internal static class SpineJsonLegacyConverter
{
    internal static string ConvertIfNeeded(string jsonText, string logName)
    {
        if (string.IsNullOrEmpty(jsonText))
            return jsonText;

        Dictionary<string, object> root = Json.Deserialize(new StringReader(jsonText)) as Dictionary<string, object>;
        if (root == null || !NeedsConversion(root))
            return jsonText;

        ConvertBones(root);
        ConvertSkins(root);
        if (root.TryGetValue("animations", out object animations) && animations is Dictionary<string, object> animMap)
        {
            foreach (KeyValuePair<string, object> kv in animMap)
            {
                if (kv.Value is Dictionary<string, object> anim)
                    ConvertAnimation(anim);
            }
        }

        Plugin.Log?.LogInfo("[SpineJsonLegacyConverter] Converted " + logName + " from Spine " + GetVersion(root) + " JSON");
        var sb = new StringBuilder(jsonText.Length);
        Write(sb, root);
        return sb.ToString();
    }

    private static string GetVersion(Dictionary<string, object> root)
    {
        if (root.TryGetValue("skeleton", out object sk) && sk is Dictionary<string, object> skeleton
            && skeleton.TryGetValue("spine", out object v) && v is string version)
            return version;
        return "?";
    }

    private static bool NeedsConversion(Dictionary<string, object> root)
    {
        if (root.TryGetValue("skins", out object skins) && skins is List<object>)
            return true;
        string version = GetVersion(root);
        return version.StartsWith("3.7", StringComparison.Ordinal) || version.StartsWith("3.8", StringComparison.Ordinal);
    }

    private static void ConvertBones(Dictionary<string, object> root)
    {
        if (!root.TryGetValue("bones", out object bones) || !(bones is List<object> list))
            return;

        foreach (object o in list)
        {
            if (!(o is Dictionary<string, object> bone) || !bone.TryGetValue("transform", out object t) || !(t is string mode))
                continue;

            bone.Remove("transform");
            switch (mode)
            {
                case "onlyTranslation":
                    bone["inheritRotation"] = false;
                    bone["inheritScale"] = false;
                    break;
                case "noRotationOrReflection":
                    bone["inheritRotation"] = false;
                    break;
                case "noScale":
                case "noScaleOrReflection":
                    bone["inheritScale"] = false;
                    break;
            }
        }
    }

    private static void ConvertSkins(Dictionary<string, object> root)
    {
        if (!root.TryGetValue("skins", out object skins))
            return;

        Dictionary<string, object> skinMap;
        if (skins is List<object> skinList)
        {
            skinMap = new Dictionary<string, object>();
            foreach (object o in skinList)
            {
                if (!(o is Dictionary<string, object> skin) || !(skin.TryGetValue("name", out object n) && n is string name))
                    continue;
                skinMap[name] = skin.TryGetValue("attachments", out object att) && att is Dictionary<string, object>
                    ? att
                    : new Dictionary<string, object>();
            }
            root["skins"] = skinMap;
        }
        else
        {
            skinMap = skins as Dictionary<string, object>;
        }

        if (skinMap == null)
            return;

        // Attachment types unknown to the old runtime make Enum.Parse throw.
        foreach (object slots in skinMap.Values)
        {
            if (!(slots is Dictionary<string, object> slotMap))
                continue;
            foreach (object atts in slotMap.Values)
            {
                if (!(atts is Dictionary<string, object> attMap))
                    continue;
                var drop = new List<string>();
                foreach (KeyValuePair<string, object> kv in attMap)
                {
                    if (kv.Value is Dictionary<string, object> a && a.TryGetValue("type", out object type)
                        && (Equals(type, "clipping") || Equals(type, "point")))
                        drop.Add(kv.Key);
                }
                foreach (string key in drop)
                    attMap.Remove(key);
            }
        }
    }

    private static void ConvertAnimation(Dictionary<string, object> anim)
    {
        if (anim.TryGetValue("slots", out object slots) && slots is Dictionary<string, object> slotMap)
        {
            foreach (object s in slotMap.Values)
            {
                if (!(s is Dictionary<string, object> timelines))
                    continue;
                if (timelines.TryGetValue("twoColor", out object twoColor) && twoColor is List<object> twoColorFrames)
                {
                    timelines.Remove("twoColor");
                    if (!timelines.ContainsKey("color"))
                    {
                        foreach (object f in twoColorFrames)
                        {
                            if (f is Dictionary<string, object> frame && frame.TryGetValue("light", out object light))
                            {
                                frame["color"] = light;
                                frame.Remove("light");
                                frame.Remove("dark");
                            }
                        }
                        timelines["color"] = twoColorFrames;
                    }
                }
                foreach (object frames in timelines.Values)
                    ConvertFrames(frames, null);
            }
        }

        if (anim.TryGetValue("bones", out object bones) && bones is Dictionary<string, object> boneMap)
        {
            foreach (object b in boneMap.Values)
            {
                if (!(b is Dictionary<string, object> timelines))
                    continue;
                foreach (KeyValuePair<string, object> kv in timelines)
                    ConvertFrames(kv.Value, kv.Key);
            }
        }

        ConvertTimelineGroup(anim, "ik");
        ConvertTimelineGroup(anim, "transform");

        if (anim.TryGetValue("paths", out object paths) && paths is Dictionary<string, object> pathMap)
        {
            foreach (object p in pathMap.Values)
            {
                if (p is Dictionary<string, object> timelines)
                {
                    foreach (object frames in timelines.Values)
                        ConvertFrames(frames, null);
                }
            }
        }

        if (anim.TryGetValue("deform", out object deform) && deform is Dictionary<string, object> skinMap)
        {
            foreach (object skin in skinMap.Values)
            {
                if (!(skin is Dictionary<string, object> slotsOfSkin))
                    continue;
                foreach (object slot in slotsOfSkin.Values)
                {
                    if (!(slot is Dictionary<string, object> attMap))
                        continue;
                    foreach (object frames in attMap.Values)
                        ConvertFrames(frames, null);
                }
            }
        }

        if (anim.TryGetValue("drawOrder", out object drawOrder))
            ConvertFrames(drawOrder, null);
        if (anim.TryGetValue("draworder", out object drawOrderLower))
            ConvertFrames(drawOrderLower, null);
        if (anim.TryGetValue("events", out object events))
            ConvertFrames(events, null);
    }

    private static void ConvertTimelineGroup(Dictionary<string, object> anim, string key)
    {
        if (!anim.TryGetValue(key, out object group) || !(group is Dictionary<string, object> map))
            return;
        foreach (object frames in map.Values)
            ConvertFrames(frames, null);
    }

    private static void ConvertFrames(object frames, string boneTimeline)
    {
        if (!(frames is List<object> list))
            return;

        foreach (object o in list)
        {
            if (!(o is Dictionary<string, object> frame))
                continue;

            if (!frame.ContainsKey("time"))
                frame["time"] = 0f;

            if (boneTimeline == "rotate" && !frame.ContainsKey("angle"))
                frame["angle"] = 0f;
            else if (boneTimeline == "scale")
            {
                if (!frame.ContainsKey("x"))
                    frame["x"] = 1f;
                if (!frame.ContainsKey("y"))
                    frame["y"] = 1f;
            }

            if (frame.TryGetValue("curve", out object curve) && curve is float cx1)
            {
                frame["curve"] = new List<object>
                {
                    cx1,
                    GetFloat(frame, "c2", 0f),
                    GetFloat(frame, "c3", 1f),
                    GetFloat(frame, "c4", 1f)
                };
            }
            frame.Remove("c2");
            frame.Remove("c3");
            frame.Remove("c4");
        }
    }

    private static float GetFloat(Dictionary<string, object> map, string key, float fallback)
    {
        return map.TryGetValue(key, out object v) && v is float f ? f : fallback;
    }

    private static void Write(StringBuilder sb, object value)
    {
        switch (value)
        {
            case null:
                sb.Append("null");
                break;
            case string s:
                WriteString(sb, s);
                break;
            case bool b:
                sb.Append(b ? "true" : "false");
                break;
            case float f:
                sb.Append(f.ToString("R", CultureInfo.InvariantCulture));
                break;
            case double d:
                sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                break;
            case int i:
                sb.Append(i.ToString(CultureInfo.InvariantCulture));
                break;
            case long l:
                sb.Append(l.ToString(CultureInfo.InvariantCulture));
                break;
            case Dictionary<string, object> map:
            {
                sb.Append('{');
                bool first = true;
                foreach (KeyValuePair<string, object> kv in map)
                {
                    if (!first)
                        sb.Append(',');
                    first = false;
                    WriteString(sb, kv.Key);
                    sb.Append(':');
                    Write(sb, kv.Value);
                }
                sb.Append('}');
                break;
            }
            case List<object> list:
            {
                sb.Append('[');
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0)
                        sb.Append(',');
                    Write(sb, list[i]);
                }
                sb.Append(']');
                break;
            }
            default:
                WriteString(sb, Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
        }
    }

    private static void WriteString(StringBuilder sb, string s)
    {
        sb.Append('"');
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                default:
                    if (c < ' ')
                        sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else
                        sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
    }
}
