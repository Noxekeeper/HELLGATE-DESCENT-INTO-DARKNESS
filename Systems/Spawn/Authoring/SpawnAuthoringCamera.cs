using System;
using System.Reflection;
using Com.LuisPedroFonseca.ProCamera2D;
using NoREroMod.Systems.Cache;
using UnityEngine;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// F11 authoring overview camera: scroll zoom while paused.
/// Uses the same ProCamera2D half-size pipeline as <see cref="NoREroMod.Systems.Camera.CombatCameraPresetSystem"/>.
/// Zoom-out has no practical ceiling (soft safety only); zoom-in stops at <see cref="MinMultiplier"/>.
/// </summary>
internal static class SpawnAuthoringCamera
{
    private const float MinMultiplier = 0.55f;
    /// <summary>Soft ceiling only — prevents ProCamera / float blow-ups; far beyond any map.</summary>
    private const float SoftMaxMultiplier = 500f;
    private const float ScrollStep = 0.12f;
    /// <summary>World units/sec at half-size ≈ 1; scales with current zoom.</summary>
    private const float PanSpeedPerHalfSize = 3.2f;

    private static bool panelOpen;
    private static bool overviewArmed;
    private static float defaultHalfSize;
    private static float currentHalfSize;
    private static bool hasDefault;
    private static bool mmbPanning;
    private static Vector3 mmbLastMouse;

    private static ProCamera2D proCamera;
    private static ProCamera2DZoomToFitTargets fitZoom;
    private static ProCamera2DSpeedBasedZoom speedZoom;

    private static FieldInfo startScreenSizeField;
    private static FieldInfo fitzInitial;
    private static FieldInfo fitzTarget;
    private static FieldInfo fitzSmoothed;
    private static FieldInfo spdInitial;
    private static FieldInfo spdPrevious;
    private static bool reflectionReady;

    internal static bool PanelOpen
    {
        get => panelOpen;
        set => panelOpen = value;
    }

    /// <summary>Scroll zoom is live (F11 overview mode).</summary>
    internal static bool IsOverviewArmed => overviewArmed;

    internal static float CurrentMultiplier
    {
        get
        {
            if (!hasDefault || defaultHalfSize <= 0.0001f)
                return 1f;
            return currentHalfSize / defaultHalfSize;
        }
    }

    /// <summary>Effective zoom-out ceiling (soft safety). UI may show ∞.</summary>
    internal static float MaxMultiplier => SoftMaxMultiplier;

    internal static string StatusLabel()
    {
        if (!overviewArmed)
            return "WASD / MMB-drag — move camera · wheel zooms · V toggles Overview";
        return "WASD / MMB-drag — move camera · Zoom ×" + CurrentMultiplier.ToString("0.00") +
               "  (min " + MinMultiplier.ToString("0.00") + " / max ∞)";
    }

    internal static void EndSession()
    {
        if (overviewArmed)
            ResetToDefault(restoreCombatStandard: true);
        overviewArmed = false;
        panelOpen = false;
        mmbPanning = false;
        hasDefault = false;
        currentHalfSize = 0f;
        defaultHalfSize = 0f;
        proCamera = null;
        fitZoom = null;
        speedZoom = null;
    }

    internal static bool TryArmOverview(out string status)
    {
        if (!EnsureCamera())
        {
            status = SpawnAuthoringLoc.T("status.cameraMissing");
            return false;
        }

        CaptureDefaultIfNeeded();
        if (!hasDefault || defaultHalfSize <= 0f)
        {
            status = SpawnAuthoringLoc.T("status.cameraSizeFail");
            return false;
        }

        overviewArmed = true;
        currentHalfSize = ReadCurrentHalfSizeOrDefault();
        ApplyHalfSizeImmediate(currentHalfSize);
        status = SpawnAuthoringLoc.T("status.overviewOn");
        return true;
    }

    internal static void DisarmOverview(bool restoreDefault)
    {
        if (restoreDefault && overviewArmed)
            ResetToDefault(restoreCombatStandard: false);
        overviewArmed = false;
    }

    internal static void ToggleOverview(out string status)
    {
        if (overviewArmed)
        {
            DisarmOverview(restoreDefault: true);
            status = SpawnAuthoringLoc.T("status.viewModDefault");
            return;
        }

        TryArmOverview(out status);
    }

