using System.Linq;
using System.Reflection;
using HarmonyLib;
using NoREroMod.Systems.GrabSystem;
using UnityEngine;

namespace NoREroMod.Systems.Pregnancy.Patches;

/// <summary>
/// Blocks friendly fire between Aradia, her offspring, and other Witch-faction companions when enabled in Pregnancy config.
/// </summary>
internal static class WitchOffspringFriendlyFirePatch
{
    /// <summary>Runs before GrabViaAttack (Priority.First = 800).</summary>
    private const int BlockPriority = 900;

    private static bool _reflectiveGrabPatched;

    internal static void Apply(Harmony harmony)
    {
        if (_reflectiveGrabPatched)
            return;

        try
        {
            var type = typeof(StruggleSystem).Assembly.GetType("NoREroMod.EnemyDatePatch");
            if (type == null)
            {
                Plugin.Log?.LogWarning("[Pregnancy.Offspring] EnemyDatePatch not found; collision grab block skipped.");
                return;
            }

            MethodInfo canGrabPrefix = typeof(WitchOffspringFriendlyFirePatch).GetMethod(
                nameof(CanEliteGrabPlayer_Prefix),
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo eliteGrabPrefix = typeof(WitchOffspringFriendlyFirePatch).GetMethod(
                nameof(EliteGrabPlayer_Prefix),
                BindingFlags.Static | BindingFlags.NonPublic);
            if (canGrabPrefix == null || eliteGrabPrefix == null)
                return;

            int canGrabPatched = 0;
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                         .Where(x => x.Name == "CanEliteGrabPlayer"))
            {
                try
                {
                    harmony.Patch(method, prefix: new HarmonyMethod(canGrabPrefix) { priority = BlockPriority });
                    canGrabPatched++;
                }
                catch (System.Exception ex)
                {
                    Plugin.Log?.LogWarning($"[Pregnancy.Offspring] CanEliteGrabPlayer patch failed: {ex.Message}");
                }
            }

            int eliteGrabPatched = 0;
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                         .Where(x => x.Name == "EliteGrabPlayer"))
            {
                try
                {
                    harmony.Patch(method, prefix: new HarmonyMethod(eliteGrabPrefix) { priority = BlockPriority });
                    eliteGrabPatched++;
                }
                catch (System.Exception ex)
                {
                    Plugin.Log?.LogWarning($"[Pregnancy.Offspring] EliteGrabPlayer patch failed: {ex.Message}");
                }
            }

            if (canGrabPatched > 0 || eliteGrabPatched > 0)
            {
                _reflectiveGrabPatched = true;
                Plugin.Log?.LogInfo(
                    $"[Pregnancy.Offspring] Patched grab blockers: CanEliteGrabPlayer={canGrabPatched}, EliteGrabPlayer={eliteGrabPatched}.");
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogWarning($"[Pregnancy.Offspring] Apply friendly-fire patches failed: {ex.Message}");
        }
    }

    private static bool CanEliteGrabPlayer_Prefix(EnemyDate __instance, ref bool __result)
    {
        if (!WitchOffspringCombatRules.ShouldBlockOffspringVsPlayer(__instance))
            return true;

        __result = false;
        return false;
    }

    private static bool EliteGrabPlayer_Prefix(EnemyDate __instance)
    {
        if (!WitchOffspringCombatRules.ShouldBlockOffspringVsPlayer(__instance))
            return true;

        return false;
    }

    private static bool ShouldBlockOffspringMeleeHit(MonoBehaviour damageSource)
    {
        if (damageSource == null)
            return false;

        EnemyDate attacker = damageSource.GetComponentInParent<EnemyDate>();
        return WitchOffspringCombatRules.ShouldBlockOffspringVsPlayer(attacker);
    }

