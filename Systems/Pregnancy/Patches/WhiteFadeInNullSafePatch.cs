using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Systems.Pregnancy.Patches;

/// <summary>
/// NoREroMod <c>UImngPatch.WhiteFadeIn</c> does <c>GameObject.Find("UIeffect").GetComponent...</c>
/// without a null check. HellGate black-background FIN hides overlay canvases, so Find returns
/// null and orgasm flashes throw <see cref="NullReferenceException"/> — aborting ERO OnEvent
/// before <c>Nakadasi</c> (Kinoko / MushroomERO FIN is the smoking gun).
/// </summary>
[HarmonyPatch]
internal static class WhiteFadeInNullSafePatch
{
    static MethodBase TargetMethod()
    {
        // UImngPatch is non-public in NoREroMod.dll
        Type t = AccessTools.TypeByName("NoREroMod.UImngPatch");
        return t != null ? AccessTools.Method(t, "WhiteFadeIn") : null;
    }

    [HarmonyPrefix]
    private static bool Prefix()
    {
        try
        {
            GameObject go = GameObject.Find("UIeffect");
            if (go == null)
            {
                // Inactive objects are invisible to Find — recover if black-BG hid the canvas.
                Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
                for (int i = 0; i < all.Length; i++)
                {
                    Transform t = all[i];
                    if (t == null || t.name != "UIeffect")
                        continue;
                    if (!t.gameObject.scene.IsValid())
                        continue;
                    go = t.gameObject;
                    break;
                }

                if (go == null)
                    return false; // Skip broken original — do not NRE mid-OnEvent.

                if (!go.activeSelf)
                    go.SetActive(true);
            }

            if (go.GetComponent<fadein_out>() == null)
                return false;

            return true; // UIeffect is usable — run NoREroMod original.
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[Pregnancy.WhiteFadeIn] null-safe guard: " + ex.Message);
            return false;
        }
    }
}
