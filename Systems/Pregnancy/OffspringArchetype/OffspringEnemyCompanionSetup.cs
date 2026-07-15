using System;
using System.Reflection;
using NoREroMod.Systems.Pregnancy.Patches;
using UnityEngine;

namespace NoREroMod.Systems.Pregnancy.OffspringArchetype;

/// <summary>Idle / display AI setup for offspring on any enemy prefab.</summary>
internal static class OffspringEnemyCompanionSetup
{
    internal static void ApplyCompanionAi(GameObject obj, bool hideoutCompanion)
    {
        if (obj == null)
            return;

        Mafiamuscle mafia = obj.GetComponent<Mafiamuscle>();
        if (mafia != null)
        {
            mafia.enabled = hideoutCompanion;
            if (hideoutCompanion)
            {
                try { mafia.state = Mafiamuscle.enemystate.IDLE; } catch { }
            }

            return;
        }

        ApplyGenericEnemyAi(obj, hideoutCompanion);
    }

    private static void ApplyGenericEnemyAi(GameObject obj, bool enableAi)
    {
        MonoBehaviour[] behaviours = obj.GetComponents<MonoBehaviour>();
        bool setIdleOnPrimary = false;

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour mb = behaviours[i];
            if (!IsEnemyAiBehaviour(mb))
                continue;

            Type type = mb.GetType();
            FieldInfo stateField = type.GetField(
                "state",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (stateField == null || !stateField.FieldType.IsEnum)
                continue;

            mb.enabled = enableAi ? !setIdleOnPrimary : false;
            if (!enableAi)
                continue;

            if (!setIdleOnPrimary)
            {
                TrySetIdleState(stateField, mb);
                setIdleOnPrimary = true;
            }
        }
    }

    private static bool IsEnemyAiBehaviour(MonoBehaviour mb)
    {
        if (mb == null)
            return false;

        if (mb is WitchOffspringController || mb is WitchOffspringSpawnSetup)
            return false;

        Type type = mb.GetType();
        if (type.Namespace != null && type.Namespace.StartsWith("UnityEngine", StringComparison.Ordinal))
            return false;
        if (type.Namespace != null && type.Namespace.StartsWith("Spine", StringComparison.Ordinal))
            return false;

        return true;
    }

    private static void TrySetIdleState(FieldInfo stateField, MonoBehaviour mb)
    {
        try
        {
            object idle;
            try
            {
                idle = Enum.Parse(stateField.FieldType, "IDLE", true);
            }
            catch
            {
                idle = Enum.ToObject(stateField.FieldType, 0);
            }

            stateField.SetValue(mb, idle);
        }
        catch { }
    }
}
