using System.Globalization;
using NoREroMod.Systems.Cache;
using UnityEngine;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// Minimal click feedback for F11 map LMB: floating label near cursor + world spawn-point marker.
/// </summary>
internal static class SpawnAuthoringClickCue
{
    private const float ToastSeconds = 1.35f;
    private static readonly Color BrandOrange = new Color(1f, 0.55f, 0.1f, 1f);

    private static float toastUntil;
    private static string toastText = string.Empty;
    private static Vector2 toastGui;
    private static bool toastAtCursor;

    private static GUIStyle toastStyle;

    internal static void NotifyClickRecorded(float worldX, float worldY, bool wasOverwrite)
    {
        toastText = wasOverwrite
            ? SpawnAuthoringLoc.T("click.updated")
            : SpawnAuthoringLoc.T("click.ready");
        toastText = toastText + "  " +
                    worldX.ToString("F2", CultureInfo.InvariantCulture) + "," +
                    worldY.ToString("F2", CultureInfo.InvariantCulture);
        toastUntil = Time.unscaledTime + ToastSeconds;
        toastAtCursor = true;
        toastGui = GuiMouse();
    }

    internal static void Clear()
    {
        toastUntil = 0f;
        toastText = string.Empty;
        toastAtCursor = false;
    }

    internal static void DrawGui()
    {
        DrawWorldMarker();
        DrawToast();
    }

    private static void DrawToast()
    {
        if (Time.unscaledTime >= toastUntil || string.IsNullOrEmpty(toastText))
            return;

        EnsureStyle();
        if (toastAtCursor)
            toastGui = GuiMouse();

        Vector2 size = toastStyle.CalcSize(new GUIContent(toastText));
        float padX = 10f;
        float padY = 5f;
        float w = size.x + padX * 2f;
        float h = size.y + padY * 2f;
        float px = toastGui.x + 16f;
        float py = toastGui.y + 18f;
        if (px + w > Screen.width - 6f)
            px = toastGui.x - w - 12f;
        if (py + h > Screen.height - 6f)
            py = toastGui.y - h - 10f;

        Rect r = new Rect(px, py, w, h);
        float life = Mathf.Clamp01((toastUntil - Time.unscaledTime) / ToastSeconds);
        float alpha = life > 0.25f ? 0.92f : life / 0.25f * 0.92f;

        Color prev = GUI.color;
        GUI.color = new Color(0.08f, 0.06f, 0.05f, alpha * 0.78f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = new Color(BrandOrange.r, BrandOrange.g, BrandOrange.b, alpha);
        GUI.DrawTexture(new Rect(r.x, r.yMax - 2f, r.width, 2f), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.Label(new Rect(r.x + padX, r.y + padY, size.x, size.y), toastText, toastStyle);
        GUI.color = prev;
    }

    private static void DrawWorldMarker()
    {
        if (!SpawnAuthoringState.HasLastClick)
            return;

        if (!TryWorldToGui(
                new Vector3(SpawnAuthoringState.LastClickX, SpawnAuthoringState.LastClickY, 0f),
                out Vector2 gui))
            return;

        Color prev = GUI.color;
        GUI.color = BrandOrange;
        const float arm = 9f;
        const float t = 2f;
        // Cross
        GUI.DrawTexture(new Rect(gui.x - arm, gui.y - t * 0.5f, arm * 2f, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(gui.x - t * 0.5f, gui.y - arm, t, arm * 2f), Texture2D.whiteTexture);
        // Diamond / ring corners
        const float d = 5f;
        GUI.DrawTexture(new Rect(gui.x - d, gui.y - d - 6f, 2f, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(gui.x + d, gui.y - d - 6f, 2f, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(gui.x - d, gui.y + d + 4f, 2f, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(gui.x + d, gui.y + d + 4f, 2f, 2f), Texture2D.whiteTexture);
        // Center dot
        GUI.DrawTexture(new Rect(gui.x - 2f, gui.y - 2f, 4f, 4f), Texture2D.whiteTexture);
        GUI.color = prev;

        EnsureStyle();
        string tag = SpawnAuthoringLoc.T("click.marker");
        Vector2 sz = toastStyle.CalcSize(new GUIContent(tag));
        GUI.Label(new Rect(gui.x + arm + 4f, gui.y - sz.y * 0.5f, sz.x + 4f, sz.y), tag, toastStyle);

        // Skip floating coords if a locked enemy already shows them at the same spot.
        if (SpawnAuthoringWorldPick.Locked == null)
            DrawCoordsAbove(gui, SpawnAuthoringState.LastClickX, SpawnAuthoringState.LastClickY);
    }

    private static void DrawCoordsAbove(Vector2 gui, float worldX, float worldY)
    {
        string text = worldX.ToString("F2", CultureInfo.InvariantCulture) + " , " +
                      worldY.ToString("F2", CultureInfo.InvariantCulture);
        Vector2 size = toastStyle.CalcSize(new GUIContent(text));
        float padX = 6f;
        float padY = 3f;
        float w = size.x + padX * 2f;
        float h = size.y + padY * 2f;
        float px = gui.x - w * 0.5f;
        float py = gui.y - 26f - h;
        if (px < 4f)
            px = 4f;
        if (px + w > Screen.width - 4f)
            px = Screen.width - w - 4f;
        if (py < 4f)
            py = gui.y + 16f;

        Rect r = new Rect(px, py, w, h);
        Color prev = GUI.color;
        GUI.color = new Color(0.08f, 0.06f, 0.05f, 0.72f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = new Color(BrandOrange.r, BrandOrange.g, BrandOrange.b, 0.95f);
        GUI.DrawTexture(new Rect(r.x, r.yMax - 2f, r.width, 2f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(r.x + padX, r.y + padY, size.x, size.y), text, toastStyle);
        GUI.color = prev;
    }

    private static void EnsureStyle()
    {
        if (toastStyle != null)
            return;
        toastStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = false,
            clipping = TextClipping.Overflow
        };
        toastStyle.normal.textColor = Color.white;
    }

    private static Vector2 GuiMouse()
    {
        if (Event.current != null)
            return Event.current.mousePosition;
        return new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
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
