using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Systems.Gameplay;

/// <summary>Blocks <c>CanEliteGrabPlayer</c> while <see cref="playercon._stabnow"/>. Grab-via-attack uses <see cref="GrabChanceCalculator"/> / <see cref="GrabViaAttackPatch"/>.</summary>
internal static class VengeanceStrikeNoGrabDuringStabPatch
{
    private static bool _applied;

    internal static void Apply(Harmony harmony)
    {
        if (_applied) return;
        try
        {
            var type = typeof(StruggleSystem).Assembly.GetType("NoREroMod.EnemyDatePatch");
            if (type == null)
            {
                Plugin.Log?.LogWarning("[VengeanceStrike] EnemyDatePatch not found; collision grab during stab not blocked.");
                return;
            }

            var prefix = typeof(VengeanceStrikeNoGrabDuringStabPatch).GetMethod(
                nameof(CanEliteGrabPlayer_Prefix),
                BindingFlags.Static | BindingFlags.NonPublic);
            if (prefix == null) return;

            int patched = 0;
            foreach (var m in type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                         .Where(x => x.Name == "CanEliteGrabPlayer"))
            {
                try
                {
                    if (m.GetParameters().Length != 5)
                        continue; // only overload with playercon parameter is needed for stab-state check

                    harmony.Patch(m, prefix: new HarmonyMethod(prefix) { priority = Priority.First });
                    patched++;
                }
                catch (System.Exception ex)
                {
                    Plugin.Log?.LogWarning($"[VengeanceStrike] CanEliteGrabPlayer patch failed: {ex.Message}");
                }
            }

            if (patched > 0)
            {
                _applied = true;
                Plugin.Log?.LogInfo($"[VengeanceStrike] Block grab during stab: patched {patched} CanEliteGrabPlayer overload(s) (5-param).");
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogError($"[VengeanceStrike] NoGrabDuringStab apply failed: {ex.Message}");
        }
    }

    private static bool CanEliteGrabPlayer_Prefix(ref bool __result, playercon pcon)
    {
        if (!(Plugin.enableVengeanceStrikeBlockGrabDuringStab?.Value ?? true))
            return true;

        if (pcon != null && pcon._stabnow)
        {
            __result = false;
            return false;
        }
        return true;
    }
}
