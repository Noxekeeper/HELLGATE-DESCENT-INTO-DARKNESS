using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Patches.HellTraps;

/// <summary>Harmony hooks for lightningTrap_button (suppress Trapshot, lethal fun_damage).</summary>
internal static class LethalLightningTrapPatches
{
    internal static void ApplyPatches(Harmony harmony)
    {
        if (harmony == null)
            return;

        harmony.PatchAll(typeof(LethalLightningTrapPatches));
        Plugin.Log?.LogInfo("[LethalLightningTrap] Harmony patches applied.");
    }

    /// <summary>Block vanilla Trapshot salvo on lethal lightning buttons.</summary>
    [HarmonyPatch(typeof(Trap_button), "Shoot")]
    internal static class TrapButtonShootPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Trap_button __instance)
        {
            if (!Plugin.IsLethalLightningTrapActive || __instance == null)
                return true;

            if (!LethalLightningTrapRegistry.IsLethalLightningTrap(__instance))
                return true;

            return false;
        }
    }

    [HarmonyPatch(typeof(playercon), "fun_damage")]
    internal static class PlayerFunDamagePatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void ForceLethalKnockbackOff(ref int kickbackkind)
        {
            if (!LethalTrapHitGate.IsLightningLethalHitActive())
                return;

            kickbackkind = 0;
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        private static void OverrideLethalDamage(ref float getatk, ref float gettoughcut, ref int kickbackkind)
        {
            if (!Plugin.IsLethalLightningTrapActive ||
                LethalTrapHitGate.IsMagicLethalHitActive() ||
                LethalTrapHitGate.IsCocoonLethalHitActive())
            {
                return;
            }

            if (!LethalLightningTrapDeathContext.IsLethalDamageInFlight &&
                !LethalLightningTrapDeathContext.HasPending &&
                !LethalLightningTrapDeathContext.HitDealtDamage)
            {
                return;
            }

            getatk = LethalLightningTrapRuntime.GetLethalAtk();
            gettoughcut = 999f;
            kickbackkind = 0;
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(playercon __instance)
        {
            if (!LethalTrapHitGate.IsLightningLethalHitActive() || __instance == null)
                return;

            if (__instance.erodown != 0)
                __instance.erodown = 0;

            LethalMagicTrapEroSuppression.PinPlayerBody(__instance);

            Rigidbody2D body = __instance.rigi2d;
            if (body != null)
            {
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
        }
    }
}
