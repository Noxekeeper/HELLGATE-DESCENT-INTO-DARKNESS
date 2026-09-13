using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NoREroMod.Systems.Cache;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// F11 map-chunk marquee: LMB-drag on empty ground, then Ctrl+C / Ctrl+V.
/// A click without drag stays a Point (Last LMB).
/// </summary>
internal static class SpawnAuthoringRegion
{
    private const float MinSize = 0.35f;
    private const float ContainPad = 0.08f;
    private const float DragThresholdPx = 6f;

    internal struct Item
    {
        internal string Line;
        internal string Key;
        internal float WorldX;
        internal float WorldY;
    }

    private static bool armed;
    private static bool drawing;
    private static bool hasRect;
    private static bool pendingPointClick;
    private static Vector2 start;
    private static Vector2 current;
    private static Vector2 downScreen;
    private static int cachedCount;
    private static readonly List<Item> cachedItems = new List<Item>(32);
    private static string cachedSummary = string.Empty;
    private static string[] cachedRows = new string[0];

    internal static bool IsDrawing => drawing;
    internal static bool HasRect => hasRect;
    internal static bool IsArmed => armed || drawing || hasRect;

    internal static float MinX => Mathf.Min(start.x, current.x);
    internal static float MaxX => Mathf.Max(start.x, current.x);
    internal static float MinY => Mathf.Min(start.y, current.y);
    internal static float MaxY => Mathf.Max(start.y, current.y);

    internal static void Clear()
    {
        armed = false;
        drawing = false;
        hasRect = false;
        pendingPointClick = false;
        cachedCount = 0;
        cachedItems.Clear();
        cachedSummary = string.Empty;
        cachedRows = new string[0];
    }

    internal static bool TryBeginAtMouse()
    {
        if (!SpawnAuthoringWorldPick.TryGetMouseWorld(out Vector2 mouse))
            return false;

        start = mouse;
        current = mouse;
        downScreen = Input.mousePosition;
        armed = true;
        drawing = false;
        pendingPointClick = false;
        return true;
    }

    internal static bool ConsumeClickForPoint()
    {
        if (!pendingPointClick)
            return false;
        pendingPointClick = false;
        return true;
    }

