using HarmonyLib;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Systems.Gameplay;

/// <summary>Vengeance Strike: sets <see cref="SkeletonAnimation.timeScale"/> while <see cref="playercon._stabnow"/>.</summary>
[HarmonyPatch(typeof(playercon), "Update")]
internal static class VengeanceStrikePlayerUpdatePatch
{
    private static bool _boostedLastFrame;

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(playercon __instance)
    {
        if (__instance == null) return;

        var spine = __instance.gameObject.GetComponent<SkeletonAnimation>();
        if (spine == null)
            spine = __instance.gameObject.GetComponentInChildren<SkeletonAnimation>(true);

        if (!__instance._stabnow || !(Plugin.enableVengeanceStrikeSpineBoost?.Value ?? true))
        {
            // Previous frames used assignment (not *=); restore vanilla pace when stab ends.
            if (_boostedLastFrame && spine != null)
                spine.timeScale = 1f;
            _boostedLastFrame = false;
            return;
        }

        float mult = Plugin.vengeanceStrikeSpineMultiplier?.Value ?? 2f;
        if (mult <= 0.01f) return;

        if (Plugin.vengeanceStrikeSpineCompensateSlowMo?.Value ?? false)
        {
            float ts = Time.timeScale;
            if (ts > 0.001f && ts < 0.999f)
                mult *= 1f / ts;
        }

        if (spine == null) return;

        // Assign, never multiply — *= compounded every Update and left spine.timeScale huge after stab.
        spine.timeScale = mult;
        _boostedLastFrame = true;
    }
}
