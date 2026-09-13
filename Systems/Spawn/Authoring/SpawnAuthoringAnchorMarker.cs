using NoREroMod.Systems.Cache;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// F11-only handle for EVENTTRAP / REINFORCEMENT pack lines. Invisible in play;
/// in the authoring overlay it is a labeled gizmo you can pick, drag, and save.
/// </summary>
internal sealed class SpawnAuthoringAnchorMarker : MonoBehaviour
{
    private const float HitRadius = 0.95f;

    internal string Command = "EVENTTRAP";
    internal string PackFolder = string.Empty;

    private CircleCollider2D hit;
    private static GUIStyle labelStyle;

    internal static GameObject Spawn(
        string command,
        string packFolder,
        Vector2 pos,
        string packPath,
        int lineIndex,
        string sourceLine)
    {
        string cmd = string.IsNullOrEmpty(command) ? "EVENTTRAP" : command.Trim().ToUpperInvariant();
        string folder = packFolder != null ? packFolder.Trim() : "anchor";

        GameObject go = new GameObject("HG_Anchor_" + cmd + "_" + folder);
        go.transform.position = new Vector3(pos.x, pos.y, 0f);

        SpawnAuthoringAnchorMarker marker = go.AddComponent<SpawnAuthoringAnchorMarker>();
        marker.Command = cmd;
        marker.PackFolder = folder;
        marker.EnsureHitCollider();

        SpawnConfigExecutor.ApplyAuthoringLink(
            go,
            packPath,
            lineIndex,
            sourceLine,
            pos.x,
            pos.y,
            folder,
            null,
            1f,
            1,
            false,
            false,
            0f,
            0,
            true);
        SpawnConfigExecutor.MoveSpawnedToGameplayScene(go);
        return go;
    }

    internal static bool TryGet(GameObject go, out SpawnAuthoringAnchorMarker marker)
    {
        marker = go != null ? go.GetComponent<SpawnAuthoringAnchorMarker>() : null;
        return marker != null;
    }

    internal static void DrawGui()
    {
        if (!global::NoREroMod.SpawnPointAnalyzer.IsRecordingModeActive)
            return;

        SpawnAuthoringAnchorMarker[] markers = Object.FindObjectsOfType<SpawnAuthoringAnchorMarker>();
        if (markers == null || markers.Length == 0)
            return;

        EnsureStyle();
        GameObject locked = SpawnAuthoringWorldPick.Locked;
        for (int i = 0; i < markers.Length; i++)
        {
            SpawnAuthoringAnchorMarker marker = markers[i];
            if (marker == null)
                continue;
            marker.DrawOne(locked == marker.gameObject);
        }
    }

    internal string FormatLabel()
    {
        string shortCmd = string.Equals(Command, "REINFORCEMENT", System.StringComparison.OrdinalIgnoreCase)
            ? "REINF"
            : "ETRAP";
        if (string.IsNullOrEmpty(PackFolder))
            return shortCmd;
        return shortCmd + "  " + PackFolder;
    }

    private void Awake()
    {
        EnsureHitCollider();
    }

    private void LateUpdate()
    {
        bool authoring = global::NoREroMod.SpawnPointAnalyzer.IsRecordingModeActive;
        if (hit != null && hit.enabled != authoring)
            hit.enabled = authoring;
    }

    private void EnsureHitCollider()
    {
        if (hit == null)
            hit = GetComponent<CircleCollider2D>();
        if (hit == null)
            hit = gameObject.AddComponent<CircleCollider2D>();
        hit.isTrigger = true;
        hit.radius = HitRadius;
    }

    private void DrawOne(bool selected)
    {
        if (!TryWorldToGui(transform.position, out Vector2 gui))
            return;

        Color prev = GUI.color;
        Color accent = selected
            ? new Color(1f, 0.85f, 0.25f, 0.95f)
            : new Color(1f, 0.45f, 0.15f, 0.92f);

        const float arm = 11f;
        const float t = 2f;
        GUI.color = accent;
        GUI.DrawTexture(new Rect(gui.x - arm, gui.y - t * 0.5f, arm * 2f, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(gui.x - t * 0.5f, gui.y - arm, t, arm * 2f), Texture2D.whiteTexture);

        float diamond = selected ? 16f : 12f;
        GUI.DrawTexture(new Rect(gui.x - diamond * 0.5f, gui.y - diamond * 0.5f, diamond, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(gui.x - diamond * 0.5f, gui.y + diamond * 0.5f - 2f, diamond, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(gui.x - diamond * 0.5f, gui.y - diamond * 0.5f, 2f, diamond), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(gui.x + diamond * 0.5f - 2f, gui.y - diamond * 0.5f, 2f, diamond), Texture2D.whiteTexture);

        string tag = FormatLabel();
        Vector2 sz = labelStyle.CalcSize(new GUIContent(tag));
        float padX = 6f;
        float padY = 3f;
        Rect label = new Rect(gui.x + arm + 6f, gui.y - sz.y * 0.5f - padY, sz.x + padX * 2f, sz.y + padY * 2f);
        GUI.color = new Color(0.06f, 0.05f, 0.04f, 0.82f);
        GUI.DrawTexture(label, Texture2D.whiteTexture);
        GUI.color = accent;
        GUI.DrawTexture(new Rect(label.x, label.yMax - 2f, label.width, 2f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(label.x + padX, label.y + padY, sz.x, sz.y), tag, labelStyle);
        GUI.color = prev;
    }

    private static void EnsureStyle()
    {
        if (labelStyle != null)
            return;
        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = false,
            clipping = TextClipping.Overflow
        };
        labelStyle.normal.textColor = Color.white;
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
