using System.Globalization;
using NoREroMod.Systems.Cache;
using UnityEngine;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// F11 authoring markers for a locked enemy: pack SPAWN vs NOW (and EDIT preview).
/// Selection outline frame intentionally omitted — it was unreliable on this Unity build.
/// </summary>
internal static class SpawnAuthoringOutline
{
    private static bool editPreviewActive;
    private static float editPreviewX;
    private static float editPreviewY;

    private static GUIStyle labelStyle;
    private static GUIStyle boxStyle;

    internal static void Show(GameObject target, bool isSelected)
    {
        // Kept for WorldPick call sites; visual frame removed.
    }

    internal static void Hide()
    {
        ClearEditPreview();
    }

    internal static void SetEditPreview(bool active, float x, float y)
    {
        editPreviewActive = active;
        editPreviewX = x;
        editPreviewY = y;
    }

    internal static void ClearEditPreview()
    {
        editPreviewActive = false;
    }

    internal static void TickFollow()
    {
    }

    internal static void Destroy()
    {
        Hide();
    }

    internal static void DrawGui()
    {
        DrawLockedSpawnMarkers();
    }

    private static void DrawLockedSpawnMarkers()
    {
        GameObject locked = SpawnAuthoringWorldPick.Locked;
        if (locked == null)
            return;

        EnsureStyles();

        Vector3 nowWorld = locked.transform.position;
        if (!TryWorldToGui(nowWorld, out Vector2 nowGui))
            return;

        SpawnManagedInstance managed = locked.GetComponent<SpawnManagedInstance>()
            ?? locked.GetComponentInChildren<SpawnManagedInstance>(true);

        bool hasLink = managed != null && managed.HasAuthoringLink;
        Vector2 spawnGui = nowGui;
        float spawnX = nowWorld.x;
        float spawnY = nowWorld.y;
        bool drawSpawn = false;

        if (hasLink)
        {
            spawnX = managed.AuthoringSpawnX;
            spawnY = managed.AuthoringSpawnY;
            drawSpawn = TryWorldToGui(new Vector3(spawnX, spawnY, nowWorld.z), out spawnGui);
        }

        bool drawEdit = false;
        Vector2 editGui = default;
        if (editPreviewActive &&
            TryWorldToGui(new Vector3(editPreviewX, editPreviewY, nowWorld.z), out editGui))
        {
            float dx = editGui.x - spawnGui.x;
            float dy = editGui.y - spawnGui.y;
            if (!drawSpawn || dx * dx + dy * dy > 4f)
                drawEdit = true;
        }

        if (drawSpawn)
            DrawGuiLine(spawnGui, nowGui, new Color(0.35f, 0.85f, 1f, 0.75f), 2f);
        if (drawEdit)
            DrawGuiLine(editGui, nowGui, new Color(1f, 0.7f, 0.2f, 0.8f), 2f);

        if (drawSpawn)
            DrawCross(spawnGui, 10f, new Color(0.25f, 0.8f, 1f, 0.95f), "SPAWN");
        DrawCross(nowGui, 10f, new Color(0.3f, 1f, 0.45f, 0.95f), "NOW");
        if (drawEdit)
            DrawCross(editGui, 10f, new Color(1f, 0.65f, 0.15f, 0.95f), "EDIT");

        // Floating coords above the selected enemy (updates live with arrow nudge).
        DrawCoordsAbove(nowGui, nowWorld.x, nowWorld.y);

        float panelW = 210f;
        float panelH = hasLink ? (drawEdit ? 92f : 74f) : 52f;
        float px = nowGui.x + 18f;
        float py = nowGui.y - panelH * 0.5f;
        if (px + panelW > Screen.width - 8f)
            px = nowGui.x - panelW - 18f;
        if (py < 8f)
            py = 8f;
        if (py + panelH > Screen.height - 8f)
            py = Screen.height - panelH - 8f;

        Rect panel = new Rect(px, py, panelW, panelH);
        GUI.Box(panel, GUIContent.none, boxStyle);
        GUILayout.BeginArea(new Rect(panel.x + 6f, panel.y + 4f, panel.width - 12f, panel.height - 8f));

        string key = hasLink && !string.IsNullOrEmpty(managed.AuthoringEnemyKey)
            ? managed.AuthoringEnemyKey
            : locked.name;
        GUILayout.Label(key, labelStyle);

        if (hasLink)
        {
            GUILayout.Label("SPAWN  " + Fmt(spawnX) + " , " + Fmt(spawnY), labelStyle);
            GUILayout.Label("NOW    " + Fmt(nowWorld.x) + " , " + Fmt(nowWorld.y), labelStyle);
            float ddx = nowWorld.x - spawnX;
            float ddy = nowWorld.y - spawnY;
            GUILayout.Label("Δ      " + Signed(ddx) + " , " + Signed(ddy), labelStyle);
            if (drawEdit)
                GUILayout.Label("EDIT   " + Fmt(editPreviewX) + " , " + Fmt(editPreviewY), labelStyle);
        }
        else
        {
            GUILayout.Label("NOW    " + Fmt(nowWorld.x) + " , " + Fmt(nowWorld.y), labelStyle);
            GUILayout.Label(SpawnAuthoringLoc.T("status.noPackLink"), labelStyle);
        }

        GUILayout.EndArea();
    }

