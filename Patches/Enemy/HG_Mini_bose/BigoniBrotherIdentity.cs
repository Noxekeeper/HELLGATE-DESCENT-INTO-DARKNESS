using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace NoREroMod.Patches.Enemy;

internal static class BigoniBrotherIdentity
{
    private static readonly HashSet<int> RegisteredBrotherIds = new HashSet<int>();
    private static readonly HashSet<int> ActiveEroInstanceIds = new HashSet<int>();

    private static readonly FieldInfo OyaField = typeof(StartBigoniERO).GetField(
        "oya",
        BindingFlags.NonPublic | BindingFlags.Instance);

    internal static void RegisterBrother(Bigoni bigoni)
    {
        if (bigoni == null || bigoni.gameObject == null)
            return;

        RegisteredBrotherIds.Add(bigoni.gameObject.GetInstanceID());
        EnsureMarker(bigoni);
    }

    internal static void BeginEroSession(StartBigoniERO ero, Bigoni bigoni)
    {
        if (ero != null)
            ActiveEroInstanceIds.Add(ero.GetInstanceID());
        RegisterBrother(bigoni);
    }

    internal static void EndEroSession(StartBigoniERO ero)
    {
        if (ero != null)
            ActiveEroInstanceIds.Remove(ero.GetInstanceID());
    }

    internal static void EnsureMarker(Bigoni bigoni)
    {
        if (bigoni == null || bigoni.gameObject == null)
            return;
        if (bigoni.GetComponent<BigoniBrotherMarker>() != null)
            return;
        if (IsBrother(bigoni))
            bigoni.gameObject.AddComponent<BigoniBrotherMarker>();
    }

    internal static bool TryResolveBigoni(StartBigoniERO ero, out Bigoni bigoni)
    {
        bigoni = null;
        if (ero == null)
            return false;

        bigoni = OyaField?.GetValue(ero) as Bigoni;
        if (bigoni != null)
            return true;

        bigoni = ero.GetComponentInParent<Bigoni>();
        if (bigoni != null)
            return true;

        Transform walk = ero.transform;
        while (walk != null)
        {
            bigoni = walk.GetComponent<Bigoni>();
            if (bigoni != null)
                return true;
            walk = walk.parent;
        }

        return false;
    }

    internal static bool IsBrother(Bigoni bigoni)
    {
        if (bigoni == null || bigoni.gameObject == null)
            return false;

        int id = bigoni.gameObject.GetInstanceID();
        if (RegisteredBrotherIds.Contains(id))
            return true;
        if (bigoni.GetComponent<BigoniBrotherMarker>() != null)
            return true;

        string name = bigoni.gameObject.name;
        return !string.IsNullOrEmpty(name)
               && name.IndexOf("BigoniBrother", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    internal static bool IsBrother(StartBigoniERO ero, out Bigoni bigoni)
    {
        if (!TryResolveBigoni(ero, out bigoni))
            return false;
        return IsBrother(bigoni);
    }

    internal static bool ShouldBypassVanillaGameOver(StartBigoniERO ero, out Bigoni bigoni)
    {
        bigoni = null;
        if (ero == null)
            return false;

        if (ActiveEroInstanceIds.Contains(ero.GetInstanceID()))
        {
            TryResolveBigoni(ero, out bigoni);
            return true;
        }

        return IsBrother(ero, out bigoni);
    }
}
