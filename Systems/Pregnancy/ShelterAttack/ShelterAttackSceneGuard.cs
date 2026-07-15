using System;
using System.Collections.Generic;
using NoREroMod.Systems.Pregnancy.Patches;
using NoREroMod.Systems.Spawn;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NoREroMod.Systems.Pregnancy.ShelterAttack;

/// <summary>
/// Hides tutorial orbs, gallery triggers, altar tutorial colliders, the Parish savepoint, and the DPS training mannequin during shelter assault.
/// Full scene scan runs once per assault; only DPS canvases and tutorial popups are re-checked periodically.
/// </summary>
internal static class ShelterAttackSceneGuard
{
    private const float TransientRefreshSeconds = 3f;

    private static readonly string[] ExactHideNames =
    {
        "Vendetta",
        "col_spell",
        "col_contl1",
        "col_contl2"
    };

    private static readonly string[] NameFragmentsToHide =
    {
        "col_galary",
        "col_gal"
    };

    private static readonly Dictionary<GameObject, bool> _savedActiveStates = new Dictionary<GameObject, bool>();
    private static readonly List<Canvas> _cachedDpsCanvases = new List<Canvas>();

    private static bool _maskApplied;
    private static bool _loggedMaskActive;
    private static bool _fullMaskApplied;
    private static float _nextTransientRefreshAt;

    internal static bool IsCombatSpawnActive()
    {
        return ShelterAttackState.Phase == ShelterAttackPhase.Spawning
            || ShelterAttackState.Phase == ShelterAttackPhase.Combat;
    }

    internal static bool ShouldBlockParishTutorials()
    {
        if (!ShelterAttackState.IsAssaultPhase)
            return false;

        return HideoutSceneUtility.IsParishHideoutActive();
    }

    internal static bool ShouldBlockParishZoneRefresh(string sceneName)
    {
        if (!IsCombatSpawnActive())
            return false;

        if (HideoutSceneUtility.IsParishHideoutActive())
            return true;

        if (!string.IsNullOrEmpty(sceneName)
            && sceneName.IndexOf("Parish", StringComparison.OrdinalIgnoreCase) >= 0
            && sceneName.IndexOf("Church", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }

    internal static void ApplyAssaultMaskIfNeeded()
    {
        if (!ShouldBlockParishTutorials())
            return;

        if (_fullMaskApplied && !IsMaskTrackingStale())
        {
            MaybeRefreshTransientMask();
            return;
        }

        BeginFullMaskApply();
        ApplyFullAssaultMask();
        _fullMaskApplied = true;
        _nextTransientRefreshAt = Time.unscaledTime + TransientRefreshSeconds;
        LogMaskActivatedOnce();
    }

    internal static void RestoreAssaultMask()
    {
        if (!_maskApplied)
        {
            ClearMaskState();
            return;
        }

        foreach (KeyValuePair<GameObject, bool> kvp in _savedActiveStates)
        {
            if (kvp.Key != null)
                kvp.Key.SetActive(kvp.Value);
        }

        ClearMaskState();

        if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
            Plugin.Log?.LogInfo("[Pregnancy.ShelterAttack] Assault mask restored.");
    }

    internal static void Reset()
    {
        RestoreAssaultMask();
    }

    private static void BeginFullMaskApply()
    {
        _savedActiveStates.Clear();
        _cachedDpsCanvases.Clear();
        _maskApplied = true;
        _loggedMaskActive = false;
    }

    private static void ClearMaskState()
    {
        _savedActiveStates.Clear();
        _cachedDpsCanvases.Clear();
        _maskApplied = false;
        _loggedMaskActive = false;
        _fullMaskApplied = false;
        _nextTransientRefreshAt = 0f;
    }

    private static bool IsMaskTrackingStale()
    {
        if (!_maskApplied || _savedActiveStates.Count == 0)
            return true;

        foreach (KeyValuePair<GameObject, bool> kvp in _savedActiveStates)
        {
            if (kvp.Key != null)
                return false;
        }

        return true;
    }

    private static void MaybeRefreshTransientMask()
    {
        if (Time.unscaledTime < _nextTransientRefreshAt)
            return;

        _nextTransientRefreshAt = Time.unscaledTime + TransientRefreshSeconds;
        RefreshCachedDpsCanvases();
        CloseActiveTutorialWindowsInParish();
    }

    private static void ApplyFullAssaultMask()
    {
        for (int i = 0; i < ExactHideNames.Length; i++)
            TryHideByNameInLoadedScenes(ExactHideNames[i]);

        for (int i = 0; i < NameFragmentsToHide.Length; i++)
            TryHideNameContainsInLoadedScenes(NameFragmentsToHide[i]);

        HideTutorialTriggers();
        HideParishSavepoints();
        HideParishTrainingMannequins();
        HideParishDpsOverlays();
        CloseActiveTutorialWindowsInParish();
    }

    private static void RefreshCachedDpsCanvases()
    {
        for (int i = _cachedDpsCanvases.Count - 1; i >= 0; i--)
        {
            Canvas canvas = _cachedDpsCanvases[i];
            if (canvas == null)
            {
                _cachedDpsCanvases.RemoveAt(i);
                continue;
            }

            if (canvas.enabled)
                canvas.enabled = false;

            EnsureObjectHidden(canvas.gameObject);
        }
    }

    private static void LogMaskActivatedOnce()
    {
        if (PregnancyConfig.DebugLogging == null || !PregnancyConfig.DebugLogging.Value
            || _savedActiveStates.Count == 0 || _loggedMaskActive)
        {
            return;
        }

        _loggedMaskActive = true;
        Plugin.Log?.LogInfo($"[Pregnancy.ShelterAttack] Assault mask active ({_savedActiveStates.Count} object(s) hidden).");
    }

    private static void HideTutorialTriggers()
    {
        StartColObject[] triggers = Object.FindObjectsOfType<StartColObject>();
        for (int i = 0; i < triggers.Length; i++)
        {
            StartColObject trigger = triggers[i];
            if (trigger == null)
                continue;

            HideObject(trigger.gameObject);
        }
    }

    private static void HideParishSavepoints()
    {
        Savepoint_on[] savepoints = Object.FindObjectsOfType<Savepoint_on>();
        for (int i = 0; i < savepoints.Length; i++)
        {
            Savepoint_on savepoint = savepoints[i];
            if (savepoint == null)
                continue;

            Scene scene = savepoint.gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded || !IsParishChurchSceneName(scene.name))
                continue;

            HideObject(savepoint.gameObject);
        }
    }

    private static void HideParishTrainingMannequins()
    {
        DPScheckWood[] mannequins = Object.FindObjectsOfType<DPScheckWood>();
        for (int i = 0; i < mannequins.Length; i++)
        {
            DPScheckWood mannequin = mannequins[i];
            if (mannequin == null)
                continue;

            Scene scene = mannequin.gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded || !IsParishChurchSceneName(scene.name))
                continue;

            HideObject(mannequin.gameObject);
        }
    }