    private static void EnsureStyles()
    {
        if (labelStyle != null)
            return;

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            richText = false,
            wordWrap = false,
            clipping = TextClipping.Clip
        };
        labelStyle.normal.textColor = Color.white;

        boxStyle = new GUIStyle(GUI.skin.box);
    }

    private static string Fmt(float v)
    {
        return v.ToString("F2", CultureInfo.InvariantCulture);
    }

    private static string Signed(float v)
    {
        string s = v.ToString("F2", CultureInfo.InvariantCulture);
        if (v > 0.005f)
            return "+" + s;
        return s;
    }

    private static void DrawCoordsAbove(Vector2 gui, float worldX, float worldY)
    {
        string text = Fmt(worldX) + " , " + Fmt(worldY);
        Vector2 size = labelStyle.CalcSize(new GUIContent(text));
        float padX = 6f;
        float padY = 3f;
        float w = size.x + padX * 2f;
        float h = size.y + padY * 2f;
        float px = gui.x - w * 0.5f;
        float py = gui.y - 28f - h;
        if (px < 4f)
            px = 4f;
        if (px + w > Screen.width - 4f)
            px = Screen.width - w - 4f;
        if (py < 4f)
            py = gui.y + 16f;

        Rect r = new Rect(px, py, w, h);
        Color prev = GUI.color;
        GUI.color = new Color(0.06f, 0.07f, 0.08f, 0.72f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = new Color(0.3f, 1f, 0.45f, 0.95f);
        GUI.DrawTexture(new Rect(r.x, r.yMax - 2f, r.width, 2f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(r.x + padX, r.y + padY, size.x, size.y), text, labelStyle);
        GUI.color = prev;
    }

    private static void DrawCross(Vector2 gui, float half, Color color, string tag)
    {
        Color prev = GUI.color;
        GUI.color = color;
        const float t = 2f;
        GUI.DrawTexture(new Rect(gui.x - half, gui.y - t * 0.5f, half * 2f, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(gui.x - t * 0.5f, gui.y - half, t, half * 2f), Texture2D.whiteTexture);
        GUI.Label(new Rect(gui.x + half + 4f, gui.y - 10f, 64f, 18f), tag);
        GUI.color = prev;
    }

    private static void DrawGuiLine(Vector2 a, Vector2 b, Color color, float thickness)
    {
        Vector2 d = b - a;
        float len = d.magnitude;
        if (len < 1f)
            return;

        float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        Color prev = GUI.color;
        Matrix4x4 prevM = GUI.matrix;
        GUI.color = color;
        GUIUtility.RotateAroundPivot(angle, a);
        GUI.DrawTexture(new Rect(a.x, a.y - thickness * 0.5f, len, thickness), Texture2D.whiteTexture);
        GUI.matrix = prevM;
        GUI.color = prev;
    }

    private static bool TryWorldToGui(Vector3 world, out Vector2 gui)
    {
        gui = default;
        global::UnityEngine.Camera cam = GetCam();
        if (cam == null)
            return false;

        Vector3 sp = cam.WorldToScreenPoint(world);
        if (sp.z < 0f)
            return false;

        gui = new Vector2(sp.x, Screen.height - sp.y);
        return true;
    }

    private static global::UnityEngine.Camera GetCam()
    {
        GameObject camGo = UnifiedCameraCacheManager.GetMainCamera();
        global::UnityEngine.Camera cam = camGo != null ? camGo.GetComponent<global::UnityEngine.Camera>() : null;
        if (cam == null)
            cam = global::UnityEngine.Camera.main;
        return cam;
    }
}
