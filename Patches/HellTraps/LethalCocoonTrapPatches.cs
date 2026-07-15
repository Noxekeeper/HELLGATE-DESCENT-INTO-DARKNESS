using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Patches.HellTraps;

/// <summary>Harmony hooks for lethal cocoon trap (Cocoontrap) damage and death flow.</summary>
internal static class LethalCocoonTrapPatches
{
    internal static void ApplyPatches(Harmony harmony)
    {
        if (harmony == null)
            return;

        TryPatchOndamageSend(harmony);
        harmony.PatchAll(typeof(LethalCocoonTrapPatches));
        Plugin.Log?.LogInfo("[LethalCocoonTrap] Harmony patches applied.");
    }

    private static void TryPatchOndamageSend(Harmony harmony)
    {
        MethodInfo onDamage = AccessTools.Method(typeof(Cocoontrap), "OndamageSend");
        if (onDamage == null)
        {
            Plugin.Log?.LogWarning("[LethalCocoonTrap] Cocoontrap.OndamageSend not found.");
            return;
        }

        MethodInfo prefix = AccessTools.Method(
            typeof(CocoontrapOndamageSendPatch),
            nameof(CocoontrapOndamageSendPatch.Prefix));
        harmony.Patch(onDamage, prefix: new HarmonyMethod(prefix));
        Plugin.Log?.LogInfo("[LethalCocoonTrap] Patched Cocoontrap.OndamageSend.");
    }

    /// <summary>Primary path: block vanilla ExecuteEvents and run lethal flow here.</summary>
    [HarmonyPatch(typeof(playerDamage), "OnTriggerEnter2D")]
    internal static class PlayerDamageCocoonLethalPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(playerDamage __instance, Collider2D attack)
        {
            if (!Plugin.enableLethalCocoonTrap.Value || __instance == null || attack == null)
                return true;

            if (attack.tag != "playerDAMAGEcol")
                return true;

            if (!LethalCocoonTrapRegistry.IsLethalCocoonTrap(__instance))
                return true;

            if (LethalCocoonTrapRuntime.TryHandleLethalPlayerDamage(__instance, attack))
                return false;

            Plugin.Log?.LogWarning(
                "[LethalCocoonTrap] Lethal trap playerDamage on '"
                + __instance.transform.root.name
                + "' fell through — blocking vanilla anyway.");
            return false;
        }
    }

    /// <summary>Skip vanilla warning icon on lethal cocoon (tiny red "?" above trap).</summary>
    [HarmonyPatch(typeof(Cocoontrap), "OnTriggerEnter2D")]
    internal static class CocoontrapTriggerLethalPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Cocoontrap __instance)
        {
            if (!Plugin.enableLethalCocoonTrap.Value || __instance == null)
                return;

            if (!LethalCocoonTrapRuntime.IsLethalTrap(__instance))
                return;

            if (CocoonWarningIconField != null)
                CocoonWarningIconField.SetValue(__instance, null);
        }

        private static readonly System.Reflection.FieldInfo CocoonWarningIconField =
            AccessTools.Field(typeof(Cocoontrap), "warningIcon");
    }

    [HarmonyPatch(typeof(Cocoontrap), "OndamageSend")]
    internal static class CocoontrapOndamageSendPatch
    {
        internal static bool Prefix(Cocoontrap __instance, string tag)
        {
            if (!Plugin.enableLethalCocoonTrap.Value || __instance == null)
                return true;

            if (!LethalCocoonTrapRuntime.IsLethalTrap(__instance))
                return true;

            if (LethalCocoonTrapRuntime.TryHandleLethalDamageSend(__instance, tag))
                return false;

            return true;
        }
    }

    [HarmonyPatch(typeof(playercon), "fun_damage")]
    internal static class PlayerFunDamagePatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void ForceLethalKnockbackOff(ref int kickbackkind)
        {
            if (!LethalTrapHitGate.IsCocoonLethalHitActive())
                return;

            kickbackkind = 0;
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        private static void OverrideLethalDamage(ref float getatk, ref float gettoughcut, ref int kickbackkind)
        {
            if (!Plugin.enableLethalCocoonTrap.Value || LethalTrapHitGate.IsMagicLethalHitActive())
                return;

            if (!LethalCocoonTrapDeathContext.IsLethalDamageInFlight &&
                !LethalCocoonTrapDeathContext.HasPending &&
                !LethalCocoonTrapDeathContext.HitDealtDamage)
            {
                return;
            }

            getatk = LethalMagicTrapRuntime.GetLethalShotAtk();
            gettoughcut = 999f;
            kickbackkind = 0;
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(playercon __instance)
        {
            if (!LethalTrapHitGate.IsCocoonLethalHitActive() || __instance == null)
                return;

            if (__instance.erodown != 0)
                __instance.erodown = 0;

            LethalMagicTrapEroSuppression.PinPlayerBody(__instance);

            // Vanilla HP<=0 branch calls fun_death(3) and can knock the player away from the trap anchor.
            Rigidbody2D body = __instance.rigi2d;
            if (body != null)
            {
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
        }
    }

}
