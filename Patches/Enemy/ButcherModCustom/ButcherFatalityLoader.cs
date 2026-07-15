using System.IO;
using System.Reflection;
using Spine.Unity;
using UnityEngine;
using NoREroMod.Patches.Enemy.RickEnemyModShared;
using NoREroMod.Systems.Spawn;

namespace NoREroMod.Patches.Enemy.ButcherModCustom;

/// <summary>
/// Butcher spawn key: vanilla Slaughterer combat + RickEnemyMod fatality spine + shared Fatality Logo overlay.
/// </summary>
internal static class ButcherFatalityLoader
{
    private static SkeletonDataAsset _fatalitySkeleton;
    private static bool _initFailed;

    public static void ApplyButcherFatality(GameObject spawned)
    {
        if (spawned == null)
            return;

        var slaughterer = spawned.GetComponent<Slaughterer>();
        if (slaughterer == null)
        {
            Plugin.Log?.LogWarning("[ButcherFatalityLoader] Slaughterer component not found");
            return;
        }

        EnemyPrefabRegistry.Initialize();
        GameObject slaughtererPrefab = EnemyPrefabRegistry.GetPrefab("Slaughterer");

        RickEnemyModFatalityLogoLoader.MarkEnemy(spawned);
        RickEnemyModFatalityLogoLoader.TryApplyFatalityIcon(slaughterer, slaughtererPrefab);

        SkeletonDataAsset fatalityAsset = GetFatalitySkeleton(slaughtererPrefab);
        if (fatalityAsset == null)
        {
            Plugin.Log?.LogWarning(
                "[ButcherFatalityLoader] Fatality spine not loaded. Add RickEnemyMod/Butcher/kaitaisya_spine_fatality.png. Fatality Logo may still apply.");
            return;
        }

        var erodataField = typeof(EnemyDate).GetField(
            "erodata",
            BindingFlags.Public | BindingFlags.Instance);
        if (erodataField == null)
            return;

        var erodata = erodataField.GetValue(slaughterer) as GameObject;
        if (erodata == null)
            return;

        var eroSpine = erodata.GetComponent<SkeletonAnimation>();
        if (eroSpine == null)
            return;

        eroSpine.skeletonDataAsset = fatalityAsset;
        eroSpine.Initialize(true);
    }

    private static SkeletonDataAsset GetFatalitySkeleton(GameObject slaughtererPrefabTemplate)
    {
        if (_fatalitySkeleton != null)
            return _fatalitySkeleton;
        if (_initFailed)
            return null;

        string basePath = RickEnemyModPaths.GetBasePath();
        if (string.IsNullOrEmpty(basePath))
        {
            Plugin.Log?.LogWarning("[ButcherFatalityLoader] RickEnemyMod path not found");
            _initFailed = true;
            return null;
        }

        string butcherPath = Path.Combine(basePath, "Butcher");
        _fatalitySkeleton = RickEnemyModSpineLoader.LoadSkeleton(
            Path.Combine(butcherPath, "kaitaisya_spine_fatality.json"),
            Path.Combine(butcherPath, "kaitaisya_spine_fatality.atlas"),
            Path.Combine(butcherPath, "kaitaisya_spine_fatality.png"),
            "kaitaisya_spine_fatality",
            RickEnemyModSpineLoader.BorrowSpineMaterial(slaughtererPrefabTemplate));

        if (_fatalitySkeleton == null)
            _initFailed = true;

        return _fatalitySkeleton;
    }
}
