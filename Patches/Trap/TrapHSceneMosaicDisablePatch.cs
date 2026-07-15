using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Patches.Trap;

/// <summary>
/// Disables vanilla mosaic overlays on selected trap H-scene components.
/// HellGate-spawned traps can show a misaligned black box or transparent hole when mosaic stays active.
/// Add new type names to <see cref="MosaicOwnerTypeNames"/> when another trap needs the same fix.
/// </summary>
[HarmonyPatch]
internal static class TrapHSceneMosaicDisablePatch
{
    private static readonly string[] MosaicOwnerTypeNames =
    {
        "RosewarmEro",
        "PunishmentRoomRosewarm",
    };

    static IEnumerable<MethodBase> TargetMethods()
    {
        for (int i = 0; i < MosaicOwnerTypeNames.Length; i++)
        {
            string typeName = MosaicOwnerTypeNames[i];
            Type type = AccessTools.TypeByName(typeName);
            if (type == null)
            {
                Plugin.Log?.LogWarning($"[TrapMosaicDisable] Type not found: {typeName}");
                continue;
            }

            MethodInfo start = AccessTools.Method(type, "Start");
            if (start == null)
            {
                Plugin.Log?.LogWarning($"[TrapMosaicDisable] Start() not found on: {typeName}");
                continue;
            }

            yield return start;
        }
    }

    static void Postfix(object __instance)
    {
        DisableVanillaMosaic(__instance);
    }

    internal static void DisableVanillaMosaic(object instance)
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
