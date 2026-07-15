using System;
using HarmonyLib;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Patches.Enemy.RickEnemyModShared;

/// <summary>
/// Unity clones inactive prefab templates as inactive GameObjects — Rick logo must be forced active on spawn.
/// </summary>
[HarmonyPatch(typeof(UnityEngine.Object))]
internal static class RickEnemyModFatalityIconInstantiatePatch
{
    [HarmonyPostfix]
    [HarmonyPatch("Instantiate", new Type[] { typeof(UnityEngine.Object), typeof(Vector3), typeof(Quaternion) })]
    static void Instantiate_Vector3_Postfix(UnityEngine.Object __result, UnityEngine.Object original)
    {
        PrepareSpawnedIcon(__result as GameObject, original as GameObject);
    }

    private static void PrepareSpawnedIcon(GameObject clone, GameObject source)
    {
        if (clone == null || source == null)
            return;

        if (source.GetComponent<RickEnemyModFatalityLogoMarker>() == null)
            return;

        RickEnemyModFatalityLogoLoader.ActivateSpawnedIcon(clone);
    }
}

/// <summary>Tags RickEnemyMod Fatality Logo template instances for Instantiate postfix detection.</summary>
internal sealed class RickEnemyModFatalityLogoMarker : MonoBehaviour
{
}
