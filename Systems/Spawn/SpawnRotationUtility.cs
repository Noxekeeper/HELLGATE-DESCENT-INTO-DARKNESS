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

    internal static bool IsWallMountAngle(float rotationZ)
    {
        float n = NormalizeAngle(rotationZ);
        float to90 = Mathf.Abs(Mathf.DeltaAngle(n, 90f));
        float to270 = Mathf.Abs(Mathf.DeltaAngle(n, 270f));
        return to90 <= 45f || to270 <= 45f;
    }

    internal static void ApplyRotation(GameObject root, float rotationZ)
    {
        ApplyAuthoringRotation(root, rotationZ);
    }

    /// <summary>
    /// Set Z rotation on the root without moving XY.
    /// F11 pause uses timeScale 0, so Rigidbody2D.MoveRotation is skipped —
    /// it would not apply and later fight the transform.
    /// </summary>
    internal static void ApplyAuthoringRotation(GameObject root, float rotationZ)
    {
        if (root == null)
            return;

        float angle = NormalizeAngle(rotationZ);
        root.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        Rigidbody2D body = root.GetComponent<Rigidbody2D>();
        if (body == null)
            return;

        body.angularVelocity = 0f;
        body.velocity = Vector2.zero;
        body.rotation = angle;
    }

    /// <summary>
    /// Keep pack Z rotation after Trapdata/Start resets euler (same idea as SpawnFixedFacing).
    /// </summary>
    internal static void LockAuthoringRotation(GameObject root, float rotationZ)
    {
        if (root == null)
            return;

        float angle = NormalizeAngle(rotationZ);
        ApplyAuthoringRotation(root, angle);

        SpawnFixedRotation hold = root.GetComponent<SpawnFixedRotation>();
        if (hold == null)
            hold = root.AddComponent<SpawnFixedRotation>();
        hold.FixedZ = angle;
    }

    internal static float ReadRotationZ(GameObject root)
    {
        if (root == null)
            return 0f;

        Rigidbody2D body = root.GetComponent<Rigidbody2D>();
        if (body != null)
            return NormalizeAngle(body.rotation);

        return NormalizeAngle(root.transform.eulerAngles.z);
    }
}