    /// <summary>
    /// Home: pan to the player. If Overview is on, also reset zoom to Default
    /// without leaving Overview.
    /// </summary>
    internal static bool TryRecenterOnPlayer(out string status)
    {
        if (!EnsureCamera())
        {
            status = SpawnAuthoringLoc.T("status.cameraHomeFail");
            return false;
        }

        CaptureDefaultIfNeeded();
        GameObject player = UnifiedPlayerCacheManager.GetPlayerObject();
        if (player == null)
        {
            status = SpawnAuthoringLoc.T("status.cameraHomeFail");
            return false;
        }

        Vector3 p = player.transform.position;
        try
        {
            proCamera.MoveCameraInstantlyToPosition(new Vector2(p.x, p.y));
        }
        catch
        {
            Transform t = proCamera.transform;
            t.position = new Vector3(p.x, p.y, t.position.z);
        }

        if (overviewArmed && hasDefault && defaultHalfSize > 0f)
        {
            currentHalfSize = defaultHalfSize;
            ApplyHalfSizeImmediate(currentHalfSize);
        }

        status = SpawnAuthoringLoc.T("status.cameraHome");
        return true;
    }

    internal static void ResetToDefault(bool restoreCombatStandard)
    {
        if (!EnsureCamera())
            return;

        CaptureDefaultIfNeeded();
        if (!hasDefault)
            return;

        currentHalfSize = defaultHalfSize;
        ApplyHalfSizeImmediate(currentHalfSize);

        if (restoreCombatStandard)
        {
            try
            {
                NoREroMod.Systems.Camera.CombatCameraPresetSystem.ResetToStandard();
            }
            catch
            {
                // Combat camera optional during early boot.
            }
        }
    }

    /// <summary>
    /// F11 navigation: WASD or hold-MMB drag pan; scroll zoom when Overview is armed
    /// (or first scroll arms it). Uses unscaledDeltaTime because authoring pause sets timeScale=0.
    /// </summary>
    internal static void Tick(bool pointerOverUi)
    {
        if (!EnsureCamera())
            return;

        CaptureDefaultIfNeeded();
        if (!hasDefault)
            return;

        if (currentHalfSize <= 0f)
            currentHalfSize = ReadCurrentHalfSizeOrDefault();

        SyncBaselines(currentHalfSize);

        TickMiddleMousePan();
        if (!pointerOverUi)
            TickWasdPan();

        if (!pointerOverUi && !mmbPanning)
            TickScrollZoom();
    }

    internal static bool IsMiddleMousePanning => mmbPanning;

    private static void TickWasdPan()
    {
        // Don't steal keys from Ctrl+S / shortcuts.
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
            Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
            return;

        float h = 0f;
        float v = 0f;
        if (Input.GetKey(KeyCode.A)) h -= 1f;
        if (Input.GetKey(KeyCode.D)) h += 1f;
        if (Input.GetKey(KeyCode.S)) v -= 1f;
        if (Input.GetKey(KeyCode.W)) v += 1f;
        if (Mathf.Abs(h) < 0.01f && Mathf.Abs(v) < 0.01f)
            return;

        float half = currentHalfSize > 0.05f ? currentHalfSize : defaultHalfSize;
        float speed = PanSpeedPerHalfSize * half;
        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f)
            dt = 0.016f;

