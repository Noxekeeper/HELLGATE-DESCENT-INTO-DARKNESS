using UnityEngine;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// Hold LMB on a selected/hovered enemy to drag it in the world (F11 authoring).
/// </summary>
internal static class SpawnAuthoringDrag
{
    private const float DragThresholdPx = 6f;

    private static bool holdArmed;
    private static bool dragging;
    private static Vector2 downScreen;
    private static Vector2 grabOffset;
    private static GameObject target;

    internal static bool IsDragging => dragging;
    internal static bool IsArmed => holdArmed || dragging;

    internal static void Clear()
    {
        holdArmed = false;
        dragging = false;
        target = null;
    }

    /// <summary>Call after locking an enemy on LMB down.</summary>
    internal static void BeginOnLocked()
    {
        GameObject go = SpawnAuthoringWorldPick.Locked;
        if (go == null)
            return;

        if (!SpawnAuthoringWorldPick.TryGetMouseWorld(out Vector2 mouse))
            return;

        target = go;
        holdArmed = true;
        dragging = false;
        downScreen = Input.mousePosition;
        Vector3 p = go.transform.position;
        grabOffset = new Vector2(mouse.x - p.x, mouse.y - p.y);
    }

    internal static void Tick(bool pointerOverUi)
    {
        if (SpawnAuthoringCamera.IsMiddleMousePanning)
            return;

        if (pointerOverUi && !dragging)
        {
            // Cancel arm if press started then moved onto UI without dragging yet.
            if (holdArmed && !Input.GetMouseButton(0))
                Clear();
            return;
        }

        if (holdArmed && Input.GetMouseButton(0) && target != null)
        {
            Vector2 screen = Input.mousePosition;
            if (!dragging)
            {
                float dx = screen.x - downScreen.x;
                float dy = screen.y - downScreen.y;
                if (dx * dx + dy * dy >= DragThresholdPx * DragThresholdPx)
                    dragging = true;
            }

            if (dragging && SpawnAuthoringWorldPick.TryGetMouseWorld(out Vector2 mouse))
            {
                float nx = Snap(mouse.x - grabOffset.x);
                float ny = Snap(mouse.y - grabOffset.y);
                SpawnAuthoringNudge.SetEnemyWorldPos(target, nx, ny);
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            bool didDrag = dragging;
            GameObject dragged = target;
            Clear();
            if (didDrag && dragged != null)
                SpawnAuthoringOverlayHost.TryCommitDraggedInstance(dragged);
        }
    }

    private static float Snap(float v)
    {
        return Mathf.Round(v * 100f) / 100f;
    }
}
