using System.Collections.Generic;
using NoREroMod.Patches.HellTraps;
using NoREroMod.Systems.Cache;
using NoREroMod.Systems.Economy;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// Hover-pick live enemies / traps while F11 authoring is on.
/// Precise by default: sprite / solid collider only (no pickup-trigger halo).
/// Hold Alt for the old generous grab. Wheel cycles overlaps when not zooming.
/// </summary>
internal static class SpawnAuthoringWorldPick
{
    private const float BoundsPad = 0.04f;
    private const float SlackRadius = 0.45f;
    private const float AnchorRadius = 1.0f;
    private const float MaxSpriteArea = 24f;
    private const float MaxMeshArea = 8f;
    private const float SpriteTight = 0.92f;
    private const float MeshTight = 0.58f;

    private static readonly List<GameObject> Candidates = new List<GameObject>(16);
    private static readonly List<float> CandidateDistSq = new List<float>(16);
    private static int cycleIndex;
    private static GameObject hovered;
    private static GameObject locked;

    internal static GameObject Hovered => hovered;
    internal static GameObject Locked => locked;
    internal static int CandidateCount => Candidates.Count;
    internal static int CycleIndex1Based => Candidates.Count == 0 ? 0 : (cycleIndex % Candidates.Count) + 1;

    internal static void Clear()
    {
        Candidates.Clear();
        CandidateDistSq.Clear();
        cycleIndex = 0;
        hovered = null;
        locked = null;
        SpawnAuthoringOutline.Hide();
        SpawnAuthoringDrag.Clear();
    }

    internal static void Tick(bool pointerOverUi)
    {
        if (SpawnAuthoringDrag.IsDragging)
        {
            hovered = locked;
            SpawnAuthoringOutline.Show(locked, locked != null);
            return;
        }

        if (pointerOverUi || SpawnAuthoringCamera.IsMiddleMousePanning)
        {
            hovered = locked;
            SpawnAuthoringOutline.Show(hovered, locked != null && hovered == locked);
            return;
        }

        if (!TryGetMouseWorld(out Vector2 mouse))
        {
            hovered = locked;
            SpawnAuthoringOutline.Show(hovered, locked != null);
            return;
        }

        RebuildCandidates(mouse);

        float scroll = Input.mouseScrollDelta.y;
        // When overview camera is armed, scroll is reserved for zoom (not pick cycling).
        if (!SpawnAuthoringCamera.IsOverviewArmed &&
            Candidates.Count > 1 && Mathf.Abs(scroll) > 0.01f)
        {
            if (scroll > 0f)
                cycleIndex--;
            else
                cycleIndex++;
            while (cycleIndex < 0)
                cycleIndex += Candidates.Count;
            if (Candidates.Count > 0)
                cycleIndex %= Candidates.Count;
        }

        if (Candidates.Count == 0)
        {
            hovered = null;
        }
        else
        {
            if (cycleIndex >= Candidates.Count)
                cycleIndex = 0;
            hovered = Candidates[cycleIndex];
        }

        bool isLocked = locked != null && hovered == locked;
        SpawnAuthoringOutline.Show(hovered != null ? hovered : locked, locked != null && (hovered == null || isLocked));

        if (locked != null && locked != hovered && hovered == null)
            SpawnAuthoringOutline.Show(locked, true);
    }

    /// <summary>LMB on a hovered enemy locks selection (does not place a spawn point).</summary>
    internal static bool TryLockHoveredOnClick()
    {
        if (hovered == null)
            return false;
        locked = hovered;
        SpawnAuthoringOutline.Show(locked, true);
        return true;
    }

    internal static void ClearLock()
    {
        locked = null;
        if (hovered == null)
            SpawnAuthoringOutline.Hide();
    }

    /// <summary>Select a known instance (e.g. after Ctrl+V paste) without a mouse pick.</summary>
    internal static void ForceLock(GameObject go)
    {
        locked = go;
        hovered = go;
        if (go != null)
            SpawnAuthoringOutline.Show(go, true);
        else
            SpawnAuthoringOutline.Hide();
    }

