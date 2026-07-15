using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Patches.Enemy.RickEnemyModShared;

/// <summary>
/// Shared RickEnemyMod overlay that replaces vanilla "FATALITY" text with the skull/blood START animation.
/// </summary>
internal static class RickEnemyModFatalityLogoLoader
{
    private static SkeletonDataAsset _logoSkeleton;
    private static GameObject _logoTemplate;
    private static bool _loadFailed;
    private static MeshRenderer _vanillaRendererSnapshot;

    internal static bool UsesRickFatalityLogo(GameObject enemyRoot)
    {
        if (enemyRoot == null)
            return false;

        return enemyRoot.GetComponent<RickEnemyModFatalityEnemyMarker>() != null
               || string.Equals(enemyRoot.name, "Butcher", StringComparison.Ordinal);
    }

    internal static void MarkEnemy(GameObject enemyRoot)
    {
        if (enemyRoot == null)
            return;

        if (enemyRoot.GetComponent<RickEnemyModFatalityEnemyMarker>() == null)
            enemyRoot.AddComponent<RickEnemyModFatalityEnemyMarker>();
    }

    internal static bool TryApplyFatalityIcon(Component enemy, GameObject materialTemplate = null)
    {
        if (enemy == null)
            return false;

        FieldInfo iconField = AccessTools.Field(enemy.GetType(), "FatalityIcon");
        if (iconField == null)
        {
            Plugin.Log?.LogWarning($"[RickEnemyMod] FatalityIcon field not found on {enemy.GetType().Name}");
            return false;
        }

        GameObject vanillaIcon = iconField.GetValue(enemy) as GameObject;
        CacheVanillaRendererSnapshot(vanillaIcon);

        GameObject template = GetLogoTemplate(materialTemplate);
        if (template == null)
            return false;

        iconField.SetValue(enemy, template);
        return true;
    }

    internal static GameObject GetLogoTemplate(GameObject materialTemplate = null)
    {
        SkeletonDataAsset asset = GetLogoSkeleton(materialTemplate);
        if (asset == null)
            return null;

        if (_logoTemplate != null)
            return _logoTemplate;

        try
        {
            var go = new GameObject("RickEnemyMod_FatalityLogo_Template");
            go.AddComponent<RickEnemyModFatalityLogoMarker>();
            go.AddComponent<RickEnemyModFatalityLogoPlayer>();

            var spine = go.AddComponent<SkeletonAnimation>();
            spine.skeletonDataAsset = asset;
            spine.Initialize(true);

            ApplyRendererSnapshot(go);
            ParkTemplateOffscreen(go);

            UnityEngine.Object.DontDestroyOnLoad(go);
            _logoTemplate = go;
            return _logoTemplate;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[RickEnemyMod] Failed to build Fatality Logo template: {ex.Message}");
            return null;
        }
    }

    internal static void ActivateSpawnedIcon(GameObject clone)
    {
        if (clone == null)
            return;

        clone.SetActive(true);

        var spine = clone.GetComponent<SkeletonAnimation>();
        if (spine != null)
        {
            if (!spine.valid)
                spine.Initialize(true);

            spine.Update(0f);
            spine.LateUpdate();
            spine.state?.SetAnimation(0, "START", false);
        }

        var renderer = clone.GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.enabled = true;
    }

    private static void ParkTemplateOffscreen(GameObject go)
    {
        go.SetActive(true);
        go.transform.position = new Vector3(10000f, 10000f, 0f);
    }

    private static void CacheVanillaRendererSnapshot(GameObject vanillaIcon)
    {
        if (vanillaIcon == null || _vanillaRendererSnapshot != null)
            return;

        var vanillaSpine = vanillaIcon.GetComponent<SkeletonAnimation>();
        if (vanillaSpine == null)
            return;

        _vanillaRendererSnapshot = vanillaSpine.GetComponent<MeshRenderer>();
    }

    private static void ApplyRendererSnapshot(GameObject target)
    {
        if (_vanillaRendererSnapshot == null)
            return;

        var renderer = target.GetComponent<MeshRenderer>();
        if (renderer == null)
            return;

        renderer.sortingLayerID = _vanillaRendererSnapshot.sortingLayerID;
        renderer.sortingOrder = _vanillaRendererSnapshot.sortingOrder;
        renderer.sortingLayerName = _vanillaRendererSnapshot.sortingLayerName;
    }

    private static SkeletonDataAsset GetLogoSkeleton(GameObject materialTemplate)
    {
        if (_logoSkeleton != null)
            return _logoSkeleton;
        if (_loadFailed)
            return null;

        string logoFolder = RickEnemyModPaths.GetFatalityLogoFolder();
        if (string.IsNullOrEmpty(logoFolder))
        {
            _loadFailed = true;
            return null;
        }

        _logoSkeleton = RickEnemyModSpineLoader.LoadSkeleton(
            Path.Combine(logoFolder, "FatalityDeath.json"),
            Path.Combine(logoFolder, "FatalityDeath.atlas"),
            Path.Combine(logoFolder, "FatalityDeath.png"),
            "FatalityDeath",
            RickEnemyModSpineLoader.BorrowSpineMaterial(materialTemplate));

        if (_logoSkeleton == null)
        {
            _loadFailed = true;
            Plugin.Log?.LogWarning(
                $"[RickEnemyMod] Fatality Logo not loaded. Expected: {Path.Combine(logoFolder, "FatalityDeath.png")}");
        }

        return _logoSkeleton;
    }
}

internal sealed class RickEnemyModFatalityEnemyMarker : MonoBehaviour
{
}

/// <summary>
/// Plays START on spawn and self-destructs after the Spine END event (vanilla FatalityDeath behaviour).
/// </summary>
internal sealed class RickEnemyModFatalityLogoPlayer : MonoBehaviour
{
    private SkeletonAnimation _spine;
    private bool _subscribed;

    private void Awake()
    {
        _spine = GetComponent<SkeletonAnimation>();
    }

    private void OnEnable()
    {
        if (_spine == null)
            _spine = GetComponent<SkeletonAnimation>();

        if (_spine != null && !_subscribed)
        {
            _spine.state.Event += OnSpineEvent;
            _subscribed = true;
        }

        if (_spine?.state != null)
            _spine.state.SetAnimation(0, "START", false);
    }

    private void OnDisable()
    {
        if (_spine?.state != null && _subscribed)
        {
            _spine.state.Event -= OnSpineEvent;
            _subscribed = false;
        }
    }

    private void OnSpineEvent(Spine.AnimationState state, int trackIndex, Spine.Event e)
    {
        if (e?.Data != null && e.Data.Name == "END")
            UnityEngine.Object.Destroy(gameObject);
    }
}
