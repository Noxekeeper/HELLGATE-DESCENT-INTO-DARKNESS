using System.Reflection;
using HarmonyLib;
using UnityEngine;
using NoREroMod;
using NoREroMod.Patches.Enemy.MafiaBossCustom;

namespace NoREroMod.Patches.Enemy.MafiaBossCustom;

/// <summary>
/// On захвате MafiaBossCustom:
/// 1) Для первого мафии выставляем вариант 0
/// 2) Для последующtheir (GlobalHandoffCount > 0) СРАЗУ подменяем START on STARTERO/2ERO/4ERO
/// 
/// ✨ КРИТИЧНО: Подмеon происходит СРАЗУ after SetAnimation in оригинальном коде,
/// so that избежать двойной работы (START → прерывание → STARTERO)
/// </summary>
[HarmonyPatch(typeof(Mafiamuscle), "OnTriggerStay2D")]
internal static class MafiaBossCustomGrabPatch
{
    private static readonly BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;
    
    // Critical optimization: Кэшируем FieldInfo for erospine
    // IMPORTANT: eroflag - this PUBLIC поле in EnemyDate, not нужon рефлексия!
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
            // ДИАГНОСТИКА: Проверяем that this MafiaBossCustom
            bool isCustom = MafiaBossCustomStats.IsMafiaBossCustom(__instance);
            if (!isCustom)
                return;
            
            if (collision?.gameObject?.tag != "playerDAMAGEcol")
                return;
            
            // ✅ eroflag - this PUBLIC поле in EnemyDate, доступbut directly!
            if (!__instance.eroflag)
                return;
            
            // ДИАГНОСТИКА: Log каждый захват
            int globalCount = EnemyHandoffSystem.GlobalHandoffCount;
            int currentVariant = MafiaBossCustomPassLogic.GetVariant(__instance);
            Plugin.Log?.LogInfo($"[MafiaBossCustomGrabPatch] Grab detected! GlobalHandoffCount={globalCount}, CurrentVariant={currentVariant}, Name={__instance.gameObject.name}");
            
            // For первого мафии - просто выставляем вариант 0
            if (globalCount == 0)
            {
                MafiaBossCustomPassLogic.SetVariant(__instance, 0);
                Plugin.Log?.LogInfo($"[MafiaBossCustomGrabPatch] First mafia - set variant 0");
                return;
            }
            
            // ✨ ДЛЯ ПОСЛЕДУЮЩИХ МАФИЙ: Сразу подменяем START on случайную animation
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
                
                // Выбираем случайный вариант старта
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
                
                // Подменяем animation СРАЗУ (START еще not успел проиграть events)
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
