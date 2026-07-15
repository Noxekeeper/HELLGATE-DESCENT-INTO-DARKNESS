using System.Collections.Generic;
using UnityEngine;

namespace NoREroMod.Patches.HellTraps;

/// <summary>Tracks lethal cocoon trap instance roots by Unity instance id.</summary>
internal static class LethalCocoonTrapRegistry
{
    private static readonly HashSet<int> _rootIds = new HashSet<int>();

    internal static void Register(GameObject root)
    {
        if (root == null)
            return;

        _rootIds.Add(root.GetInstanceID());

        Cocoontrap trap = root.GetComponent<Cocoontrap>();
        if (trap != null)
            _rootIds.Add(trap.gameObject.GetInstanceID());

        Cocoontrap[] children = root.GetComponentsInChildren<Cocoontrap>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Cocoontrap child = children[i];
            if (child != null)
                _rootIds.Add(child.gameObject.GetInstanceID());
        }
    }

    internal static void Unregister(GameObject root)
    {
        if (root == null)
            return;

        _rootIds.Remove(root.GetInstanceID());

        Cocoontrap[] children = root.GetComponentsInChildren<Cocoontrap>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Cocoontrap child = children[i];
            if (child != null)
                _rootIds.Remove(child.gameObject.GetInstanceID());
        }
    }

    internal static bool IsLethalCocoonTrap(Component component)
    {
        if (component == null)
            return false;

        Transform node = component.transform;
        while (node != null)
        {
            if (_rootIds.Contains(node.gameObject.GetInstanceID()))
                return true;

            if (node.GetComponent<HellGateLethalCocoonTrapMarker>() != null)
                return true;

            if (node.GetComponent<HellGateLethalCocoonTrapTracker>() != null)
                return true;

            string name = node.gameObject.name;
            if (!string.IsNullOrEmpty(name) &&
                name.IndexOf("LethalCocoon", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            node = node.parent;
        }

        return false;
    }
}
