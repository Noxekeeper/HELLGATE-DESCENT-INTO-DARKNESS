using System.Collections.Generic;
using System.Globalization;
using NoREroMod.Systems.Cache;
using NoREroMod.Systems.Spawn.Gimmicks;
using UnityEngine;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// F11 gizmo: action radius of every standalone HellGate lever with a mark on each gate/ladder it drives,
/// the guard zone of guarded gimmicks (red = enemies inside, green = clear) with the enemies it sees,
/// and the player range of auto gimmicks (cyan lines).
/// The selected object is drawn brighter.
/// </summary>
internal static class SpawnAuthoringGimmickRadius
{
    private const int Segments = 48;
    private static readonly Vector2[] ring = new Vector2[Segments + 1];
    private static GUIStyle labelStyle;

    internal static void DrawGui()
    {
        if (!global::NoREroMod.SpawnPointAnalyzer.IsRecordingModeActive)
            return;

        List<HellGateGimmickLever> levers = HellGateGimmickLever.ActiveLevers;
        List<HellGateGimmickController> controllers = HellGateGimmickController.ActiveControllers;
        if (levers.Count == 0 && controllers.Count == 0)
            return;

        global::UnityEngine.Camera cam = GetCam();
        if (cam == null)
            return;

        EnsureStyle();
        GameObject locked = SpawnAuthoringWorldPick.Locked;
        for (int i = controllers.Count - 1; i >= 0; i--)
        {
            HellGateGimmickController controller = controllers[i];
            HellGateGimmickGuard guard = controller != null ? controller.Guard : null;
            if (guard == null)
                continue;

            bool selected = locked != null && controller.transform.IsChildOf(locked.transform);
            DrawGuard(cam, guard, selected);
        }

        for (int i = levers.Count - 1; i >= 0; i--)
        {
            HellGateGimmickLever lever = levers[i];
            if (lever == null || !lever.IsInteractive || lever.HasOwner)
                continue;

            bool selected = locked != null && lever.transform.IsChildOf(locked.transform);
            DrawLever(cam, lever, selected);
        }
    }

    private static void DrawGuard(global::UnityEngine.Camera cam, HellGateGimmickGuard guard, bool selected)
    {
        Rect zone = guard.GetZone();
        Color color = guard.HasEnemies
            ? new Color(1f, 0.3f, 0.25f, selected ? 0.95f : 0.6f)
            : new Color(0.35f, 1f, 0.45f, selected ? 0.95f : 0.5f);
        float thickness = selected ? 2f : 1.5f;

        Vector2 tl = ToGui(cam, new Vector3(zone.xMin, zone.yMax, 0f));
        if (guard.HasGuardZone)
        {
            Vector2 bl = ToGui(cam, new Vector3(zone.xMin, zone.yMin, 0f));
            Vector2 br = ToGui(cam, new Vector3(zone.xMax, zone.yMin, 0f));
            Vector2 tr = ToGui(cam, new Vector3(zone.xMax, zone.yMax, 0f));
            DrawGuiLine(bl, br, color, thickness);
            DrawGuiLine(br, tr, color, thickness);
            DrawGuiLine(tr, tl, color, thickness);
            DrawGuiLine(tl, bl, color, thickness);
        }

        if (guard.IsAuto)
            DrawAutoRange(cam, guard, selected);

        List<EnemyDate> enemies = guard.EnemiesInside;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyDate enemy = enemies[i];
            if (enemy == null)
                continue;
            Vector2 e = ToGui(cam, enemy.transform.position);
            DrawGuiLine(new Vector2(e.x - 7f, e.y - 7f), new Vector2(e.x + 7f, e.y + 7f), color, 2f);
            DrawGuiLine(new Vector2(e.x - 7f, e.y + 7f), new Vector2(e.x + 7f, e.y - 7f), color, 2f);
        }

        Vector2 anchor = ToGui(cam, guard.transform.position);
        DrawGuiLine(new Vector2(anchor.x - 8f, anchor.y), new Vector2(anchor.x + 8f, anchor.y), color, 2f);
        DrawGuiLine(new Vector2(anchor.x, anchor.y - 8f), new Vector2(anchor.x, anchor.y + 8f), color, 2f);

