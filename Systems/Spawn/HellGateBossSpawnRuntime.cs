using System;
using System.Reflection;
using UnityEngine;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// Skips vanilla boss intro gates (BOSSflag / BattleStart) for HellGate coordinate spawns.
/// </summary>
internal static class HellGateBossSpawnRuntime
{
    private static readonly FieldInfo BossTouzokuBossFlagField =
        typeof(BossTouzoku).GetField("BOSSflag", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo BossTouzokuMeshField =
        typeof(BossTouzoku).GetField("myspinerennder", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo BossTouzokuHpUiField =
        typeof(BossTouzoku).GetField("HPUI", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo BossTouzokuPro2dField =
        typeof(BossTouzoku).GetField("pro2d", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo BossTouzokuXWeaponField =
        typeof(BossTouzoku).GetField("xweapon_comp", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly MethodInfo BossTouzokuBattleStartMethod =
        typeof(BossTouzoku).GetMethod("BattleStart", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly MethodInfo BossTouzokuStateMethod =
        typeof(BossTouzoku).GetMethod("State", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly MethodInfo BossTouzokuSousaMethod =
        typeof(BossTouzoku).GetMethod("sousa", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly Type BossTouzokuStateEnum =
        typeof(BossTouzoku).GetNestedType("enemystate", BindingFlags.NonPublic);

    internal static void ConfigureSpawnedBossIfNeeded(GameObject root)
    {
        if (root == null)
            return;

        if (root.GetComponent<BossTouzoku>() != null &&
            root.GetComponent<HellGateBossSpawnBootstrap>() == null &&
            (root.name == null || root.name.IndexOf("BossTouzokuCustom", StringComparison.OrdinalIgnoreCase) < 0))
        {
            root.AddComponent<HellGateBossSpawnBootstrap>();
        }
    }

    internal static void TryActivateBossTouzoku(BossTouzoku boss)
    {
        if (boss == null)
            return;

        try
        {
            if (BossTouzokuBattleStartMethod != null)
            {
                BossTouzokuBattleStartMethod.Invoke(boss, null);
                return;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning(
                $"[BOSS SPAWN] BossTouzoku BattleStart failed: {FormatException(ex)}. Using fallback.");
        }

        TryActivateBossTouzokuFallback(boss);
    }

    private static void TryActivateBossTouzokuFallback(BossTouzoku boss)
    {
        try
        {
            MeshRenderer mesh = BossTouzokuMeshField?.GetValue(boss) as MeshRenderer;
            if (mesh != null)
                mesh.enabled = true;

            Behaviour hpUi = BossTouzokuHpUiField?.GetValue(boss) as Behaviour;
            if (hpUi != null)
                hpUi.enabled = true;

            if (BossTouzokuPro2dField != null)
            {
                object pro2d = BossTouzokuPro2dField.GetValue(boss);
                if (pro2d != null)
                {
                    FieldInfo offsetField = pro2d.GetType().GetField("OverallOffset");
                    if (offsetField != null)
                    {
                        Vector2 offset = (Vector2)offsetField.GetValue(pro2d);
                        offset.x = 0f;
                        offsetField.SetValue(pro2d, offset);
                    }
                }
            }

            if (BossTouzokuSousaMethod != null)
                BossTouzokuSousaMethod.Invoke(boss, null);

            if (BossTouzokuStateMethod != null && BossTouzokuStateEnum != null)
            {
                object idleState = Enum.Parse(BossTouzokuStateEnum, "IDLE");
                BossTouzokuStateMethod.Invoke(boss, new[] { idleState });
            }

            if (BossTouzokuBossFlagField != null)
                BossTouzokuBossFlagField.SetValue(boss, true);

            Array weapons = BossTouzokuXWeaponField?.GetValue(boss) as Array;
            if (weapons != null && weapons.Length > 0 && weapons.GetValue(0) != null)
            {
                MethodInfo activate = weapons.GetValue(0).GetType().GetMethod("Activate");
                activate?.Invoke(weapons.GetValue(0), null);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[BOSS SPAWN] BossTouzoku fallback failed: {FormatException(ex)}");
        }
    }

    private static string FormatException(Exception ex)
    {
        if (ex == null)
            return string.Empty;

        if (ex.InnerException != null)
            return ex.Message + " -> " + ex.InnerException.Message;

        return ex.Message;
    }
}
