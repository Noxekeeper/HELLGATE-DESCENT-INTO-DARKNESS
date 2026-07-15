using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Patches.Enemy.BossTouzokuCustom;

/// <summary>
/// Blocks vanilla story-boss side effects for coordinate spawns (walls, victory flow, scene unload).
/// </summary>
[HarmonyPatch(typeof(BossTouzoku), "OnDestroy")]
internal static class BossTouzokuCustomOnDestroyPatch
{
    [HarmonyPrefix]
    private static bool Prefix(BossTouzoku __instance)
    {
        return !BossTouzokuCustomStats.IsCustom(__instance);
    }
}

[HarmonyPatch(typeof(BossTouzoku), "treasureNumSet")]
internal static class BossTouzokuCustomTreasurePatch
{
    [HarmonyPrefix]
    private static bool Prefix(BossTouzoku __instance)
    {
        return !BossTouzokuCustomStats.IsCustom(__instance);
    }
}

[HarmonyPatch(typeof(movecameraWall), "movestart")]
internal static class BossTouzokuCustomWallPatch
{
    [HarmonyPrefix]
    private static bool Prefix(movecameraWall __instance)
    {
        BossTouzoku boss = __instance.GetComponentInParent<BossTouzoku>();
        if (boss != null && BossTouzokuCustomStats.IsCustom(boss))
            return false;

        return true;
    }
}
