using HarmonyLib;
using UnityEngine;

// Token: 0x02000001 RID: 1
namespace NoREroMod.Patches.Enemy;

/// <summary>
/// Патч для Bigoni.EROstartset() - заменяет StartBigoniERO на BigoniBrotherERO для BigoniBrother
/// </summary>
internal class BigoniBrotherSpawner
{
    /// <summary>
    /// Патч на Bigoni.EROstartset() - определяет BigoniBrother и создает правильный ERO компонент
    /// </summary>
    [HarmonyPatch(typeof(Bigoni), "EROstartset")]
    [HarmonyPrefix]
    private static bool BigoniEROstartset_Prefix(Bigoni __instance)
    {
        try
        {
            // Проверяем, является ли это BigoniBrother
            if (__instance.gameObject != null && __instance.gameObject.name.Contains("BigoniBrother"))
            {
                Plugin.Log.LogInfo("[BigoniBrotherSpawner] Detected BigoniBrother - using BigoniBrotherERO");

                // Создаем BigoniBrotherERO вместо StartBigoniERO
                __instance.ero = __instance.erodata.AddComponent<BigoniBrotherERO>();
                __instance.erospine = __instance.erodata.GetComponent<SkeletonAnimation>();

                if (__instance.erodata.activeSelf)
                {
                    __instance.erodata.SetActive(false);
                }

                // Устанавливаем ссылку на oya в BigoniBrotherERO
                var bigoniBrotherEro = __instance.ero as BigoniBrotherERO;
                if (bigoniBrotherEro != null)
                {
                    bigoniBrotherEro.oya = __instance;
                }

                // НЕ вызываем оригинальный метод
                return false;
            }
            else
            {
                Plugin.Log.LogInfo("[BigoniBrotherSpawner] Detected regular Bigoni - using original StartBigoniERO");
                // Для обычного Bigoni вызываем оригинальный метод
                return true;
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError($"[BigoniBrotherSpawner] Error in EROstartset: {ex.Message}");
            return true; // Fallback to original
        }
    }
}