using System.Reflection;
using HarmonyLib;
using UnityEngine;
using NoREroMod;
using NoREroMod.Patches.Enemy.MafiaBossCustom;

namespace NoREroMod.Patches.Enemy.MafiaBossCustom;

/// <summary>
/// On MafiaBossCustom grab:
/// 1) For the first mafia, set start variant 0
/// 2) For subsequent mafias (GlobalHandoffCount > 0) immediately replace START with STARTERO/2ERO/4ERO
/// 
/// CRITICAL: the swap happens immediately after SetAnimation in the original code,
/// to avoid double work (START → interrupt → STARTERO)
/// </summary>
[HarmonyPatch(typeof(Mafiamuscle), "OnTriggerStay2D")]
internal static class MafiaBossCustomGrabPatch
{
    private static readonly BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;
    
    // Critical optimization: cache FieldInfo for erospine
    // IMPORTANT: eroflag is a PUBLIC field on EnemyDate — no reflection needed!
    private static FieldInfo _cachedErospineField;
    
    static MafiaBossCustomGrabPatch()
    {
        try
        {
            _cachedErospineField = typeof(Mafiamuscle).GetField("erospine", Priv);
            if (_cachedErospineField == null)
            {
                Plugin.Log?.LogError($"[MafiaBossCustomGrabPatch] Failed to find 'erospine' field in Mafiamuscle!");
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogError($"[MafiaBossCustomGrabPatch] Failed to cache fields: {ex.Message}");
        }
    }

    [HarmonyPostfix]
    private static void Postfix(Mafiamuscle __instance, Collider2D collision)
    {
        try
        {
            // DIAGNOSTIC: verify this is MafiaBossCustom
            bool isCustom = MafiaBossCustomStats.IsMafiaBossCustom(__instance);
            if (!isCustom)
                return;
            
            if (collision?.gameObject?.tag != "playerDAMAGEcol")
                return;
            
            // eroflag is a PUBLIC field on EnemyDate — access it directly!
            if (!__instance.eroflag)
                return;
            
            // DIAGNOSTIC: log every grab
            int globalCount = EnemyHandoffSystem.GlobalHandoffCount;
            int currentVariant = MafiaBossCustomPassLogic.GetVariant(__instance);
            Plugin.Log?.LogInfo($"[MafiaBossCustomGrabPatch] Grab detected! GlobalHandoffCount={globalCount}, CurrentVariant={currentVariant}, Name={__instance.gameObject.name}");
            
            // First mafia: just set start variant 0
            if (globalCount == 0)
            {
                MafiaBossCustomPassLogic.SetVariant(__instance, 0);
                Plugin.Log?.LogInfo($"[MafiaBossCustomGrabPatch] First mafia - set variant 0");
                return;
            }
            
            // Subsequent mafias: immediately replace START with a random start animation
            if (currentVariant == 0)
            {
                if (_cachedErospineField == null)
                {
                    Plugin.Log?.LogWarning($"[MafiaBossCustomGrabPatch] _cachedErospineField is NULL!");
                    return;
                }
                
                var erospine = _cachedErospineField.GetValue(__instance) as Spine.Unity.SkeletonAnimation;
                if (erospine == null)
                {
                    Plugin.Log?.LogWarning($"[MafiaBossCustomGrabPatch] erospine is NULL!");
                    return;
                }
                
                // Pick a random start variant
                int[] allowedVariants = { 1, 2, 4 };
                int variant = allowedVariants[UnityEngine.Random.Range(0, 3)];
                MafiaBossCustomPassLogic.SetVariant(__instance, variant);
                
                string startAnim;
                bool loop;
                switch (variant)
                {
                    case 1: startAnim = "STARTERO"; loop = false; break;
                    case 2: startAnim = "2ERO"; loop = true; break;
                    case 4: startAnim = "4ERO"; loop = true; break;
                    default: startAnim = "STARTERO"; loop = false; break;
                }
                
                // Swap the animation immediately (START has not played its events yet)
                erospine.state.SetAnimation(0, startAnim, loop);
                erospine.timeScale = 1f;
                
                Plugin.Log?.LogInfo($"[MafiaBossCustom] ✅ Handoff #{globalCount}: START → {startAnim}");
            }
            else
            {
                Plugin.Log?.LogInfo($"[MafiaBossCustomGrabPatch] Variant already set to {currentVariant}, skipping animation swap");
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogError($"[MafiaBossCustomGrabPatch] EXCEPTION: {ex.Message}\n{ex.StackTrace}");
        }
    }
}