    internal static string FormatHoverLabel()
    {
        GameObject focus = hovered != null ? hovered : locked;
        if (focus == null)
            return string.Empty;

        string name = focus.name ?? "?";
        if (SpawnAuthoringAnchorMarker.TryGet(focus, out SpawnAuthoringAnchorMarker marker))
            name = marker.FormatLabel();
        else
        {
            int clone = name.IndexOf("(Clone)", System.StringComparison.OrdinalIgnoreCase);
            if (clone >= 0)
                name = name.Substring(0, clone).Trim();
        }

        if (Candidates.Count > 1 && hovered != null)
            return name + "  (" + CycleIndex1Based + "/" + Candidates.Count + ")  wheel=cycle  LMB=select";

        if (locked != null && hovered == locked)
            return name + "  [selected]";

        return name + "  LMB=select";
    }

    private static void RebuildCandidates(Vector2 mouse)
    {
        Candidates.Clear();
        CandidateDistSq.Clear();

        // Combat enemies (tag).
        try
        {
            GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
            for (int i = 0; i < enemies.Length; i++)
                TryAddCandidate(enemies[i], mouse);
        }
        catch
        {
        }

        // HellGate-managed traps / templates (often no Enemy tag).
        SpawnManagedInstance[] managed = Object.FindObjectsOfType<SpawnManagedInstance>();
        for (int i = 0; i < managed.Length; i++)
        {
            if (managed[i] == null)
                continue;
            TryAddCandidate(managed[i].gameObject, mouse);
        }

        GoldPickup[] piles = Object.FindObjectsOfType<GoldPickup>();
        for (int i = 0; i < piles.Length; i++)
        {
            if (piles[i] == null)
                continue;
            TryAddCandidate(piles[i].gameObject, mouse);
        }

        // Keep cycle index stable relative to previous hovered when possible.
        if (hovered != null)
        {
            int idx = Candidates.IndexOf(hovered);
            if (idx >= 0)
                cycleIndex = idx;
        }
        else if (locked != null)
        {
            int idx = Candidates.IndexOf(locked);
            if (idx >= 0)
                cycleIndex = idx;
        }
        else
        {
            cycleIndex = 0;
        }
    }

    private static void TryAddCandidate(GameObject go, Vector2 mouse)
    {
        if (go == null || !go.activeInHierarchy)
            return;

        // Dedupe (Enemy tag + SpawnManagedInstance on same root).
        for (int i = 0; i < Candidates.Count; i++)
        {
            if (Candidates[i] == go)
                return;
        }

        float score;
        if (!TryScorePick(go, mouse, out score))
            return;

        int insert = Candidates.Count;
        for (int c = 0; c < CandidateDistSq.Count; c++)
        {
            if (score < CandidateDistSq[c])
            {
                insert = c;
                break;
            }
        }

        Candidates.Insert(insert, go);
        CandidateDistSq.Insert(insert, score);
    }

    /// <summary>
    /// Hit the visible body. Gold skips the pickup circle; trap/decor/hostage/object
    /// prefabs keep trigger colliders and large Spine meshes. Enemies stay tight.
    /// Smaller containing shape wins. Alt restores a nearby-radius grab.
    /// </summary>
    private static bool TryScorePick(GameObject go, Vector2 mouse, out float score)
    {
        score = 0f;
        float bestContainArea = float.MaxValue;
        bool contained = false;
        float nearestDistSq = float.MaxValue;
        bool isGold = go.GetComponent<GoldPickup>() != null;
        bool isProp = !isGold && IsAuthoringPropPick(go);
        bool isAnchor = go.GetComponent<SpawnAuthoringAnchorMarker>() != null;
        bool slack = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

        Collider2D[] cols = go.GetComponentsInChildren<Collider2D>(false);
        for (int i = 0; i < cols.Length; i++)
        {
            Collider2D col = cols[i];
            if (col == null || !col.enabled)
                continue;
            if (col.isTrigger && !isAnchor && !slack)
            {
                // Gold pickup circle is several times the sprite. Prop bodies
                // (Ivy, Rosewarm, decor, hostage…) are triggers on purpose.
                if (isGold && col is CircleCollider2D)
                    continue;
                if (!isProp && !isGold)
                    continue;
            }

            Bounds b = col.bounds;
            float area = Mathf.Max(0.01f, b.size.x * b.size.y);
            if (col.OverlapPoint(mouse))
            {
                contained = true;
                if (area < bestContainArea)
                    bestContainArea = area;
            }

            float d2 = DistSq(b.center, mouse);
            if (d2 < nearestDistSq)
                nearestDistSq = d2;
        }

        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(false);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy)
                continue;
            if (r is ParticleSystemRenderer)
                continue;

