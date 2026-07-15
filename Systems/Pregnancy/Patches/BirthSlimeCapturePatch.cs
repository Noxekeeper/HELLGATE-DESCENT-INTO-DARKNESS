using HarmonyLib;

namespace NoREroMod.Systems.Pregnancy.Patches;

/// <summary>
/// Catches the birth slime once it spawns. The vanilla birth sequence uses a suraimu
/// (directly or via a MonsterChild), so we patch suraimu.Start and attach a transformer
/// that will turn the slime into a Witch-faction MafiaMuscle offspring after a delay.
/// </summary>
[HarmonyPatch(typeof(suraimu), "Start")]
internal static class BirthSlimeCapturePatch
{
    [HarmonyPostfix]
    private static void Postfix(suraimu __instance)
    {
        if (!PregnancyConfig.IsEnabled)
            return;

        if (__instance == null || __instance.gameObject == null)
            return;

        if (BirthSpawnOverridePatch.TryClaimPendingBirth(__instance.gameObject, out ChildData child, out int faction, out float scale))
        {
            float slimeScale = PregnancyConfig.BirthSlimeDisplayScale?.Value ?? 0.5f;
            var enemyDate = __instance.GetComponent<EnemyDate>();
            if (enemyDate != null)
                WitchOffspringVisuals.ApplyUniformOffspringScale(enemyDate, slimeScale);

            var transformer = __instance.gameObject.AddComponent<WitchOffspringTransformer>();
            transformer.Initialize(child, faction, scale);

            if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
                Plugin.Log?.LogInfo($"[Pregnancy.Birth] WitchOffspringTransformer attached to slime {__instance.gameObject.name} (slimeScale={slimeScale:F2})");
        }
    }
}
