using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Patches.Enemy;

/// <summary>
/// Disables vanilla mosaic overlays on Gorotuki H-scenes.
/// Same idea as <see cref="Trap.TrapHSceneMosaicDisablePatch"/> (Rosewarm):
/// broken/missing mosaic sprites show as solid white rectangles over genitals.
/// GorotukiERO re-enables mosaics mid-scene (START3 / MOZA2), so we also postfix OnEvent / OnEnable.
/// </summary>
[HarmonyPatch]
internal static class GorotukiHSceneMosaicDisablePatch
{
    private static readonly string[] OwnerTypeNames =
    {
        "GorotukiERO",
        "GAgorotukiERO"
    };

    static IEnumerable<MethodBase> TargetMethods()
    {
        for (int i = 0; i < OwnerTypeNames.Length; i++)
        {
            Type type = AccessTools.TypeByName(OwnerTypeNames[i]);
            if (type == null)
            {
                Plugin.Log?.LogWarning($"[GorotukiMosaicDisable] Type not found: {OwnerTypeNames[i]}");
                continue;
            }

            MethodInfo start = AccessTools.Method(type, "Start");
            if (start != null)
                yield return start;

            MethodInfo onEnable = AccessTools.Method(type, "OnEnable");
            if (onEnable != null)
                yield return onEnable;

            MethodInfo onEvent = AccessTools.Method(
                type,
                "OnEvent",
                new[] { typeof(Spine.AnimationState), typeof(int), typeof(Spine.Event) });
            if (onEvent != null)
                yield return onEvent;
        }
    }

    static void Postfix(object __instance)
    {
        DisableVanillaMosaic(__instance);
    }

    private static void DisableVanillaMosaic(object instance)
    {
        if (instance == null)
            return;

        FieldInfo mosaicField = AccessTools.Field(instance.GetType(), "mosaic");
        if (mosaicField == null)
            return;

        object value = mosaicField.GetValue(instance);
        if (value is GameObject single)
        {
            if (single != null)
                single.SetActive(false);
            return;
        }

        if (value is GameObject[] array)
        {
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] != null)
                    array[i].SetActive(false);
            }
        }
    }
}
