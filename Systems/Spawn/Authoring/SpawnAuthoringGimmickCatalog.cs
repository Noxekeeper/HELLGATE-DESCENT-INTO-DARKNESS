using System;
using System.Collections;
using System.Collections.Generic;
using NoREroMod.Systems.Spawn.Gimmicks;
using UnityEngine;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// F11 Gimmicks picker: gates, folding ladders and the standalone lever.
/// One key per cached template; scene-suffixed duplicates and vanilla script aliases are hidden.
/// </summary>
internal static class SpawnAuthoringGimmickCatalog
{
    private static readonly string[] BuiltInKeys =
    {
        "gate_fence",
        "gate_fence_pilgrimageentrance",
        "ladder_nomal",
        "ladder_nomal_pilgrimageentrance",
        "ladder2",
        HellGateGimmickRuntime.LeverOnlyKey
    };

    /// <summary>Vanilla script-name keys; they spawn the same assemblies as the skeleton keys.</summary>
    private static readonly string[] AliasKeys =
    {
        "dooropen",
        "ladder_on",
        "gimmickladder_on"
    };

    private static bool hydrating;

    internal static string[] GetKeys()
    {
        var keys = new List<string>();
        var seenTemplates = new HashSet<int>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < BuiltInKeys.Length; i++)
        {
            string key = BuiltInKeys[i];
            keys.Add(key);
            seenKeys.Add(key);
            if (IsLeverKey(key))
                continue;
            int id = SpawnTemplateCatalog.GetTemplateInstanceId(key);
            if (id != 0)
                seenTemplates.Add(id);
        }

        var cached = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        SpawnTemplateCatalog.CollectAuthoringGimmickKeys(cached);
        var hiddenRoots = new List<string>(keys);
        hiddenRoots.AddRange(AliasKeys);
        var extras = new List<string>();
        foreach (var kv in cached)
        {
            if (seenKeys.Contains(kv.Key) || IsAliasKey(kv.Key) || IsSceneVariantOf(kv.Key, hiddenRoots))
                continue;
            if (!seenTemplates.Add(kv.Value))
                continue;
            extras.Add(kv.Key);
        }

        extras.Sort(StringComparer.OrdinalIgnoreCase);
        keys.AddRange(extras);
        return keys.ToArray();
    }

    internal static bool IsPickerKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;
        key = key.Trim();
        for (int i = 0; i < BuiltInKeys.Length; i++)
        {
            if (string.Equals(BuiltInKeys[i], key, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return IsAliasKey(key);
    }

    internal static bool IsReady(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;
        return SpawnTemplateCatalog.HasTemplate(HellGateGimmickRuntime.ResolveTemplateKey(key, out _));
    }

    /// <summary>Loads source maps for built-in keys that are not cached yet. Returns false when nothing is missing.</summary>
    internal static bool TryStartHydrate(Action onDone)
    {
        if (hydrating || Plugin.Instance == null)
            return false;

        var missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < BuiltInKeys.Length; i++)
        {
            string templateKey = HellGateGimmickRuntime.ResolveTemplateKey(BuiltInKeys[i], out _);
            if (!SpawnTemplateCatalog.HasTemplate(templateKey))
                missing.Add(templateKey);
        }

        if (missing.Count == 0)
            return false;

        hydrating = true;
        Plugin.Instance.StartCoroutine(HydrateRoutine(missing, onDone));
        return true;
    }

    private static IEnumerator HydrateRoutine(HashSet<string> keys, Action onDone)
    {
        try
        {
            yield return SpawnTemplateDiskCache.HydrateKeys(keys, "F11 Gimmicks");
        }
        finally
        {
            hydrating = false;
        }

        onDone?.Invoke();
    }

    private static bool IsLeverKey(string key)
    {
        return string.Equals(key, HellGateGimmickRuntime.LeverOnlyKey, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Picker label; the pack line keeps the raw key.</summary>
    internal static string GetDisplayName(string key)
    {
        if (string.IsNullOrEmpty(key))
            return key;
        string locKey = "gimmick.name." + key.Trim().ToLowerInvariant();
        string name = SpawnAuthoringLoc.T(locKey);
        return string.Equals(name, locKey, StringComparison.Ordinal) ? key : name;
    }

    private static bool IsAliasKey(string key)
    {
        string compact = Compact(key);
        for (int i = 0; i < AliasKeys.Length; i++)
        {
            if (string.Equals(Compact(AliasKeys[i]), compact, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>Also catches compact aliases (<c>gatefenceundergroundchurch</c>) of scene-suffixed keys.</summary>
    private static bool IsSceneVariantOf(string key, List<string> listed)
    {
        string compact = Compact(key);
        for (int i = 0; i < listed.Count; i++)
        {
            if (compact.StartsWith(Compact(listed[i]), StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static string Compact(string key)
    {
        return key.Trim().Replace("_", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
    }
}
