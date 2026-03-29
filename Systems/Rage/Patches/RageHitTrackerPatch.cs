using HarmonyLib;
using NoREroMod.Systems.Rage;

namespace NoREroMod.Systems.Rage.Patches;

/// <summary>
/// Registers player hits on enemies for Rage combo accumulation.
/// Uses only OndamageSend / OndamageSendMagic — one hit per player strike.
/// Do NOT also patch WeaponDamage/MagicDamage: vanilla flow calls Send then Damage,
/// so double postfix would count the same strike twice and inflate combo/rage.
/// </summary>
[HarmonyPatch]
internal static class RageHitTrackerPatch
{
    /// <summary>
    /// Registers weapon hit from OndamageSend (tag == "ATKweapon").
    /// </summary>
    [HarmonyPatch(typeof(EnemyDate), "OndamageSend")]
    [HarmonyPostfix]
    private static void OndamageSend_Postfix(EnemyDate __instance, string tag)
    {
        if (!RageSystem.Enabled) return;
        if (__instance == null) return;
        
        // Register only weapon hits.
        if (tag == "ATKweapon")
        {
            RageComboSystem.RegisterHit();
        }
    }
    
    /// <summary>
    /// Registers magic hit from OndamageSendMagic (tag == "ATKmagic").
    /// </summary>
    [HarmonyPatch(typeof(EnemyDate), "OndamageSendMagic")]
    [HarmonyPostfix]
    private static void OndamageSendMagic_Postfix(EnemyDate __instance, string tag, float[] damage, float dir, int attribute, float cut, float FalterDIR)
    {
        if (!RageSystem.Enabled) return;
        if (__instance == null) return;
        
        // Register only magic hits.
        if (tag == "ATKmagic")
        {
            RageComboSystem.RegisterHit();
        }
    }
}