        string text = BuildGuardLabel(guard, enemies.Count);
        Vector2 size = labelStyle.CalcSize(new GUIContent(text));
        Color prev = GUI.color;
        GUI.color = color;
        GUI.Label(new Rect(tl.x + 4f, tl.y - size.y - 2f, size.x, size.y), text, labelStyle);
        GUI.color = prev;
    }

    private static string BuildGuardLabel(HellGateGimmickGuard guard, int enemyCount)
    {
        string prefix = guard.HasGuardZone && guard.IsAuto ? "GUARD+AUTO" : guard.HasGuardZone ? "GUARD" : "AUTO";
        if (guard.HasEnemies)
            return prefix + " " + enemyCount + " — closed";
        if (guard.IsAuto && !guard.PlayerNear)
            return prefix + " — closed (no player)";
        return prefix + " — open";
    }

    /// <summary>Two vertical lines at ±auto range around the spawn point, over the player-detection height.</summary>
    private static void DrawAutoRange(global::UnityEngine.Camera cam, HellGateGimmickGuard guard, bool selected)
    {
        Vector3 p = guard.transform.position;
        float half = guard.Height * 0.5f;
        float r = guard.AutoRange;
        Color color = guard.PlayerNear
            ? new Color(0.4f, 0.95f, 1f, selected ? 0.95f : 0.7f)
            : new Color(0.4f, 0.85f, 1f, selected ? 0.8f : 0.45f);
        float thickness = selected ? 2f : 1.5f;

        Vector2 lt = ToGui(cam, new Vector3(p.x - r, p.y + half, 0f));
        Vector2 lb = ToGui(cam, new Vector3(p.x - r, p.y - half, 0f));
        Vector2 rt = ToGui(cam, new Vector3(p.x + r, p.y + half, 0f));
        Vector2 rb = ToGui(cam, new Vector3(p.x + r, p.y - half, 0f));
        DrawGuiLine(lt, lb, color, thickness);
        DrawGuiLine(rt, rb, color, thickness);
    }

    private static Vector2 ToGui(global::UnityEngine.Camera cam, Vector3 world)
    {
        Vector3 sp = cam.WorldToScreenPoint(world);
        return new Vector2(sp.x, Screen.height - sp.y);
    }

    private static void DrawLever(global::UnityEngine.Camera cam, HellGateGimmickLever lever, bool selected)
    {
        Vector3 center = lever.WorldAnchor;
        float radius = lever.Radius;
        Color color = selected
            ? new Color(1f, 0.85f, 0.25f, 0.95f)
            : new Color(0.4f, 0.85f, 1f, 0.55f);

        for (int s = 0; s <= Segments; s++)
        {
            float a = s * Mathf.PI * 2f / Segments;
            Vector3 world = new Vector3(center.x + Mathf.Cos(a) * radius, center.y + Mathf.Sin(a) * radius, center.z);
            Vector3 sp = cam.WorldToScreenPoint(world);
            ring[s] = new Vector2(sp.x, Screen.height - sp.y);
        }

        for (int s = 0; s < Segments; s++)
            DrawGuiLine(ring[s], ring[s + 1], color, selected ? 2f : 1.5f);

        Vector3 csp = cam.WorldToScreenPoint(center);
        if (csp.z < 0f)
            return;
        Vector2 c = new Vector2(csp.x, Screen.height - csp.y);

        float radiusSqr = radius * radius;
        List<HellGateGimmickController> controllers = HellGateGimmickController.ActiveControllers;
        for (int i = 0; i < controllers.Count; i++)
        {
            HellGateGimmickController controller = controllers[i];
            if (controller == null || controller.HasOwnLever || controller.IsGuardDriven ||
                controller.DistanceSqrTo(center) > radiusSqr)
                continue;

            Vector3 tsp = cam.WorldToScreenPoint(controller.AnchorPosition);
            Vector2 t = new Vector2(tsp.x, Screen.height - tsp.y);
            DrawGuiLine(c, t, new Color(color.r, color.g, color.b, 0.35f), 1f);
            DrawGuiLine(new Vector2(t.x - 6f, t.y - 6f), new Vector2(t.x + 6f, t.y + 6f), color, 2f);
            DrawGuiLine(new Vector2(t.x - 6f, t.y + 6f), new Vector2(t.x + 6f, t.y - 6f), color, 2f);
        }

        if (!selected)
            return;

        string text = "R " + radius.ToString("0.#", CultureInfo.InvariantCulture);
        Vector2 size = labelStyle.CalcSize(new GUIContent(text));
        Vector2 top = ring[Segments / 4];
        Color prev = GUI.color;
        GUI.color = color;
        GUI.Label(new Rect(top.x - size.x * 0.5f, top.y - size.y - 2f, size.x, size.y), text, labelStyle);
        GUI.color = prev;
    }

    private static void DrawGuiLine(Vector2 a, Vector2 b, Color color, float thickness)
    {
        Vector2 d = b - a;
        float len = d.magnitude;
        if (len < 0.5f)
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

    private static void EnsureStyle()
    {
        if (labelStyle != null)
            return;
        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            wordWrap = false,
            clipping = TextClipping.Overflow
        };
        labelStyle.normal.textColor = Color.white;
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
