using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Systems.CombatAi.Factions;

/// <summary>
/// Detects vanilla story bosses so faction "peaceful" reputation never freezes their AI.
/// Primary signal: private <c>BOSSflag</c> field on the enemy class (see Assembly-CSharp decompile).
/// </summary>
internal static class FactionBossDetection
{
    private static readonly Dictionary<Type, bool> _structuralBossByType = new Dictionary<Type, bool>();
    private static readonly Dictionary<Type, FieldInfo> _bossFlagFieldByType = new Dictionary<Type, FieldInfo>();

    /// <summary>
    /// Vanilla boss types confirmed via decompiled Assembly-CSharp (private bool BOSSflag):
    /// BossTouzoku, BOSS_Village, BossScapegoatentrance, BossInsomniaUnder, Boss_Ranch,
    /// Candore, SuccubusSpine, OriginIbaranoMajyo, LastIbaranoMajyo, Praymaiden, DemonRequiemKnight.
    /// BossLeftinsomniaUnder / BossRightinsomniaUnder are parts of BossInsomniaUnder (Boss* prefix).
    /// RequiemKnight has no BOSSflag — treated as elite mob, not a story boss.
    /// MafiaBossCustom has no BOSSflag — normal mafia grunt.
    /// </summary>
    public static bool IsBossEnemy(EnemyDate enemy)
    {
        if (enemy == null || enemy.gameObject == null)
            return false;

        string objectName = enemy.gameObject.name;
        if (!string.IsNullOrEmpty(objectName) &&
            objectName.IndexOf("BossTouzokuCustom", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return false;
        }

        string typeName = enemy.GetType().Name;
        if (IsExplicitlyExcluded(typeName))
            return false;
        // Field mini-bosses share vanilla AI and must always hunt the player; faction
        // redirect/passive would send them into nearby monsters (Rosewarm, Ivy) instead.
        if (typeName.Equals("Bigoni", StringComparison.Ordinal))
            return true;
        if (IsExplicitlyIncluded(typeName))
            return true;
        if (HasVanillaBossNamePrefix(typeName))
            return true;
        if (HasStructuralBossMarker(enemy.GetType()))
            return true;
        if (TryReadBossFlagValue(enemy))
            return true;

        return false;
    }

    public static bool IsBossEnemy(GameObject enemyObject)
    {
        if (enemyObject == null)
            return false;
        EnemyDate enemy = enemyObject.GetComponent<EnemyDate>();
        return enemy != null && IsBossEnemy(enemy);
    }

    private static bool IsExplicitlyExcluded(string typeName)
    {
        string[] excluded = EnemyFactionsConfig.BossExcludeTypes;
        if (excluded == null)
            return false;
        for (int i = 0; i < excluded.Length; i++)
        {
            string entry = excluded[i];
            if (!string.IsNullOrEmpty(entry) && typeName.Equals(entry, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static bool IsExplicitlyIncluded(string typeName)
    {
        string[] included = EnemyFactionsConfig.BossTypes;
        if (included == null)
            return false;
        for (int i = 0; i < included.Length; i++)
        {
            string entry = included[i];
            if (!string.IsNullOrEmpty(entry) && typeName.Equals(entry, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static bool HasVanillaBossNamePrefix(string typeName)
    {
        return typeName.StartsWith("Boss", StringComparison.OrdinalIgnoreCase) ||
               typeName.StartsWith("BOSS", StringComparison.Ordinal);
    }

    private static bool HasStructuralBossMarker(Type enemyType)
    {
        if (enemyType == null)
            return false;

        bool cached;
        if (_structuralBossByType.TryGetValue(enemyType, out cached))
            return cached;

        bool isBoss = TypeDeclaresBossFlag(enemyType) || TypeDeclaresBossIdEnum(enemyType);
        _structuralBossByType[enemyType] = isBoss;
        return isBoss;
    }

    private static bool TypeDeclaresBossFlag(Type type)
    {
        return TryFindDeclaredField(type, "BOSSflag", typeof(bool)) != null;
    }

    private static bool TypeDeclaresBossIdEnum(Type type)
    {
        return TryFindDeclaredField(type, "Bossidenum", typeof(int)) != null;
    }

    private static FieldInfo TryFindDeclaredField(Type type, string fieldName, Type fieldType)
    {
        for (Type t = type; t != null && t != typeof(EnemyDate) && t != typeof(object); t = t.BaseType)
        {
            FieldInfo field = t.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field != null && field.FieldType == fieldType && !field.IsStatic)
                return field;
        }

        return null;
    }

    private static bool TryReadBossFlagValue(EnemyDate enemy)
    {
        if (enemy == null)
            return false;

        FieldInfo field = ResolveBossFlagField(enemy.GetType());
        if (field == null)
            return false;

        try
        {
            object raw = field.GetValue(enemy);
            return raw is bool && (bool)raw;
        }
        catch
        {
            return false;
        }
    }

    private static FieldInfo ResolveBossFlagField(Type type)
    {
        if (type == null)
            return null;

        FieldInfo cached;
        if (_bossFlagFieldByType.TryGetValue(type, out cached))
            return cached;

        FieldInfo resolved = TryFindDeclaredField(type, "BOSSflag", typeof(bool));

        _bossFlagFieldByType[type] = resolved;
        return resolved;
    }
}
