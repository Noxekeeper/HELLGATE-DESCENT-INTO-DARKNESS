using HarmonyLib;
using NoREroMod;
using NoREroMod.Systems.GrabSystem;

namespace NoREroMod.Systems.Audio;

/// <summary>
/// Hooks damage entry points and plays attack SFX from the custom Human pack.
/// Uses kickbackkind to split into Regular vs Power categories.
/// </summary>
internal static class AttackSoundPatch
{
    [System.ThreadStatic]
    private static EnemyDate _pendingAttacker;

    [System.ThreadStatic]
    private static int _pendingKickbackKind;

    [System.ThreadStatic]
    private static bool _pendingWasRanged;

    [HarmonyPatch(typeof(playercon), "fun_damage")]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void FunDamage_Prefix(int kickbackkind)
    {
        CaptureHitContext(kickbackkind);
    }

    [HarmonyPatch(typeof(playercon), "fun_damage")]
    [HarmonyPostfix]
    private static void FunDamage_Postfix()
    {
        FlushHitContext();
    }

    [HarmonyPatch(typeof(playercon), "fun_damage_Improvement")]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void FunDamageImprovement_Prefix(int kickbackkind)
    {
        CaptureHitContext(kickbackkind);
    }

    [HarmonyPatch(typeof(playercon), "fun_damage_Improvement")]
    [HarmonyPostfix]
    private static void FunDamageImprovement_Postfix()
    {
        FlushHitContext();
    }

    private static void CaptureHitContext(int kickbackkind)
    {
        _pendingAttacker = GrabViaAttackContext.CurrentAttacker;
        if (_pendingAttacker == null)
        {
            object currentEnemy = QTESystem.GetCurrentEnemyInstance();
            EnemyDate enemyDate = currentEnemy as EnemyDate;
            if (enemyDate != null) _pendingAttacker = enemyDate;
        }
        _pendingKickbackKind = kickbackkind;
        _pendingWasRanged = GrabViaAttackContext.LastDamageWasRanged;
    }

    private static void FlushHitContext()
    {
        try
        {
            AttackSoundSystem.TryPlayForHit(_pendingAttacker, _pendingKickbackKind, _pendingWasRanged);
        }
        catch
        {
            // Sound logic must never break combat flow.
        }
        finally
        {
            _pendingAttacker = null;
            _pendingKickbackKind = 0;
            _pendingWasRanged = false;
        }
    }
}
