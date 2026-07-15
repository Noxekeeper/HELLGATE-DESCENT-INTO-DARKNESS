using HarmonyLib;

namespace NoREroMod.Systems.Gameplay;

/// <summary>Plays optional WAV from <see cref="VengeanceStrikeContent"/> on <see cref="playercon.Stab_fun"/>.</summary>
[HarmonyPatch(typeof(playercon), "Stab_fun")]
internal static class VengeanceStrikeStabSoundPatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.First)]
    private static void Postfix(playercon __instance, ref bool enestabnow)
    {
        if (__instance == null) return;
        if (!(Plugin.enableVengeanceStrikeAssets?.Value ?? true)) return;
        if (!(Plugin.enableVengeanceStrikePlayOnStab?.Value ?? true)) return;
        if (!__instance._stabnow && !enestabnow) return;
        if (!VengeanceStrikeContent.HasStrikeClip) return;

        VengeanceStrikeContent.TryPlayStrikeSound(1f);
    }
}
