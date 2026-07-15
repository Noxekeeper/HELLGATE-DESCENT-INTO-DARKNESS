using HarmonyLib;
using NoREroMod.Systems.Rage;

namespace NoREroMod.Systems.Gameplay;

/// <summary>Toggles hand VFX on <see cref="playercon._stabnow"/> via <see cref="RageHandsParticleSystem"/>.</summary>
[HarmonyPatch(typeof(playercon), "Update")]
internal static class VengeanceStrikeHandsPatch
{
    private static bool _wasStab;

    [HarmonyPostfix]
    [HarmonyPriority(Priority.First)]
    private static void Postfix(playercon __instance)
    {
        if (__instance == null) return;
        if (!(Plugin.enableVengeanceStrikeHandGlow?.Value ?? true)) return;

        bool st = __instance._stabnow;

        if (st)
        {
            if (!_wasStab)
            {
                _wasStab = true;
                RageHandsParticleSystem.ShowVengeanceStrikeHands();
            }
        }
        else if (_wasStab)
        {
            _wasStab = false;
            RageHandsParticleSystem.HideVengeanceStrikeHands();
        }
    }
}
