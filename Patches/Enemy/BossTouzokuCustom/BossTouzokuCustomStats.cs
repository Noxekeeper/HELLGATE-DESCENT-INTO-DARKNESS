using UnityEngine;

namespace NoREroMod.Patches.Enemy.BossTouzokuCustom;

/// <summary>
/// HellGate field spawn variant of BossTouzoku: same prefab/class, disambiguated by object name.
/// </summary>
internal static class BossTouzokuCustomStats
{
    public const string ObjectNameKey = "BossTouzokuCustom";
    public const string RegistryKey = "BossTouzokuCustom";
    /// <summary>Field mob max HP (vanilla arena boss is 2000).</summary>
    public const float FieldMobMaxHp = 1200f;
    /// <summary>Same combat reach as TouzokuNormal (not arena BossTouzoku 9/50).</summary>
    public const float FieldMobAtkdistance = 4f;
    public const float FieldMobMovedistance = 5f;
    public const float FieldMobDetectionRange = 13f;

    public static bool IsCustom(BossTouzoku boss)
    {
        if (boss == null || boss.gameObject == null)
            return false;

        if (boss.gameObject.GetComponent<HellGateBossTouzokuCustomMarker>() != null)
            return true;

        return boss.gameObject.name != null &&
               boss.gameObject.name.Contains(ObjectNameKey);
    }

    public static bool IsCustom(EnemyDate enemy)
    {
        return enemy is BossTouzoku boss && IsCustom(boss);
    }
}
