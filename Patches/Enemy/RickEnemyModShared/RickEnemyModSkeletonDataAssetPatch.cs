using System;
using HarmonyLib;
using Spine;
using Spine.Unity;

namespace NoREroMod.Patches.Enemy.RickEnemyModShared;

[HarmonyPatch(typeof(SkeletonDataAsset))]
internal static class RickEnemyModSkeletonDataAssetPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("GetSkeletonData", new Type[] { typeof(bool) })]
    static bool GetSkeletonData_Prefix(SkeletonDataAsset __instance, ref SkeletonData __result)
    {
        if (RickEnemyModSpineLoader.CustomAssets.TryGetValue(__instance, out var data))
        {
            __result = data.SkeletonData;
            return false;
        }

        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch("GetAnimationStateData")]
    static bool GetAnimationStateData_Prefix(SkeletonDataAsset __instance, ref AnimationStateData __result)
    {
        if (RickEnemyModSpineLoader.CustomAssets.TryGetValue(__instance, out var data))
        {
            __result = data.StateData;
            return false;
        }

        return true;
    }
}
