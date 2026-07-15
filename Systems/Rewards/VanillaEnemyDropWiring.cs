using System.Reflection;
using UnityEngine;
using NoREroMod.Systems.Spawn;

namespace NoREroMod.Systems.Rewards;

/// <summary>
/// Wires <see cref="EnemyDate"/>'s serialized <c>Drop</c> reference the same way the game data does for normal enemies
/// (shared pickup prefab assigned in the inspector on goblin, undead, etc.).
/// Slime prefabs often leave <c>Drop</c> empty because vanilla code never spawns loot from it.
/// </summary>
internal static class VanillaEnemyDropWiring
{
    private static readonly FieldInfo s_dropField = typeof(EnemyDate).GetField(
        "Drop",
        BindingFlags.Instance | BindingFlags.NonPublic);

    private static GameObject s_borrowedDropPrefab;
    private static bool s_loggedBorrow;
    private static bool s_loggedAssign;

    private static GameObject ReadDrop(EnemyDate enemy)
    {
        if (enemy == null || s_dropField == null)
            return null;
        try
        {
            return s_dropField.GetValue(enemy) as GameObject;
        }
        catch
        {
            return null;
        }
    }

    private static void WriteDrop(EnemyDate enemy, GameObject value)
    {
        if (enemy == null || s_dropField == null)
            return;
        s_dropField.SetValue(enemy, value);
    }

    /// <summary>
    /// If <paramref name="enemy"/> has no <c>Drop</c>, assigns the same prefab reference taken from a vanilla enemy prefab in <see cref="EnemyPrefabRegistry"/>.
    /// </summary>
    internal static void EnsureEnemyDateDropReference(EnemyDate enemy)
    {
        if (enemy == null)
            return;

        if (ReadDrop(enemy) != null)
            return;

        GameObject borrowed = ResolveBorrowedDropPrefab();
        if (borrowed == null)
        {
            if (!s_loggedBorrow)
            {
                s_loggedBorrow = true;
                Plugin.Log?.LogWarning(
                    "[drop-wiring] No EnemyDate.Drop found on registry prefabs or live scene enemies; biscord cannot Instantiate(Drop) like vanilla.");
            }
            return;
        }

        try
        {
            WriteDrop(enemy, borrowed);
            if (!s_loggedAssign)
            {
                s_loggedAssign = true;
                Plugin.Log?.LogInfo("[drop-wiring] Assigned EnemyDate.Drop (vanilla loot pipeline).");
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogWarning($"[drop-wiring] Failed to set EnemyDate.Drop: {ex.Message}");
        }
    }

    private static GameObject ResolveBorrowedDropPrefab()
    {
        if (s_borrowedDropPrefab != null)
            return s_borrowedDropPrefab;

        // Registry keys from HellGate spawn table / ALL_ENEMIES; order is "likely to have Drop wired".
        string[] keys =
        {
            "Goblin", "Undead", "TouzokuNormal", "Kinoko", "Kakasi", "Snailshell",
            "Mutude", "Vagrant", "PrisonOfficer", "Slaughterer", "GobBigAlter"
        };

        foreach (string key in keys)
        {
            if (!EnemyPrefabRegistry.TryGetPrefab(key, out GameObject prefab) || prefab == null)
                continue;

            EnemyDate root = prefab.GetComponent<EnemyDate>();
            if (root == null)
                continue;

            GameObject drop = ReadDrop(root);
            if (drop != null)
            {
                s_borrowedDropPrefab = drop;
                return s_borrowedDropPrefab;
            }
        }

        // Prefab assets sometimes ship without serialized references; copy Drop from any live enemy in the scene.
        GameObject live = TryBorrowDropFromScene();
        if (live != null)
            s_borrowedDropPrefab = live;

        return s_borrowedDropPrefab;
    }

    private static GameObject TryBorrowDropFromScene()
    {
        // Concrete enemy types that use EnemyDate.Drop in vanilla scripts.
        System.Type[] types =
        {
            typeof(goblin), typeof(Undead), typeof(TouzokuNormal), typeof(Snailshell), typeof(Vagrant)
        };

        for (int t = 0; t < types.Length; t++)
        {
            UnityEngine.Object[] found = UnityEngine.Object.FindObjectsOfType(types[t]);
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] is not EnemyDate ed)
                    continue;
                GameObject drop = ReadDrop(ed);
                if (drop != null)
                    return drop;
            }
        }

        return null;
    }
}
