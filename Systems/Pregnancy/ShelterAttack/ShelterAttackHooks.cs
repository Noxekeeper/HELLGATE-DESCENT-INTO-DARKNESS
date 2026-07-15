using HarmonyLib;
using NoREroMod.Systems.Spawn;

namespace NoREroMod.Systems.Pregnancy.ShelterAttack;

/// <summary>
/// Zone-change hooks aligned with HellGate spawn refresh entry points.
/// </summary>
internal static class ShelterAttackHooks
{
    internal static void Initialize()
    {
        ShelterAttackDriver.Initialize();
    }

    // Scene-load arrival is handled directly in SceneLoadSpawnRefreshPatch AFTER LoadSceneAndWait
    // finishes (so ParishChurch entry works even when spawn refresh is skipped during assault).
    // RefreshAfterAltar still covers altar-only / same-zone resets.

    [HarmonyPatch(typeof(HellGateLocationSpawnRefresh), nameof(HellGateLocationSpawnRefresh.RefreshAfterAltar))]
    internal static class RefreshAfterAltarPostfix
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
                Plugin.Log?.LogInfo("[Pregnancy.ShelterAttack] RefreshAfterAltar hook fired.");
            ShelterAttackDriver.OnSceneChanged();
        }
    }

    [HarmonyPatch(typeof(HellGateLocationSpawnRefresh), nameof(HellGateLocationSpawnRefresh.NotifyCrossZoneWalkTransition))]
    internal static class NotifyCrossZoneWalkTransitionPostfix
    {
        [HarmonyPostfix]
        private static void Postfix(string fromZone, string toZone)
        {
            if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
            {
                Plugin.Log?.LogInfo(
                    $"[Pregnancy.ShelterAttack] Walk transition hook: \"{fromZone}\" -> \"{toZone}\"");
            }

            ShelterAttackDriver.OnZoneTransition(fromZone, toZone);
        }
    }

    /// <summary>
    /// Altar map teleports run <c>LoadSceneWait</c>, which updates <see cref="StaticMng.Idea_Nowscene"/>
    /// before <see cref="PlayerStatus.LoadSceneAndWait"/> — capture the real origin zone in this prefix.
    /// </summary>
    [HarmonyPatch(typeof(Savepoint_menu), "LoadSceneWait")]
    internal static class SavepointMenuFastTravelPrefix
    {
        private static readonly AccessTools.FieldRef<Savepoint_menu, game_fragmng> FlagMngRef =
            AccessTools.FieldRefAccess<Savepoint_menu, game_fragmng>("FlagMng");

        [HarmonyPrefix]
        private static void Prefix(Savepoint_menu __instance)
        {
            if (!PregnancyConfig.IsEnabled
                || PregnancyConfig.EnableShelterAttack == null
                || !PregnancyConfig.EnableShelterAttack.Value)
                return;

            string fromZone = HellGateLocationSpawnRefresh.GetActiveGameplayZone();
            game_fragmng frag = FlagMngRef(__instance);
            string toZone = frag?._re_Scenename;
            if (string.IsNullOrEmpty(toZone))
            {
                Plugin.Log?.LogWarning("[Pregnancy.ShelterAttack] Fast travel prefix: destination zone is empty.");
                return;
            }

            ShelterAttackDriver.NotifyFastTravelPending(fromZone, toZone);
        }
    }
}