    [HarmonyPatch(typeof(playerDamage), "OnTriggerEnter2D")]
    internal static class BlockOffspringPlayerDamageTriggerPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(BlockPriority)]
        private static bool Prefix(playerDamage __instance, Collider2D col)
        {
            if (col == null || !col.CompareTag("playerDAMAGEcol"))
                return true;

            if (!ShouldBlockOffspringMeleeHit(__instance))
                return true;

            GrabViaAttackContext.Reset();
            return false;
        }
    }

    [HarmonyPatch(typeof(SlashDamage), "OnTriggerEnter2D")]
    internal static class BlockOffspringSlashDamageTriggerPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(BlockPriority)]
        private static bool Prefix(SlashDamage __instance, Collider2D col)
        {
            if (col == null || !col.CompareTag("playerDAMAGEcol"))
                return true;

            if (!ShouldBlockOffspringMeleeHit(__instance))
                return true;

            GrabViaAttackContext.Reset();
            return false;
        }
    }

    [HarmonyPatch(typeof(ImpactDamage), "OnTriggerEnter2D")]
    internal static class BlockOffspringImpactDamageTriggerPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(BlockPriority)]
        private static bool Prefix(ImpactDamage __instance, Collider2D col)
        {
            if (col == null || !col.CompareTag("playerDAMAGEcol"))
                return true;

            if (!ShouldBlockOffspringMeleeHit(__instance))
                return true;

            GrabViaAttackContext.Reset();
            return false;
        }
    }

    [HarmonyPatch(typeof(playercon), "fun_damage")]
    internal static class BlockOffspringFunDamagePatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(BlockPriority)]
        private static bool Prefix()
        {
            EnemyDate attacker = GrabViaAttackContext.CurrentAttacker;
            if (!WitchOffspringCombatRules.ShouldBlockOffspringVsPlayer(attacker))
                return true;

            GrabViaAttackContext.Reset();
            return false;
        }
    }

    [HarmonyPatch(typeof(playercon), "fun_damage_Improvement")]
    internal static class BlockOffspringFunDamageImprovementPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(BlockPriority)]
        private static bool Prefix()
        {
            EnemyDate attacker = GrabViaAttackContext.CurrentAttacker;
            if (!WitchOffspringCombatRules.ShouldBlockOffspringVsPlayer(attacker))
                return true;

            GrabViaAttackContext.Reset();
            return false;
        }
    }

    [HarmonyPatch(typeof(EnemyDate), "OndamageSend")]
    internal static class BlockOffspringOndamageSendPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(BlockPriority)]
        private static bool Prefix(EnemyDate __instance, string tag)
        {
            if (tag == "playerDAMAGEcol")
                return !WitchOffspringCombatRules.ShouldBlockOffspringVsPlayer(__instance);

            if (IsPlayerAttackTag(tag))
                return !WitchOffspringCombatRules.ShouldBlockPlayerVsOffspring(__instance);

            return true;
        }

        private static bool IsPlayerAttackTag(string tag)
        {
            return tag == "ATKweapon" || tag == "ATKstab" || tag == "ATKmagic";
        }
    }

    [HarmonyPatch(typeof(Mafiamuscle), "OnTriggerStay2D")]
    internal static class BlockOffspringMafiaMuscleDownedGrabPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(BlockPriority)]
        private static bool Prefix(Mafiamuscle __instance, Collider2D collision)
        {
            if (!WouldTriggerVanillaDownedGrab(__instance, collision))
                return true;

            if (!WitchOffspringCombatRules.ShouldBlockOffspringVsPlayer(__instance))
                return true;

            return false;
        }

        private static bool WouldTriggerVanillaDownedGrab(Mafiamuscle enemy, Collider2D collision)
        {
            if (enemy == null || collision?.gameObject == null)
                return false;
            if (collision.gameObject.tag != "playerDAMAGEcol")
                return false;
            if (enemy.com_player == null || enemy.com_player.eroflag || enemy.eroflag)
                return false;
            if (enemy.state != Mafiamuscle.enemystate.EROWALK)
                return false;
            if (enemy.com_player.state != "DOWN")
                return false;

            return Mathf.Abs(enemy.distance) <= 1f;
        }
    }

    [HarmonyPatch(typeof(EnemyDate), "OndamageSendMagic")]
    internal static class BlockPlayerMagicHitOffspringPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(BlockPriority)]
        private static bool Prefix(EnemyDate __instance, string tag)
        {
            if (tag != "ATKmagic")
                return true;

            return !WitchOffspringCombatRules.ShouldBlockPlayerVsOffspring(__instance);
        }
    }
}