    internal static void Tick(bool pointerOverUi)
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SpawnAuthoringOverlayHost.TryHandleEscape();
            return;
        }

        if (!armed && !drawing)
            return;

        if (!pointerOverUi && SpawnAuthoringWorldPick.TryGetMouseWorld(out Vector2 mouse))
            current = mouse;

        if (armed && Input.GetMouseButton(0) && !drawing)
        {
            Vector2 screen = Input.mousePosition;
            float dx = screen.x - downScreen.x;
            float dy = screen.y - downScreen.y;
            if (dx * dx + dy * dy >= DragThresholdPx * DragThresholdPx)
            {
                drawing = true;
                hasRect = false;
                cachedCount = 0;
            }
        }

        if (!Input.GetMouseButtonUp(0))
            return;

        if (drawing && (MaxX - MinX) >= MinSize && (MaxY - MinY) >= MinSize)
        {
            hasRect = true;
            RebuildCache();
        }
        else if (armed && !drawing)
        {
            pendingPointClick = true;
        }

        armed = false;
        drawing = false;
    }

    internal static bool Contains(float x, float y)
    {
        if (!hasRect && !drawing)
            return false;
        return x >= MinX - ContainPad && x <= MaxX + ContainPad &&
               y >= MinY - ContainPad && y <= MaxY + ContainPad;
    }

    internal static int CountItems()
    {
        RebuildCache();
        return cachedCount;
    }

    internal static string FormatSummary()
    {
        if (!hasRect)
            return string.Empty;
        if (string.IsNullOrEmpty(cachedSummary))
            RebuildCache();
        return cachedSummary;
    }

    internal static void CollectLiveManaged(List<SpawnManagedInstance> into)
    {
        if (into == null)
            return;
        into.Clear();
        if (!hasRect && !drawing)
            return;

        SpawnManagedInstance[] markers = Object.FindObjectsOfType<SpawnManagedInstance>();
        for (int i = 0; i < markers.Length; i++)
        {
            SpawnManagedInstance managed = markers[i];
            if (managed == null || !managed.HasAuthoringLink || managed.gameObject == null)
                continue;
            Vector3 p = managed.transform.position;
            if (!Contains(p.x, p.y))
                continue;
            into.Add(managed);
        }
    }

    private static void RebuildCache()
    {
        Collect(out List<Item> items);
        cachedItems.Clear();
        if (items != null)
            cachedItems.AddRange(items);
        cachedCount = cachedItems.Count;
        cachedRows = BuildRows(cachedItems);
        if (cachedCount <= 0)
        {
            cachedSummary = "0";
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.Append(cachedCount.ToString(CultureInfo.InvariantCulture));
        if (cachedRows.Length > 0)
        {
            sb.Append(" · ");
            for (int i = 0; i < cachedRows.Length; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append(cachedRows[i]);
            }
        }

        cachedSummary = sb.ToString();
    }

    private static string[] BuildRows(List<Item> items)
    {
        if (items == null || items.Count == 0)
            return new string[0];

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        for (int i = 0; i < items.Count; i++)
        {
            string key = items[i].Key;
            if (string.IsNullOrEmpty(key))
                key = "?";
            int n;
            if (!counts.TryGetValue(key, out n))
            {
                order.Add(key);
                counts[key] = 1;
            }
            else
                counts[key] = n + 1;
        }

        var rows = new string[order.Count];
        for (int i = 0; i < order.Count; i++)
        {
            string key = order[i];
            int n = counts[key];
            rows[i] = n > 1
                ? (key + " ×" + n.ToString(CultureInfo.InvariantCulture))
                : key;
        }

        return rows;
    }

    internal static void Collect(out List<Item> items)
    {
        items = new List<Item>(32);
        if (!hasRect && !drawing)
            return;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        SpawnManagedInstance[] markers = Object.FindObjectsOfType<SpawnManagedInstance>();
        for (int i = 0; i < markers.Length; i++)
        {
            SpawnManagedInstance managed = markers[i];
            if (managed == null || !managed.HasAuthoringLink || managed.gameObject == null)
                continue;

            Vector3 p = managed.transform.position;
            if (!Contains(p.x, p.y))
                continue;

            string line = BuildLineForManaged(managed, p.x, p.y);
            if (string.IsNullOrEmpty(line))
                continue;

            string key = managed.AuthoringEnemyKey ?? string.Empty;
            string dedup = Normalize(line);
            if (!seen.Add(dedup))
                continue;

            items.Add(new Item
            {
                Line = line,
                Key = key,
                WorldX = p.x,
                WorldY = p.y
            });
        }

        string packPath = HellGateLocationSpawnRefresh.GetActiveSpawnConfigPath();
        if (string.IsNullOrEmpty(packPath) || !File.Exists(packPath))
            return;

        string[] lines;
        try
        {
            lines = File.ReadAllLines(packPath);
        }
        catch
        {
            return;
        }

        for (int i = 0; i < lines.Length; i++)
        {
            string raw = lines[i];
            if (!SpawnAuthoringPackEdit.TryExtractKeyAndCoords(raw, out string key, out float x, out float y))
                continue;
            if (!Contains(x, y))
                continue;

            string dedup = Normalize(raw);
            if (!seen.Add(dedup))
                continue;

            items.Add(new Item
            {
                Line = raw.Trim(),
                Key = key ?? string.Empty,
                WorldX = x,
                WorldY = y
            });
        }
    }

    internal static void DrawGui()
    {
        if (!drawing && !hasRect)
            return;

        Vector2 a;
        Vector2 b;
        Vector2 c;
        Vector2 d;
        if (!TryWorldToGui(new Vector3(MinX, MinY, 0f), out a) ||
            !TryWorldToGui(new Vector3(MaxX, MinY, 0f), out b) ||
            !TryWorldToGui(new Vector3(MaxX, MaxY, 0f), out c) ||
            !TryWorldToGui(new Vector3(MinX, MaxY, 0f), out d))
            return;

        Color prev = GUI.color;
        GUI.color = drawing
            ? new Color(0.95f, 0.75f, 0.20f, 0.22f)
            : new Color(0.35f, 0.85f, 1f, 0.18f);
        Rect fill = GuiRectFromCorners(a, b, c, d);
        GUI.DrawTexture(fill, Texture2D.whiteTexture);

        GUI.color = drawing
            ? new Color(0.95f, 0.75f, 0.20f, 0.95f)
            : new Color(0.45f, 0.90f, 1f, 0.95f);
        DrawLine(a, b);
        DrawLine(b, c);
        DrawLine(c, d);
        DrawLine(d, a);
        GUI.color = Color.white;

        int n = drawing ? 0 : cachedCount;
        string label = drawing
            ? ((MaxX - MinX).ToString("0.0", CultureInfo.InvariantCulture) + " × " +
               (MaxY - MinY).ToString("0.0", CultureInfo.InvariantCulture))
            : (n.ToString(CultureInfo.InvariantCulture) + "  ·  Ctrl+C / Ctrl+X");
        GUI.Label(new Rect(fill.x + 6f, fill.y - 18f, 280f, 18f), label);

        if (!drawing && cachedRows != null && cachedRows.Length > 0)
            DrawSelectionList(fill);

        GUI.color = prev;
    }

    private static void DrawSelectionList(Rect fill)
    {
        const int maxRows = 12;
        int show = Mathf.Min(maxRows, cachedRows.Length);
        bool more = cachedRows.Length > show;
        float rowH = 16f;
        float h = 8f + show * rowH + (more ? rowH : 0f);
        float w = Mathf.Max(180f, fill.width);
        float x = fill.x;
        float y = fill.yMax + 4f;
        if (y + h > Screen.height - 90f)
            y = fill.y - h - 22f;
        if (y < 4f)
            y = 4f;

        Color prev = GUI.color;
        GUI.color = new Color(0.05f, 0.08f, 0.12f, 0.82f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        GUI.color = Color.white;
        float ly = y + 4f;
        for (int i = 0; i < show; i++)
        {
            GUI.Label(new Rect(x + 8f, ly, w - 16f, rowH), cachedRows[i]);
            ly += rowH;
        }

        if (more)
        {
            int rest = cachedRows.Length - show;
            GUI.Label(
                new Rect(x + 8f, ly, w - 16f, rowH),
                "+" + rest.ToString(CultureInfo.InvariantCulture) + " …");
        }

        GUI.color = prev;
    }

    private static string BuildLineForManaged(SpawnManagedInstance managed, float x, float y)
    {
        string raw = managed.AuthoringSourceLineRaw;
        string relocated;
        if (!string.IsNullOrEmpty(raw) && SpawnAuthoringPackEdit.TryRelocateLine(raw, x, y, out relocated))
            return relocated;

        string key = managed.AuthoringEnemyKey;
        if (string.IsNullOrEmpty(key))
            return null;

        if (SpawnAuthoringPackEdit.IsGoldManagedInstance(managed))
        {
            string goldKey = SpawnAuthoringGoldCatalog.NormalizeRangeToken(key);
            if (string.IsNullOrEmpty(goldKey))
                SpawnAuthoringPackEdit.TryExtractKeyAndCoords(raw, out goldKey, out _, out _);
            goldKey = SpawnAuthoringGoldCatalog.NormalizeRangeToken(goldKey);
            float goldChance = managed.AuthoringChance > 0f ? managed.AuthoringChance : 1f;
            return SpawnAuthoringPackEdit.BuildGoldLine(x, y, goldKey, goldChance, 1);
        }

        if (SpawnAuthoringPackEdit.IsTrapManagedInstance(managed))
        {
            return SpawnAuthoringPackEdit.BuildTrapLine(
                x,
                y,
                key,
                1,
                managed.AuthoringFlipX,
                managed.AuthoringRotationZ,
                managed.AuthoringSortOffset,
                managed.AuthoringFactionIdRaw);
        }

        float chance = managed.AuthoringChance > 0f ? managed.AuthoringChance : 1f;
        return SpawnAuthoringPackEdit.BuildEnemyLine(
            x,
            y,
            key,
            managed.AuthoringFactionIdRaw,
            chance,
            1,
            managed.AuthoringFlipX,
            managed.AuthoringForceElite || managed.ForceElite);
    }

    private static string Normalize(string line)
    {
        return string.IsNullOrEmpty(line) ? string.Empty : line.Trim().Replace("\r", string.Empty);
    }

    private static Rect GuiRectFromCorners(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        float minX = Mathf.Min(Mathf.Min(a.x, b.x), Mathf.Min(c.x, d.x));
        float maxX = Mathf.Max(Mathf.Max(a.x, b.x), Mathf.Max(c.x, d.x));
        float minY = Mathf.Min(Mathf.Min(a.y, b.y), Mathf.Min(c.y, d.y));
        float maxY = Mathf.Max(Mathf.Max(a.y, b.y), Mathf.Max(c.y, d.y));
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    private static void DrawLine(Vector2 from, Vector2 to)
    {
        Vector2 delta = to - from;
        float len = delta.magnitude;
        if (len < 1f)
            return;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        Vector2 mid = (from + to) * 0.5f;
        Matrix4x4 matrix = GUI.matrix;
        GUIUtility.RotateAroundPivot(angle, mid);
        GUI.DrawTexture(new Rect(mid.x - len * 0.5f, mid.y - 1f, len, 2f), Texture2D.whiteTexture);
        GUI.matrix = matrix;
    }

    private static bool TryWorldToGui(Vector3 world, out Vector2 gui)
    {
        gui = default;
        GameObject camGo = UnifiedCameraCacheManager.GetMainCamera();
        global::UnityEngine.Camera cam = camGo != null ? camGo.GetComponent<global::UnityEngine.Camera>() : null;
        if (cam == null)
            cam = global::UnityEngine.Camera.main;
        if (cam == null)
            return false;

        Vector3 sp = cam.WorldToScreenPoint(world);
        if (sp.z < 0f)
            return false;
        gui = new Vector2(sp.x, Screen.height - sp.y);
        return true;
    }
}