    /// <summary>
    /// DPS mannequin uses a separate canvas with timer labels ("1second:", "5second:", "10second:", "Wombo%").
    /// Hide it during shelter assault even if the mannequin trigger zone is still active.
    /// </summary>
    private static void HideParishDpsOverlays()
    {
        TextMeshProUGUI[] tmpLabels = Object.FindObjectsOfType<TextMeshProUGUI>();
        for (int i = 0; i < tmpLabels.Length; i++)
        {
            TextMeshProUGUI label = tmpLabels[i];
            if (label == null)
                continue;

            if (!LooksLikeDpsOverlayLabel(label.text))
                continue;

            Canvas rootCanvas = label.GetComponentInParent<Canvas>();
            if (rootCanvas == null)
                continue;

            Scene scene = rootCanvas.gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded || !IsParishChurchSceneName(scene.name))
                continue;

            CacheDpsCanvas(rootCanvas);
            HideObject(rootCanvas.gameObject);
        }
    }

    private static void CacheDpsCanvas(Canvas canvas)
    {
        if (canvas == null)
            return;

        for (int i = 0; i < _cachedDpsCanvases.Count; i++)
        {
            if (_cachedDpsCanvases[i] == canvas)
                return;
        }

        _cachedDpsCanvases.Add(canvas);
    }

    private static bool LooksLikeDpsOverlayLabel(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        return value.IndexOf("1second", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("5second", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("10second", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("wombo", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsParishChurchSceneName(string sceneName)
    {
        return !string.IsNullOrEmpty(sceneName)
            && sceneName.IndexOf("Parish", StringComparison.OrdinalIgnoreCase) >= 0
            && sceneName.IndexOf("Church", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void CloseActiveTutorialWindowsInParish()
    {
        TextControllerGO[] helpDialogs = Object.FindObjectsOfType<TextControllerGO>();
        for (int i = 0; i < helpDialogs.Length; i++)
        {
            TextControllerGO dialog = helpDialogs[i];
            if (dialog == null)
                continue;

            Scene scene = dialog.gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded || !IsParishChurchSceneName(scene.name))
                continue;

            Object.Destroy(dialog.gameObject);
        }
    }

    private static void TryHideByNameInLoadedScenes(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return;

        ForEachLoadedGameplayScene(scene =>
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
                TryHideByNameRecursive(roots[r].transform, objectName);
        });
    }

    private static void TryHideByNameRecursive(Transform root, string objectName)
    {
        if (root == null)
            return;

        if (string.Equals(root.name, objectName, StringComparison.Ordinal))
            HideObject(root.gameObject);

        for (int i = 0; i < root.childCount; i++)
            TryHideByNameRecursive(root.GetChild(i), objectName);
    }

    private static void TryHideNameContainsInLoadedScenes(string nameFragment)
    {
        if (string.IsNullOrEmpty(nameFragment))
            return;

        ForEachLoadedGameplayScene(scene =>
        {
            if (!IsParishChurchSceneName(scene.name))
                return;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                Transform[] transforms = roots[r].GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    Transform t = transforms[i];
                    if (t == null)
                        continue;

                    string n = t.gameObject.name;
                    if (n.IndexOf(nameFragment, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    HideObject(t.gameObject);
                }
            }
        });
    }

    private static void ForEachLoadedGameplayScene(Action<Scene> action)
    {
        if (action == null)
            return;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            if (HellGateLocationSpawnRefresh.ShouldIgnoreSceneName(scene.name))
                continue;

            action(scene);
        }
    }

    private static void EnsureObjectHidden(GameObject go)
    {
        if (go == null)
            return;

        if (_savedActiveStates.ContainsKey(go))
        {
            if (go.activeSelf)
                go.SetActive(false);
            return;
        }

        HideObject(go);
    }

    private static void HideObject(GameObject go)
    {
        if (go == null || _savedActiveStates.ContainsKey(go))
            return;

        _savedActiveStates[go] = go.activeSelf;
        go.SetActive(false);
    }
}
