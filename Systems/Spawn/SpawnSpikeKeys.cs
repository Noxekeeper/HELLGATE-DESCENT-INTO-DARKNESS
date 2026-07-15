using System;

namespace NoREroMod.Systems.Spawn;

/// <summary>Known spike / damage-trap spawn keys that support TRAP-line Z rotation (rot90/180/270).</summary>
internal static class SpawnSpikeKeys
{
    /// <summary>Static floor/wall spikes (TrapNormal / Trap_hari).</summary>
    internal static readonly string[] TrapHariKeys =
    {
        "trap",
        "trapnormal",
        "trap_hari",
    };

    /// <summary>Light impact spike prefab (ImpactDamage on LightImpactNormal).</summary>
    internal static readonly string[] LightImpactKeys =
    {
        "impactdamage",
        "lightimpactnormal",
    };

    /// <summary>Standalone damage collider bodies (often wall/ceiling spikes).</summary>
    internal static readonly string[] ImpactBoxKeys =
    {
        "impactdamagebox",
    };

    /// <summary>Animated wave spikes (Spine; cache from visited scenes).</summary>
    internal static readonly string[] WaveSpikeKeys =
    {
        "wavespike",
        "wavespikeguard",
    };

    /// <summary>Rotating spear launcher (Valley); rot adjusts mount + fire rotation.</summary>
    internal static readonly string[] SpearLauncherKeys =
    {
        "spear",
        "spearthrowtrap",
    };

    internal static bool IsTrapHariKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;

        return ContainsKey(TrapHariKeys, SpawnTemplateCatalog.NormalizeTemplateKey(key));
    }

    internal static bool IsSpikeLikeKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;

        string normalized = SpawnTemplateCatalog.NormalizeTemplateKey(key);
        return ContainsKey(TrapHariKeys, normalized) ||
               ContainsKey(LightImpactKeys, normalized) ||
               ContainsKey(ImpactBoxKeys, normalized) ||
               ContainsKey(WaveSpikeKeys, normalized) ||
               ContainsKey(SpearLauncherKeys, normalized);
    }

    private static bool ContainsKey(string[] keys, string normalized)
    {
        for (int i = 0; i < keys.Length; i++)
        {
            if (string.Equals(keys[i], normalized, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
