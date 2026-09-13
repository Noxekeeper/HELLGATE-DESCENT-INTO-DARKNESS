using System.Collections.Generic;
using UnityEngine;

namespace NoREroMod.Patches.HellTraps;

/// <summary>Tracks lethal lightning button trap instance roots by Unity instance id.</summary>
internal static class LethalLightningTrapRegistry
{
    private static readonly HashSet<int> _rootIds = new HashSet<int>();

    internal static void Register(GameObject root)
    {
        if (root == null)
            return;

        _rootIds.Add(root.GetInstanceID());

        Trap_button button = root.GetComponent<Trap_button>();
        if (button != null)
            _rootIds.Add(button.gameObject.GetInstanceID());

        Trap_button[] children = root.GetComponentsInChildren<Trap_button>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Trap_button child = children[i];
            if (child != null)
                _rootIds.Add(child.gameObject.GetInstanceID());
        }
    }

    internal static void Unregister(GameObject root)
    {
        if (root == null)
            return;

        _rootIds.Remove(root.GetInstanceID());

        Trap_button[] children = root.GetComponentsInChildren<Trap_button>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Trap_button child = children[i];
            if (child != null)
                _rootIds.Remove(child.gameObject.GetInstanceID());
        }
    }

    internal static bool IsLethalLightningTrap(Component component)
    {
        if (component == null)
            return false;

        Transform node = component.transform;
        while (node != null)
        {
            if (_rootIds.Contains(node.gameObject.GetInstanceID()))
                return true;

            if (node.GetComponent<HellGateLethalLightningTrapMarker>() != null)
                return true;

            if (node.GetComponent<HellGateLethalLightningTrapTracker>() != null)
                return true;

            if (node.GetComponent<HellGateLethalLightningTrapArmer>() != null)
                return true;

            string name = node.gameObject.name;
            if (!string.IsNullOrEmpty(name) &&
                name.IndexOf("LethalLightning", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            node = node.parent;
        }

        return false;
    }
}
