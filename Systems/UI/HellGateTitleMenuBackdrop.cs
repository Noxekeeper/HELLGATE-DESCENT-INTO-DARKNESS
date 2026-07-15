using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NoREroMod.Systems.UI;

/// <summary>
/// Suppresses vanilla Gametitle background layers while HellGate title-menu art is active.
/// </summary>
internal static class HellGateTitleMenuBackdrop
{
    private static bool _initialized;
    private static bool _applied;
    private static readonly List<GameObject> HiddenObjects = new();

    private static readonly string[] BackgroundNameHints =
    {
        "back", "bg", "background", "sky", "cloud", "fog", "rain", "snow", "weather",
        "particle", "effect", "title_art", "titleart", "mainlogo", "gametitle"
    };

    internal static void Initialize()
    {
        if (_initialized)
            return;

        SceneManager.sceneLoaded += OnSceneLoaded;
        _initialized = true;
    }

    internal static void Apply()
    {
        if (!IsGametitleActive())
            return;

        if (_applied)
            return;

        try
        {
            HideVanillaSceneBackgrounds();
            HideMenuCanvasBackgrounds();
            SetCamerasBlack();
            _applied = true;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[HellGate TitleMenu] Apply failed: {ex.Message}");
        }
    }

    internal static void Reset()
    {
        for (int i = 0; i < HiddenObjects.Count; i++)
        {
            GameObject go = HiddenObjects[i];
            if (go != null)
                go.SetActive(true);
        }

        HiddenObjects.Clear();
        _applied = false;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive)
            return;

        if (!string.Equals(scene.name, "Gametitle", StringComparison.OrdinalIgnoreCase))
        {
            Reset();
            return;
        }

        if (HellGateSplashScreen.IsTitleMenuBackdropActive)
            Apply();
    }

    private static bool IsGametitleActive()
    {
        return string.Equals(SceneManager.GetActiveScene().name, "Gametitle", StringComparison.OrdinalIgnoreCase);
    }

    private static void HideVanillaSceneBackgrounds()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            HideBackgroundRecursive(roots[i].transform);
    }

    private static void HideBackgroundRecursive(Transform node)
    {
        if (node == null)
            return;

        if (ShouldSkipNode(node))
        {
            for (int i = 0; i < node.childCount; i++)
                HideBackgroundRecursive(node.GetChild(i));
            return;
        }

        if (TryHideBackgroundNode(node))
            return;

        for (int i = 0; i < node.childCount; i++)
            HideBackgroundRecursive(node.GetChild(i));
    }

    private static bool ShouldSkipNode(Transform node)
    {
        string name = node.name;
        if (name.IndexOf("HELLGATE_SplashScreen", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        if (name.IndexOf("HellGateSplashRunner", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (node.GetComponentInParent<GameStart_menu>() != null)
            return true;

        return false;
    }

    private static bool TryHideBackgroundNode(Transform node)
    {
        string nameLower = node.name.ToLowerInvariant();

        ParticleSystem ps = node.GetComponent<ParticleSystem>();
        if (ps != null && ContainsAny(nameLower, "rain", "snow", "weather", "particle", "effect"))
            return Hide(node.gameObject);

        SpriteRenderer sr = node.GetComponent<SpriteRenderer>();
        if (sr != null && sr.enabled && IsLikelyWorldBackground(node, nameLower, sr))
            return Hide(node.gameObject);

        MeshRenderer mr = node.GetComponent<MeshRenderer>();
        if (mr != null && mr.enabled && ContainsAny(nameLower, "back", "bg", "background", "sky", "title"))
            return Hide(node.gameObject);

        Image image = node.GetComponent<Image>();
        if (image != null && image.enabled && node.GetComponent<Button>() == null)
        {
            RectTransform rt = node as RectTransform;
            if (rt != null && IsFullscreenRect(rt) && ContainsAny(nameLower, BackgroundNameHints))
                return Hide(node.gameObject);
        }

        return false;
    }

    private static bool IsLikelyWorldBackground(Transform node, string nameLower, SpriteRenderer sr)
    {
        if (ContainsAny(nameLower, BackgroundNameHints))
            return true;

        if (node.GetComponentInParent<Canvas>() != null)
            return false;

        Vector3 size = sr.bounds.size;
        return size.x > 8f && size.y > 4f;
    }

    private static void HideMenuCanvasBackgrounds()
    {
        GameStart_menu menu = UnityEngine.Object.FindObjectOfType<GameStart_menu>();
        if (menu == null)
            return;

        GameObject menuCanvas = null;
        try
        {
            var canvasField = AccessTools.Field(typeof(GameStart_menu), "canvas");
            menuCanvas = canvasField?.GetValue(menu) as GameObject;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[HellGate TitleMenu] Could not read GameStart_menu.canvas: {ex.Message}");
        }

        if (menuCanvas == null)
            return;

        HideFullscreenImages(menuCanvas.transform, menuCanvas.transform);
    }

    private static void HideFullscreenImages(Transform node, Transform menuRoot)
    {
        Image image = node.GetComponent<Image>();
        if (image != null && image.enabled && node.GetComponent<Button>() == null)
        {
            RectTransform rt = node as RectTransform;
            if (rt != null && IsFullscreenRect(rt) && node != menuRoot)
                Hide(node.gameObject);
        }

        for (int i = 0; i < node.childCount; i++)
        {
            Transform child = node.GetChild(i);
            if (child.name == "HGSettingsButton")
                continue;
            HideFullscreenImages(child, menuRoot);
        }
    }

    private static void SetCamerasBlack()
    {
        UnityEngine.Camera[] cameras = UnityEngine.Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            UnityEngine.Camera cam = cameras[i];
            if (cam == null)
                continue;

            cam.clearFlags = UnityEngine.CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
        }
    }

    private static bool Hide(GameObject go)
    {
        if (go == null || !go.activeSelf)
            return false;

        go.SetActive(false);
        HiddenObjects.Add(go);
        return true;
    }

    private static bool IsFullscreenRect(RectTransform rt)
    {
        Vector2 min = rt.anchorMin;
        Vector2 max = rt.anchorMax;
        return min.x <= 0.01f && min.y <= 0.01f && max.x >= 0.99f && max.y >= 0.99f;
    }

    private static bool ContainsAny(string value, params string[] hints)
    {
        for (int i = 0; i < hints.Length; i++)
        {
            if (value.IndexOf(hints[i], StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }
}
