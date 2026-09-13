using System;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using NoREroMod;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Systems.Spawn.Patches;

/// <summary>
/// When a HellGate spawn is marked ForceElite (|elite=1), replace NoREroMod's random
/// elite roll with a guaranteed elite apply (same &lt;SUPER&gt; + config multipliers).
/// </summary>
internal static class HellGateForceElitePatch
{
    private const string SuperTag = "<SUPER>";
    private static bool applied;

    internal static void Apply(Harmony harmony)
    {
        if (applied || harmony == null)
            return;

        try
        {
            Assembly norAssembly = typeof(StruggleSystem).Assembly;
            Type type = norAssembly.GetType("NoREroMod.EnemyDatePatch");
            if (type == null)
            {
                Plugin.Log?.LogWarning("[SPAWN ELITE] NoREroMod.EnemyDatePatch not found.");
                return;
            }

            MethodInfo spawnMethod = type.GetMethod(
                "SpawnSuperEnemyAndHideHP",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (spawnMethod == null)
            {
                Plugin.Log?.LogWarning("[SPAWN ELITE] SpawnSuperEnemyAndHideHP not found.");
                return;
            }

            MethodInfo prefix = typeof(HellGateForceElitePatch).GetMethod(
                nameof(SpawnSuperEnemyAndHideHP_Prefix),
                BindingFlags.NonPublic | BindingFlags.Static);
            if (prefix == null)
                return;

            harmony.Patch(spawnMethod, prefix: new HarmonyMethod(prefix) { priority = Priority.First });
            applied = true;
            Plugin.Log?.LogInfo("[SPAWN ELITE] Force-elite prefix on SpawnSuperEnemyAndHideHP.");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[SPAWN ELITE] Patch failed: " + ex.Message);
        }
    }

    internal static bool HasForceElite(EnemyDate enemy)
    {
        if (enemy == null)
            return false;
        SpawnManagedInstance managed = enemy.GetComponent<SpawnManagedInstance>()
            ?? enemy.GetComponentInParent<SpawnManagedInstance>();
        return managed != null && managed.ForceElite;
    }

    private static bool SpawnSuperEnemyAndHideHP_Prefix(
        EnemyDate __instance,
        SkeletonAnimation ___mySpine,
        ref string ___JPname)
    {
        if (!HasForceElite(__instance))
            return true;

        ApplyForcedElite(__instance, ___mySpine, ref ___JPname);
        ApplyHiddenHpBars(__instance);
        return false;
    }

    private static void ApplyForcedElite(EnemyDate enemy, SkeletonAnimation spine, ref string jpName)
    {
        if (enemy == null)
            return;

        if (string.IsNullOrEmpty(jpName) || jpName.IndexOf(SuperTag, StringComparison.Ordinal) < 0)
            jpName = (jpName ?? string.Empty) + SuperTag;

        float hpMin = ReadConfigFloat("eliteHPMultiMin", 2.6f);
        float hpMax = ReadConfigFloat("eliteHPMultiMax", 3.4f);
        if (hpMax < hpMin)
        {
            float t = hpMin;
            hpMin = hpMax;
            hpMax = t;
        }

        float hpMulti = UnityEngine.Random.Range(hpMin, hpMax);
        enemy.MaxHp *= hpMulti;
        enemy.Hp = enemy.MaxHp;
        enemy.Exp = Mathf.RoundToInt(enemy.Exp * ReadConfigFloat("eliteEXPMulti", 4f));
        enemy.enmMovespeed *= ReadConfigFloat("eliteSpeedMulti", 1.3f);
        enemy.enmMAXtough *= ReadConfigFloat("elitePoiseMulti", 2f);

        string colorHtml = ReadConfigString("eliteColor", "#FF0000");
        if (spine != null && spine.skeleton != null &&
            ColorUtility.TryParseHtmlString(colorHtml, out Color eliteColor))
        {
            spine.skeleton.SetColor(eliteColor);
        }
    }

    private static void ApplyHiddenHpBars(EnemyDate enemy)
    {
        if (enemy == null || !ReadConfigBool("hiddenHPBars", false))
            return;

        try
        {
            Transform canvas = enemy.gameObject.transform.Find("Canvas/Hp");
            if (canvas != null)
                canvas.gameObject.SetActive(false);
            Transform back = enemy.gameObject.transform.Find("Canvas/Hpback");
            if (back != null)
                back.gameObject.SetActive(false);
        }
        catch
        {
        }
    }

    private static Type norPluginType;

    private static Type GetNorPluginType()
    {
        if (norPluginType != null)
            return norPluginType;
        norPluginType = Type.GetType("NoREroMod.Plugin, NoREroMod");
        return norPluginType;
    }

    private static object GetConfigEntry(string fieldName)
    {
        Type t = GetNorPluginType();
        if (t == null)
            return null;
        FieldInfo field = t.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
        return field?.GetValue(null);
    }

    private static float ReadConfigFloat(string fieldName, float fallback)
    {
        try
        {
            object entry = GetConfigEntry(fieldName);
            if (entry is ConfigEntry<float> f)
                return f.Value;
        }
        catch
        {
        }

        return fallback;
    }

    private static string ReadConfigString(string fieldName, string fallback)
    {
        try
        {
            object entry = GetConfigEntry(fieldName);
            if (entry is ConfigEntry<string> s)
                return s.Value ?? fallback;
        }
        catch
        {
        }

        return fallback;
    }

    private static bool ReadConfigBool(string fieldName, bool fallback)
    {
        try
        {
            object entry = GetConfigEntry(fieldName);
            if (entry is ConfigEntry<bool> b)
                return b.Value;
        }
        catch
        {
        }

        return fallback;
    }
}
