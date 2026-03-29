using System;
using HarmonyLib;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Patches.UI.MindBroken;

internal static class MutudeMindbrokenControl
{
    internal static readonly string[] TargetAnimations = { "DRINK", "ERO3", "ERO4", "ERO5" };

    [HarmonyPatch(typeof(Mutudeero), "Start")]
    [HarmonyPostfix]
    private static void AttachTracker(Mutudeero __instance)
    {
        try
        {
            if (__instance == null) return;
            var tracker = __instance.gameObject.GetComponent<MutudeMindbrokenTracker>();
            if (tracker == null)
            {
                tracker = __instance.gameObject.AddComponent<MutudeMindbrokenTracker>();
            }
            tracker.Init(__instance);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[MutudeMindbrokenControl] Error attaching tracker: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(MultipleAtkBadstatus), "OnTriggerStay2D")]
    [HarmonyPrefix]
    private static bool BoostBadstatus(Collider2D col, MultipleAtkBadstatus __instance)
    {
        try
        {
            if (col == null || col.gameObject == null) return true;
            if (col.gameObject.tag != "playerDAMAGEcol") return true;

            // Достаём приватные поля через Harmony
            var countField = AccessTools.Field(typeof(MultipleAtkBadstatus), "count");
            var plField = AccessTools.Field(typeof(MultipleAtkBadstatus), "Pl");
            if (countField == null || plField == null) return true;

            float count = (float)(countField.GetValue(__instance) ?? 0f);
            count += Time.deltaTime;

            if (count > 1f)
            {
                var pl = plField.GetValue(__instance) as PlayerStatus;
                if (pl == null)
                {
                    pl = NoREroMod.Systems.Cache.UnifiedGameControllerCacheManager.GetPlayerStatus();
                    if (pl != null)
                    {
                        plField.SetValue(__instance, pl);
                    }
                }

                if (pl != null)
                {
                    // Base value 35 -> умножаем on 3 (200% усиление)
                    pl.BadstatusValPlus(35f * 3f);
                }
                count = 0f;
            }

            countField.SetValue(__instance, count);
            // Skip оригинальный метод, так as обработали сами
            return false;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[MutudeMindbrokenControl] Error boosting badstatus: {ex.Message}");
            // In case of error not блокируем оригинал
            return true;
        }
    }
}

internal class MutudeMindbrokenTracker : MonoBehaviour
{
    private Mutudeero host;
    private SkeletonAnimation spine;

    internal void Init(Mutudeero h)
    {
        host = h;
        spine = h != null ? h.GetComponent<SkeletonAnimation>() : null;
    }

    private void Update()
    {
        if (host == null || spine == null || spine.AnimationState == null) return;

        string anim = spine.AnimationName ?? string.Empty;
        if (string.IsNullOrEmpty(anim)) return;

        anim = anim.ToUpperInvariant();
        foreach (var target in MutudeMindbrokenControl.TargetAnimations)
        {
            if (anim.Contains(target))
            {
                // Value is expressed in percent-per-second (e.g., 1 = +1% per second).
                float perSecondPercent = Plugin.mutudeMindBrokenPerSecondPercent?.Value ?? 1f;
                MindBrokenSystem.AddPercent((perSecondPercent / 100f) * Time.deltaTime, "mutude-ero");
                break;
            }
        }
    }
}

