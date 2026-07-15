using System;
using UnityEngine;

namespace NoREroMod.Systems.Spawn;

/// <summary>Z-axis rotation for template spawns (spike traps, static damage props).</summary>
internal static class SpawnRotationUtility
{
    internal static bool TryParseRotationToken(string token, out float rotationZ)
    {
        rotationZ = 0f;
        if (string.IsNullOrEmpty(token))
            return false;

        token = token.Trim();
        if (token.Length == 0)
            return false;

        if (string.Equals(token, "rot0", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "norot", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(token, "rot90", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "90", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "left90", StringComparison.OrdinalIgnoreCase))
        {
            rotationZ = 90f;
            return true;
        }

        if (string.Equals(token, "rot270", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "270", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "right90", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "-90", StringComparison.OrdinalIgnoreCase))
        {
            rotationZ = 270f;
            return true;
        }

        if (string.Equals(token, "rot180", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "180", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "upside", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "upsidedown", StringComparison.OrdinalIgnoreCase))
        {
            rotationZ = 180f;
            return true;
        }

        if (token.StartsWith("rot", StringComparison.OrdinalIgnoreCase) &&
            float.TryParse(token.Substring(3), out float custom))
        {
            rotationZ = NormalizeAngle(custom);
            return true;
        }

        return false;
    }

    internal static float NormalizeAngle(float degrees)
    {
        float wrapped = degrees % 360f;
        if (wrapped < 0f)
            wrapped += 360f;
        return wrapped;
    }

    internal static void ApplyRotation(GameObject root, float rotationZ)
    {
        if (root == null)
            return;

        float angle = NormalizeAngle(rotationZ);
        if (Mathf.Abs(angle) < 0.001f)
            return;

        root.transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }
}