        ApplyPanWorld(h * speed * dt, v * speed * dt);
    }

    /// <summary>Hold middle mouse and drag — same camera move as WASD.</summary>
    private static void TickMiddleMousePan()
    {
        if (Input.GetMouseButtonDown(2))
        {
            mmbPanning = true;
            mmbLastMouse = Input.mousePosition;
        }

        if (!Input.GetMouseButton(2))
        {
            mmbPanning = false;
            return;
        }

        if (!mmbPanning)
            return;

        Vector3 now = Input.mousePosition;
        Vector3 delta = now - mmbLastMouse;
        mmbLastMouse = now;
        if (Mathf.Abs(delta.x) < 0.01f && Mathf.Abs(delta.y) < 0.01f)
            return;

        float half = currentHalfSize > 0.05f ? currentHalfSize : defaultHalfSize;
        float worldPerPx = (2f * half) / Mathf.Max(1f, Screen.height);
        // Invert: drag the map (Unity Scene / Blender MMB pan).
        ApplyPanWorld(-delta.x * worldPerPx, -delta.y * worldPerPx);
    }

    private static void ApplyPanWorld(float worldX, float worldY)
    {
        if (proCamera == null)
            return;
        if (Mathf.Abs(worldX) < 0.0001f && Mathf.Abs(worldY) < 0.0001f)
            return;

        Vector3 pos = proCamera.LocalPosition;
        Vector2 next = new Vector2(pos.x + worldX, pos.y + worldY);
        try
        {
            proCamera.MoveCameraInstantlyToPosition(next);
        }
        catch
        {
            Transform t = proCamera.transform;
            t.position = new Vector3(next.x, next.y, t.position.z);
        }
    }

    private static void TickScrollZoom()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) < 0.01f)
            return;

        // First scroll auto-arms overview so Default reset has a baseline.
        if (!overviewArmed)
            overviewArmed = true;

        float mult = CurrentMultiplier;
        // Exponential scroll: farther out = bigger world steps, still smooth.
        float step = ScrollStep;
        if (mult > 4f)
            step = ScrollStep * Mathf.Lerp(1f, 1.8f, Mathf.Clamp01((mult - 4f) / 20f));
        mult *= 1f - scroll * step;
        mult = Mathf.Clamp(mult, MinMultiplier, SoftMaxMultiplier);
        currentHalfSize = defaultHalfSize * mult;
        ApplyHalfSizeImmediate(currentHalfSize);
    }

    private static void InitReflection()
    {
        if (reflectionReady)
            return;

        startScreenSizeField = typeof(ProCamera2D).GetField(
            "_startScreenSizeInWorldCoordinates", BindingFlags.NonPublic | BindingFlags.Instance);

        Type fitzType = typeof(ProCamera2DZoomToFitTargets);
        fitzInitial = fitzType.GetField("_initialCamSize", BindingFlags.NonPublic | BindingFlags.Instance);
        fitzTarget = fitzType.GetField("_targetCamSize", BindingFlags.NonPublic | BindingFlags.Instance);
        fitzSmoothed = fitzType.GetField("_targetCamSizeSmoothed", BindingFlags.NonPublic | BindingFlags.Instance);

        Type spdType = typeof(ProCamera2DSpeedBasedZoom);
        spdInitial = spdType.GetField("_initialCamSize", BindingFlags.NonPublic | BindingFlags.Instance);
        spdPrevious = spdType.GetField("_previousCamSize", BindingFlags.NonPublic | BindingFlags.Instance);

        reflectionReady = true;
    }

    private static bool EnsureCamera()
    {
        if (proCamera != null)
            return true;

        GameObject camGo = UnifiedCameraCacheManager.GetMainCamera();
        if (camGo == null)
            return false;

        proCamera = camGo.GetComponent<ProCamera2D>();
        fitZoom = camGo.GetComponent<ProCamera2DZoomToFitTargets>();
        speedZoom = camGo.GetComponent<ProCamera2DSpeedBasedZoom>();
        return proCamera != null;
    }

    private static void CaptureDefaultIfNeeded()
    {
        if (hasDefault || proCamera == null)
            return;

        InitReflection();
        float half = 0f;
        if (startScreenSizeField != null)
        {
            Vector2 startSize = (Vector2)startScreenSizeField.GetValue(proCamera);
            half = startSize.y * 0.5f;
        }

        if (half <= 0f)
            half = proCamera.ScreenSizeInWorldCoordinates.y * 0.5f;

        if (half > 0f)
        {
            defaultHalfSize = half;
            hasDefault = true;
            if (currentHalfSize <= 0f)
                currentHalfSize = half;
        }
    }

    private static float ReadCurrentHalfSizeOrDefault()
    {
        if (proCamera == null)
            return defaultHalfSize;
        float half = proCamera.ScreenSizeInWorldCoordinates.y * 0.5f;
        return half > 0f ? half : defaultHalfSize;
    }

    private static void ApplyHalfSizeImmediate(float halfSize)
    {
        if (proCamera == null || halfSize <= 0f)
            return;

        SyncBaselines(halfSize);
        // Duration 0 — timeScale is 0 during F11 pause, so animated tweens would never finish.
        proCamera.UpdateScreenSize(halfSize, 0f, EaseType.EaseInOut);
    }

    private static void SyncBaselines(float halfSize)
    {
        InitReflection();
        try
        {
            if (fitZoom != null)
            {
                fitzInitial?.SetValue(fitZoom, halfSize);
                fitzTarget?.SetValue(fitZoom, halfSize);
                fitzSmoothed?.SetValue(fitZoom, halfSize);
            }

            if (speedZoom != null)
            {
                spdInitial?.SetValue(speedZoom, halfSize);
                spdPrevious?.SetValue(speedZoom, halfSize * 2f);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[SPAWN AUTHORING] Camera sync failed: " + ex.Message);
        }
    }
}