            Bounds b = r.bounds;
            float area = Mathf.Max(0.01f, b.size.x * b.size.y);
            bool sprite = r is SpriteRenderer;
            float tight = sprite ? SpriteTight : MeshTight;
            if (sprite)
            {
                if (area > MaxSpriteArea)
                    continue;
            }
            else if (area > MaxMeshArea)
            {
                if (!isProp)
                    continue;
                tight = 0.50f;
            }
            if (TightBoundsContains(b, mouse, tight, BoundsPad))
            {
                contained = true;
                if (area < bestContainArea)
                    bestContainArea = area;
            }

            float d2 = DistSq(b.center, mouse);
            if (d2 < nearestDistSq)
                nearestDistSq = d2;
        }

        if (contained)
        {
            score = bestContainArea;
            return true;
        }

        if (isAnchor)
        {
            float dx = go.transform.position.x - mouse.x;
            float dy = go.transform.position.y - mouse.y;
            float d2 = dx * dx + dy * dy;
            if (d2 > AnchorRadius * AnchorRadius)
                return false;
            score = 50f + d2;
            return true;
        }

        if (!slack)
            return false;

        float slackR = SlackRadius;
        if (nearestDistSq > slackR * slackR)
        {
            float dx = go.transform.position.x - mouse.x;
            float dy = go.transform.position.y - mouse.y;
            nearestDistSq = dx * dx + dy * dy;
            if (nearestDistSq > slackR * slackR)
                return false;
        }

        score = 10000f + nearestDistSq;
        return true;
    }

    /// <summary>
    /// Scene props whose "body" is a trigger and/or a large Spine mesh — same pick
    /// as Ivy. Combat enemies (SPAWN/TEMPLATE) stay on solid collider + small sprite.
    /// </summary>
    private static bool IsAuthoringPropPick(GameObject go)
    {
        if (go.GetComponent<Trapdata>() != null ||
            go.GetComponentInChildren<Trapdata>(true) != null)
            return true;

        if (go.GetComponentInChildren<HellGateLethalMagicTrapMarker>(true) != null ||
            go.GetComponentInChildren<HellGateLethalCocoonTrapMarker>(true) != null ||
            go.GetComponentInChildren<HellGateLethalLightningTrapMarker>(true) != null)
            return true;

        SpawnManagedInstance managed = go.GetComponent<SpawnManagedInstance>() ??
                                       go.GetComponentInChildren<SpawnManagedInstance>(true);
        if (managed != null &&
            SpawnAuthoringPackEdit.IsPropTemplateAuthoringLine(managed.AuthoringSourceLineRaw))
            return true;

        return false;
    }

    private static bool TightBoundsContains(Bounds b, Vector2 mouse, float tightness, float pad)
    {
        Vector3 c = b.center;
        float hx = Mathf.Max(0.02f, b.extents.x * tightness) + pad;
        float hy = Mathf.Max(0.02f, b.extents.y * tightness) + pad;
        return mouse.x >= c.x - hx && mouse.x <= c.x + hx &&
               mouse.y >= c.y - hy && mouse.y <= c.y + hy;
    }

    private static float DistSq(Vector3 center, Vector2 mouse)
    {
        float dx = center.x - mouse.x;
        float dy = center.y - mouse.y;
        return dx * dx + dy * dy;
    }

    internal static bool TryGetMouseWorld(out Vector2 mouse)
    {
        mouse = default;
        GameObject camGo = UnifiedCameraCacheManager.GetMainCamera();
        global::UnityEngine.Camera cam = camGo != null ? camGo.GetComponent<global::UnityEngine.Camera>() : null;
        if (cam == null)
            cam = global::UnityEngine.Camera.main;
        if (cam == null)
            return false;

        float depthZ = 0f;
        GameObject player = UnifiedPlayerCacheManager.GetPlayerObject();
        if (player != null)
            depthZ = player.transform.position.z;

        Vector3 screen = Input.mousePosition;
        screen.z = cam.WorldToScreenPoint(new Vector3(0f, 0f, depthZ)).z;
        Vector3 world = cam.ScreenToWorldPoint(screen);
        mouse = new Vector2(world.x, world.y);
        return true;
    }
}
