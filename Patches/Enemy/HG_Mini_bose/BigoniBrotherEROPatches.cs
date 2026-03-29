using HarmonyLib;
using UnityEngine;
using Spine.Unity;

// Token: 0x02000001 RID: 1
namespace NoREroMod.Patches.Enemy;

/// <summary>
/// Патчи для BigoniBrotherERO - полностью независимые от оригинального Bigoni
/// </summary>
internal class BigoniBrotherEROPatches
{
    // Отслеживание циклов анимации для каждого экземпляра BigoniBrotherERO
    private static System.Collections.Generic.Dictionary<BigoniBrotherERO, int> finEventCounts = new System.Collections.Generic.Dictionary<BigoniBrotherERO, int>();
    private static System.Collections.Generic.Dictionary<BigoniBrotherERO, int> twoEroEventCounts = new System.Collections.Generic.Dictionary<BigoniBrotherERO, int>();
    private static System.Collections.Generic.Dictionary<BigoniBrotherERO, int> start2PlayCounts = new System.Collections.Generic.Dictionary<BigoniBrotherERO, int>();
    private static System.Collections.Generic.HashSet<Bigoni> finishingBigoniBrothers = new System.Collections.Generic.HashSet<Bigoni>();

    /// <summary>
    /// Патч на BigoniBrotherERO.OnEvent - обрабатывает события анимации
    /// </summary>
    [HarmonyPatch(typeof(BigoniBrotherERO), "OnEvent")]
    [HarmonyPrefix]
    private static bool BigoniBrotherERO_OnEvent_Prefix(BigoniBrotherERO __instance, Spine.AnimationState state, int trackIndex, Spine.Event e)
    {
        try
        {
            if (e == null || e.Data == null || __instance.oya == null)
            {
                return true;
            }

            string eventName = e.Data.name;
            string currentAnim = __instance.myspine.AnimationName;

            // Обработка событий для BigoniBrother логики
            HandleBigoniBrotherEvents(__instance, eventName, currentAnim);

            // Продолжаем выполнение оригинального метода
            return true;
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError($"[BigoniBrotherERO] Error in OnEvent: {ex.Message}");
            return true;
        }
    }

    /// <summary>
    /// Обработка событий для BigoniBrother логики
    /// </summary>
    private static void HandleBigoniBrotherEvents(BigoniBrotherERO __instance, string eventName, string currentAnim)
    {
        try
        {
            // Handle START2 event: start coroutine to track animation completion
            if (eventName == "START2")
            {
                // Initialize counter for this BigoniBrother instance
                if (!start2PlayCounts.ContainsKey(__instance))
                {
                    start2PlayCounts[__instance] = 0;
                }

                start2PlayCounts[__instance]++;
                if (start2PlayCounts[__instance] == 1)
                {
                    // Start the H-scene with proper initialization
                    __instance.myspine.AnimationState.SetAnimation(0, "START2", false);
                    __instance.myspine.AnimationState.AddAnimation(0, "ERO", true, 0f);
                }
            }

            // Handle 2ERO transition (when count reaches 2)
            if (eventName == "2ERO")
            {
                if (!twoEroEventCounts.ContainsKey(__instance))
                {
                    twoEroEventCounts[__instance] = 0;
                }

                twoEroEventCounts[__instance]++;
                if (twoEroEventCounts[__instance] == 2)
                {
                    __instance.myspine.AnimationState.SetAnimation(0, "2ERO", true);
                    DarkTonic.MasterAudio.MasterAudio.PlaySound("ero_now11", 1f, null, 0f, null, false, false);
                }
            }

            // Handle FIN transition (when count reaches 15)
            if (eventName == "FIN")
            {
                if (!finEventCounts.ContainsKey(__instance))
                {
                    finEventCounts[__instance] = 0;
                }

                finEventCounts[__instance]++;
                if (finEventCounts[__instance] >= 15)
                {
                    __instance.myspine.AnimationState.SetAnimation(0, "FIN", false);
                    __instance.myspine.AnimationState.AddAnimation(0, "JIGO", false, 0f);

                    // Start finishing coroutine
                    if (__instance.oya != null && !finishingBigoniBrothers.Contains(__instance.oya))
                    {
                        finishingBigoniBrothers.Add(__instance.oya);
                        __instance.oya.StartCoroutine(WaitForFinishAnimationAndEnd(__instance.myspine, __instance.oya, __instance));
                    }
                }
            }

            // Handle END event
            if (eventName == "END")
            {
                if (__instance.oya != null)
                {
                    finishingBigoniBrothers.Remove(__instance.oya);
                }
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError($"[BigoniBrotherERO] Error handling events: {ex.Message}");
        }
    }

    /// <summary>
    /// Корутина для завершения анимации BigoniBrother
    /// </summary>
    private static System.Collections.IEnumerator WaitForFinishAnimationAndEnd(SkeletonAnimation spine, Bigoni oya, BigoniBrotherERO eroInstance)
    {
        yield return new WaitForSeconds(2f); // Wait for JIGO animation

        // Spawn goblins after BigoniBrother defeat
        var playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            var player = playerObj.GetComponent<playercon>();
            if (player != null)
            {
                // Spawn goblin enemies
                NoREroMod.Systems.Spawn.HellGateSpawnManager.SpawnEnemiesByNames(
                    new[] { "goblinero", "goblinero", "goblinero" },
                    player.transform.position + new Vector3(2f, 0f, 0f),
                    1f
                );
            }
        }

        // Fade out and end scene
        yield return new WaitForSeconds(3f);

        if (eroInstance != null && eroInstance.gameObject != null)
        {
            UnityEngine.Object.Destroy(eroInstance.gameObject);
        }
    }
}