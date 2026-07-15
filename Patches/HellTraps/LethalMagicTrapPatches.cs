using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Patches.HellTraps;

/// <summary>Harmony entry points for lethal magic trap projectile and player damage hooks.</summary>
internal static class LethalMagicTrapPatches
{
    internal static void ApplyPatches(Harmony harmony)
    {
        if (harmony == null)
            return;

        TryPatchCreateObj(harmony);
        TryPatchBulletHit(harmony, typeof(SetupFireball), "SetupFireball.OnTriggerEnter2D");
        TryPatchBulletHit(harmony, typeof(Fireball), "Fireball.OnTriggerEnter2D");

        harmony.PatchAll(typeof(LethalMagicTrapPatches));
        LethalMagicTrapEroSuppression.ApplyPatches(harmony);
        LethalMagicTrapDeathAudio.ApplyPatches(harmony);
        Plugin.Log?.LogInfo("[LethalMagicTrap] Harmony patches applied.");
    }

    private static void TryPatchBulletHit(Harmony harmony, System.Type bulletType, string label)
    {
        MethodInfo trigger = AccessTools.Method(bulletType, "OnTriggerEnter2D");
        if (trigger == null)
        {
            Plugin.Log?.LogWarning("[LethalMagicTrap] " + label + " not found.");
            return;
        }

        if (bulletType == typeof(SetupFireball))
        {
            MethodInfo prefix = AccessTools.Method(typeof(SetupFireballHitPatch), nameof(SetupFireballHitPatch.Prefix));
            MethodInfo postfix = AccessTools.Method(typeof(SetupFireballHitPatch), nameof(SetupFireballHitPatch.Postfix));
            harmony.Patch(trigger,
                prefix: new HarmonyMethod(prefix),
                postfix: new HarmonyMethod(postfix));
        }
        else
        {
            MethodInfo prefix = AccessTools.Method(typeof(FireballHitPatch), nameof(FireballHitPatch.Prefix));
            MethodInfo postfix = AccessTools.Method(typeof(FireballHitPatch), nameof(FireballHitPatch.Postfix));
            harmony.Patch(trigger,
                prefix: new HarmonyMethod(prefix),
                postfix: new HarmonyMethod(postfix));
        }

        Plugin.Log?.LogInfo("[LethalMagicTrap] Patched " + label + ".");
    }

    private static void TryPatchCreateObj(Harmony harmony)
    {
        MethodInfo createObj = AccessTools.Method(typeof(Magictrap), "Createobj");
        if (createObj == null)
        {
            Plugin.Log?.LogWarning("[LethalMagicTrap] Magictrap.Createobj not found — lethal shots will not configure.");
            return;
        }

        MethodInfo prefix = AccessTools.Method(typeof(LethalMagicTrapCreateObjPatch), nameof(LethalMagicTrapCreateObjPatch.Prefix));
        harmony.Patch(createObj, prefix: new HarmonyMethod(prefix));
        Plugin.Log?.LogInfo("[LethalMagicTrap] Patched Magictrap.Createobj.");
    }

    internal static class LethalMagicTrapCreateObjPatch
    {
        internal static bool Prefix(Magictrap __instance)
        {
            if (!Plugin.enableLethalMagicTrap.Value || __instance == null)
                return true;

            if (!LethalMagicTrapRuntime.TryFireLethalTrapShot(__instance))
                return true;

            return false;
        }
    }

    internal static class SetupFireballHitPatch
    {
        internal static bool Prefix(SetupFireball __instance, Collider2D col)
        {
            if (LethalMagicTrapRuntime.TryHandleLethalBulletHit(__instance, col))
                return false;

            LethalMagicTrapRuntime.TryMarkLethalHitPending(__instance, col);
            return true;
        }

        internal static void Postfix(SetupFireball __instance, Collider2D col)
        {
            if (!Plugin.enableLethalMagicTrap.Value || __instance == null || col == null)
                return;

            if (col.gameObject == null || col.gameObject.tag != "playerDAMAGEcol")
                return;

            if (!LethalMagicTrapRuntime.IsLethalBullet(__instance))
                return;

            playercon player = LethalMagicTrapRuntime.ResolveBulletPlayerForPatch(__instance);
            if (player == null)
                return;

            LethalMagicTrapRuntime.FinalizeLethalBulletHit(player);
        }
    }

    internal static class FireballHitPatch
    {
        internal static bool Prefix(Fireball __instance, Collider2D col)
        {
            if (LethalMagicTrapRuntime.TryHandleLethalBulletHit(__instance, col))
                return false;

            LethalMagicTrapRuntime.TryMarkLethalHitPending(__instance, col);
            return true;
        }

        internal static void Postfix(Fireball __instance, Collider2D col)
        {
            if (!Plugin.enableLethalMagicTrap.Value || __instance == null || col == null)
                return;

            if (col.gameObject == null || col.gameObject.tag != "playerDAMAGEcol")
                return;

            if (!LethalMagicTrapRuntime.IsLethalBullet(__instance))
                return;

            playercon player = LethalMagicTrapRuntime.ResolveBulletPlayerForPatch(__instance);
            if (player == null)
                return;

            LethalMagicTrapRuntime.FinalizeLethalBulletHit(player);
        }
    }

    [HarmonyPatch(typeof(playercon), "fun_damage")]
    internal static class PlayerFunDamagePatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        private static void OverrideLethalDamage(ref float getatk, ref float gettoughcut, ref int kickbackkind)
        {
            if (!Plugin.enableLethalMagicTrap.Value || LethalTrapHitGate.IsCocoonLethalHitActive())
                return;

            if (!LethalMagicTrapDeathContext.IsLethalDamageInFlight &&
                !LethalMagicTrapDeathContext.HasPending &&
                !LethalMagicTrapDeathContext.IsLethalTrapDamageArmed)
            {
                return;
            }

            getatk = LethalMagicTrapRuntime.GetLethalShotAtk();
            gettoughcut = 999f;
            kickbackkind = 0;
        }

        [HarmonyPrefix]
        private static void Prefix(float getatk)
        {
            if (!Plugin.enableLethalMagicTrap.Value || LethalTrapHitGate.IsCocoonLethalHitActive())
                return;

            if (!LethalMagicTrapDeathContext.ShouldTreatAsLethalTrapHit(getatk))
                return;

            LethalMagicTrapDeathContext.MarkPending();
            LethalMagicTrapDeathContext.MarkBulletHitDealtDamage();
            Plugin.Log?.LogInfo(
                "[LethalMagicTrap] fun_damage lethal trap hit armed, getatk="
                + getatk.ToString("0.##"));
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(playercon __instance)
        {
            if (!Plugin.enableLethalMagicTrap.Value || __instance == null ||
                LethalTrapHitGate.IsCocoonLethalHitActive())
            {
                return;
            }

            if (!LethalMagicTrapDeathContext.HasPending &&
                !LethalMagicTrapDeathContext.BulletHitDealtDamage &&
                !LethalMagicTrapDeathContext.IsLethalTrapDamageArmed)
            {
                return;
            }

            LethalMagicTrapRuntime.FinalizeLethalBulletHit(__instance);
        }
    }

}
