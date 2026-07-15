using UnityEngine;



namespace NoREroMod.Patches.Enemy.BossTouzokuCustom;



/// <summary>Applied before SetActive so Start/bootstrap can detect field-spawn variant.</summary>

internal sealed class HellGateBossTouzokuCustomMarker : MonoBehaviour

{

    internal bool HpScaled;

    internal bool CombatApplied;

    internal bool EroRefsReady;

    internal bool EroScriptsWarmedUp;

    internal bool DeathHandled;

    internal float LastAggroNudgeAt = -999f;

    internal bool WeaponHitReactionGuard;

    internal int LastWeaponHitFrame = -1;

    internal float LastWeaponHitAtkId = float.NaN;

    internal bool SortingCaptured;

    internal string BodySortLayer = "player";

    internal int BodySortOrder;



    private void OnDisable()
    {
        // Hot reload / spawn refresh disables alive bosses — Destroyed() logs the outcome.
    }



    private void OnDestroy()

    {

        BossTouzoku boss = GetComponent<BossTouzoku>();

        if (boss == null || !BossTouzokuCustomStats.IsCustom(boss))

            return;



        Plugin.Log?.LogInfo(

            "[BossTouzokuCustom] Destroyed (state="

            + boss.state

            + " hp="

            + boss.Hp.ToString("0.##")

            + ").");

    }

}

