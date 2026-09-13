using System;
using UnityEngine;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// Arrow-key nudge for F11: selected (locked) enemy, or Last LMB spawn point when nothing selected.
/// Step = 0.10 world units. Shift+arrows = ×5.
/// </summary>
internal static class SpawnAuthoringNudge
{
    internal const float Step = 0.10f;

    private const float HoldDelay = 0.32f;
    private const float HoldRepeat = 0.045f;

    private static float holdArmedAt = -1f;
    private static float nextHoldAt;
    private static KeyCode holdKey = KeyCode.None;

    /// <summary>Fired after a successful nudge with the new world X/Y (for Edit panel sync).</summary>
    internal static Action<float, float> OnPositionNudged;

    internal static void Clear()
    {
        holdArmedAt = -1f;
        nextHoldAt = 0f;
        holdKey = KeyCode.None;
    }

    internal static void Tick()
    {
        // Let IMGUI TextFields keep Left/Right for the caret.
        if (GUIUtility.keyboardControl != 0)
            return;

        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
            Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
            return;

        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        float step = shift ? Step * 5f : Step;

        float dx = 0f;
        float dy = 0f;
        if (ConsumeAxis(KeyCode.LeftArrow, -step, 0f, ref dx, ref dy)) { }
        else if (ConsumeAxis(KeyCode.RightArrow, step, 0f, ref dx, ref dy)) { }
        else if (ConsumeAxis(KeyCode.UpArrow, 0f, step, ref dx, ref dy)) { }
        else if (ConsumeAxis(KeyCode.DownArrow, 0f, -step, ref dx, ref dy)) { }
        else
        {
            holdKey = KeyCode.None;
            holdArmedAt = -1f;
            return;
        }

        if (Mathf.Abs(dx) < 0.0001f && Mathf.Abs(dy) < 0.0001f)
            return;

        GameObject locked = SpawnAuthoringWorldPick.Locked;
        if (locked != null)
            NudgeEnemy(locked, dx, dy);
        else if (SpawnAuthoringState.HasLastClick)
            NudgeLastClick(dx, dy);
    }

    private static bool ConsumeAxis(KeyCode key, float stepX, float stepY, ref float dx, ref float dy)
    {
        if (!Input.GetKey(key))
            return false;

        bool fire = false;
        if (Input.GetKeyDown(key))
        {
            fire = true;
            holdKey = key;
            holdArmedAt = Time.unscaledTime + HoldDelay;
            nextHoldAt = holdArmedAt;
        }
        else if (holdKey == key && holdArmedAt > 0f && Time.unscaledTime >= nextHoldAt)
        {
            fire = true;
            nextHoldAt = Time.unscaledTime + HoldRepeat;
        }

        if (!fire)
            return true; // key held, but not a step this frame

        dx = stepX;
        dy = stepY;
        return true;
    }

    private static void NudgeEnemy(GameObject go, float dx, float dy)
    {
        if (go == null)
            return;

        Vector3 p = go.transform.position;
        float nx = Snap(p.x + dx);
        float ny = Snap(p.y + dy);
        go.transform.position = new Vector3(nx, ny, p.z);
        MarkMovedInEditor(go);

        SpawnAuthoringState.SetLastClickSilent(nx, ny, SpawnAuthoringState.LastClickPackHint);
        OnPositionNudged?.Invoke(nx, ny);
    }

    /// <summary>Absolute world placement (mouse drag / tools). Snap + sync pack link + Last LMB.</summary>
    internal static void SetEnemyWorldPos(GameObject go, float x, float y)
    {
        if (go == null)
            return;

        float nx = Snap(x);
        float ny = Snap(y);
        Vector3 p = go.transform.position;
        go.transform.position = new Vector3(nx, ny, p.z);
        MarkMovedInEditor(go);

        SpawnAuthoringState.SetLastClickSilent(nx, ny, SpawnAuthoringState.LastClickPackHint);
        OnPositionNudged?.Invoke(nx, ny);
    }

    private static void MarkMovedInEditor(GameObject go)
    {
        SpawnManagedInstance managed = go.GetComponent<SpawnManagedInstance>()
            ?? go.GetComponentInChildren<SpawnManagedInstance>(true);
        if (managed != null)
            managed.MarkMovedInEditor();
    }

    private static void NudgeLastClick(float dx, float dy)
    {
        float nx = Snap(SpawnAuthoringState.LastClickX + dx);
        float ny = Snap(SpawnAuthoringState.LastClickY + dy);
        SpawnAuthoringState.SetLastClickSilent(nx, ny, SpawnAuthoringState.LastClickPackHint);
        OnPositionNudged?.Invoke(nx, ny);
    }

    private static float Snap(float v)
    {
        // Avoid 0.30000001 drift from repeated 0.10 adds.
        return Mathf.Round(v * 100f) / 100f;
    }
}
