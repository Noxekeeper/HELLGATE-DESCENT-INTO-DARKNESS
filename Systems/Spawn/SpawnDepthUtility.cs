using System;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace NoREroMod.Systems.Spawn;

/// <summary>World position + 2D sorting overrides for template spawns.</summary>
internal struct SpawnDepthSettings
{
    public float WorldZOffset;
    public float WorldYOffset;
    public int SortingOrderOffset;
    public string SortingLayerName;
    public bool MatchPlayerSortingLayer;

    public static SpawnDepthSettings Empty => default;

    public bool HasPositionOffset =>
        Mathf.Abs(WorldZOffset) > 0.0001f || Mathf.Abs(WorldYOffset) > 0.0001f;

    public bool HasSortingOverride =>
        SortingOrderOffset != 0 ||
        !string.IsNullOrEmpty(SortingLayerName) ||
        MatchPlayerSortingLayer;

    public void Merge(in SpawnDepthSettings other)
    {
        WorldZOffset += other.WorldZOffset;
        WorldYOffset += other.WorldYOffset;
        SortingOrderOffset += other.SortingOrderOffset;
        if (!string.IsNullOrEmpty(other.SortingLayerName))
            SortingLayerName = other.SortingLayerName;
        if (other.MatchPlayerSortingLayer)
            MatchPlayerSortingLayer = true;
    }
}

/// <summary>World Z/Y and sorting-layer/order offsets for template spawns.</summary>
internal static class SpawnDepthUtility
{
    private static readonly PropertyInfo SortingLayerNameProperty =
        typeof(sorting).GetProperty("LayerName", BindingFlags.Instance | BindingFlags.Public);
    private static readonly PropertyInfo SortingOrderProperty =
        typeof(sorting).GetProperty("OrderInLayer", BindingFlags.Instance | BindingFlags.Public);

    internal static bool TryParseDepthToken(string token, out SpawnDepthSettings settings)
    {
        settings = SpawnDepthSettings.Empty;
        if (string.IsNullOrEmpty(token))
            return false;

        token = token.Trim();
        if (token.Length == 0)
            return false;

        if (string.Equals(token, "near", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "front", StringComparison.OrdinalIgnoreCase))
        {
            settings.SortingOrderOffset = 10;
            settings.MatchPlayerSortingLayer = true;
            return true;
        }

        if (string.Equals(token, "far", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "back", StringComparison.OrdinalIgnoreCase))
        {
            settings.SortingOrderOffset = -10;
            return true;
        }

        if (string.Equals(token, "layer:player", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "slayer:player", StringComparison.OrdinalIgnoreCase))
        {
            settings.MatchPlayerSortingLayer = true;
            return true;
        }

        if (token.StartsWith("layer:", StringComparison.OrdinalIgnoreCase))
        {
            string layer = token.Substring("layer:".Length).Trim();
            if (layer.Length == 0)
                return false;
            settings.SortingLayerName = layer;
            return true;
        }

        if (TryParseSignedFloatToken(token, "z", out float zDelta))
        {
            settings.WorldZOffset = zDelta;
            return true;
        }

        if (TryParseSignedFloatToken(token, "y", out float yDelta))
        {
            settings.WorldYOffset = yDelta;
            return true;
        }

        if (TryParseSignedIntToken(token, "sort", out int sortDelta))
        {
            settings.SortingOrderOffset = sortDelta;
            return true;
        }

        return false;
    }

    internal static void ApplyDepth(GameObject root, in SpawnDepthSettings settings)
    {
        if (root == null)
            return;

        if (settings.HasPositionOffset)
        {
            Vector3 pos = root.transform.position;
            root.transform.position = new Vector3(
                pos.x,
                pos.y + settings.WorldYOffset,
                pos.z + settings.WorldZOffset);
        }

        if (!settings.HasSortingOverride)
            return;

        string layerName = settings.SortingLayerName;
        int baseOrder = 0;
        if (settings.MatchPlayerSortingLayer && TryGetPlayerRenderer(out Renderer playerRenderer))
        {
            layerName = playerRenderer.sortingLayerName;
            baseOrder = playerRenderer.sortingOrder;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            if (!string.IsNullOrEmpty(layerName))
                renderer.sortingLayerName = layerName;

            if (settings.MatchPlayerSortingLayer)
                renderer.sortingOrder = baseOrder + settings.SortingOrderOffset;
            else if (settings.SortingOrderOffset != 0)
                renderer.sortingOrder += settings.SortingOrderOffset;
        }

        SyncVanillaSortingComponents(root, layerName, settings);
    }

    internal static bool TryGetPlayerRenderer(out Renderer renderer)
    {
        renderer = null;
        try
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player == null)
                return false;

            renderer = player.GetComponentInChildren<Renderer>(true);
            return renderer != null;
        }
        catch
        {
            return false;
        }
    }

    internal static bool TryGetPlayerWorldZ(out float worldZ)
    {
        worldZ = 0f;
        try
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player == null)
                return false;

            worldZ = player.transform.position.z;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Floor Trap_hari from another scene: match player depth plane + sorting layer.</summary>
    internal static void ApplyTrapHariFloorDefaults(string key, float rotationZ, ref float spawnWorldZ, ref SpawnDepthSettings depth)
    {
        if (!SpawnSpikeKeys.IsTrapHariKey(key))
            return;

        float normalized = SpawnRotationUtility.NormalizeAngle(rotationZ);
        if (Mathf.Abs(normalized) > 0.001f && Mathf.Abs(normalized - 180f) > 0.001f)
            return;

        if (TryGetPlayerWorldZ(out float playerZ))
            spawnWorldZ = playerZ;

        depth.MatchPlayerSortingLayer = true;
        if (depth.SortingOrderOffset == 0)
            depth.SortingOrderOffset = 2;
    }

    private static void SyncVanillaSortingComponents(GameObject root, string layerName, in SpawnDepthSettings settings)
    {
        sorting[] sortingComponents = root.GetComponentsInChildren<sorting>(true);
        for (int i = 0; i < sortingComponents.Length; i++)
        {
            sorting sortingComponent = sortingComponents[i];
            if (sortingComponent == null)
                continue;

            Renderer renderer = sortingComponent.GetComponent<Renderer>();
            if (renderer == null)
                continue;

            if (!string.IsNullOrEmpty(layerName) && SortingLayerNameProperty != null)
                SortingLayerNameProperty.SetValue(sortingComponent, layerName, null);

            if (SortingOrderProperty != null)
                SortingOrderProperty.SetValue(sortingComponent, renderer.sortingOrder, null);
        }
    }

    private static bool TryParseSignedFloatToken(string token, string prefix, out float value)
    {
        value = 0f;
        if (!token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        string number = token.Substring(prefix.Length);
        if (number.Length == 0)
            return false;

        if (number[0] != '+' && number[0] != '-' && !char.IsDigit(number[0]))
            return false;

        return float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseSignedIntToken(string token, string prefix, out int value)
    {
        value = 0;
        if (!token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        string number = token.Substring(prefix.Length);
        if (number.Length == 0)
            return false;

        if (number[0] != '+' && number[0] != '-' && !char.IsDigit(number[0]))
            return false;

        return int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
