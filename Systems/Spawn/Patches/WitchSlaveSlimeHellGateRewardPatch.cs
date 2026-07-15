using HarmonyLib;
using NoREroMod.Systems.Economy;
using UnityEngine;

namespace NoREroMod.Systems.Spawn.Patches;

/// <summary>
/// Vanilla <see cref="witchslaveslime"/> rescue calls <c>treasureNumSet(30, 2)</c> (ring id 30).
/// HellGate-spawned instances get gold instead — only when <see cref="HellGateSpawnedHostageMarker"/> is present.
/// </summary>
[HarmonyPatch(typeof(witchslaveslime), nameof(witchslaveslime.treasureNumSet))]
internal static class WitchSlaveSlimeHellGateRewardPatch
{
  private const int VanillaRingKind = 2;
  private const int VanillaGoldKind = 0;

  [HarmonyPrefix]
  private static bool Prefix(witchslaveslime __instance, ref int num, ref int kind)
  {
    if (__instance == null || kind != VanillaRingKind)
      return true;

    if (!HellGateSpawnedHostageMarker.TryGet(__instance.gameObject, out HellGateSpawnedHostageMarker marker))
      return true;

    int minGold = Mathf.Max(1, marker.MinGold);
    int maxGold = Mathf.Max(minGold, marker.MaxGold);
    int gold = Random.Range(minGold, maxGold + 1);

    if (EconomicConfig.Enable && GoldAssetLoader.HasFrames)
    {
      Vector2 pos = __instance.transform.position;
      GoldDropAwarder.TrySpawnDrop(pos, gold);
      return false;
    }

    kind = VanillaGoldKind;
    num = gold;
    return true;
  }
}
