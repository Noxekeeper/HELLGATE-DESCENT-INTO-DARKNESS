using System.Collections;
using NoREroMod.Systems.CombatAi.Factions;
using NoREroMod.Systems.Pregnancy.OffspringArchetype;
using UnityEngine;

namespace NoREroMod.Systems.Pregnancy.Patches;

/// <summary>
/// Deferred setup for spawned offspring: waits for vanilla Start(), hides enemy HUD markers,
/// applies uniform scale, snaps hideout companions to ground, and keeps their combat AI active.
/// </summary>
internal sealed class WitchOffspringSpawnSetup : MonoBehaviour
{
    public float UniformScale { get; set; } = ChildData.InfantBirthScale;
    public bool IsHideoutCompanion { get; set; }

    private void Start()
    {
        StartCoroutine(FinalizeAfterVanillaStart());
    }

    private IEnumerator FinalizeAfterVanillaStart()
    {
        yield return null;

        if (this == null || gameObject == null)
            yield break;

        WitchOffspringVisuals.HideEnemyCombatUi(gameObject);

        var enemyDate = GetComponent<EnemyDate>();
        var rb = GetComponent<Rigidbody2D>();

        if (IsHideoutCompanion)
        {
            // Keep Untagged for collision layer only — altar/zone cleanup destroys the Enemy tag.
            gameObject.tag = "Untagged";
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0)
                gameObject.layer = enemyLayer;

            OffspringEnemyCompanionSetup.ApplyCompanionAi(gameObject, hideoutCompanion: true);

            if (enemyDate != null)
            {
                WitchOffspringVisuals.ApplyUniformOffspringScale(enemyDate, UniformScale);
                WitchOffspringVisuals.SnapFeetToGround(gameObject);
            }

            if (rb != null)
            {
                rb.simulated = true;
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            WitchOffspringVisuals.HideEnemyCombatUi(gameObject);
        }
        else
        {
            OffspringEnemyCompanionSetup.ApplyCompanionAi(gameObject, hideoutCompanion: false);

            if (enemyDate != null)
                WitchOffspringVisuals.ApplyUniformOffspringScale(enemyDate, UniformScale);
        }
    }

    private void LateUpdate()
    {
        if (UniformScale <= 0f)
            return;

        var enemyDate = GetComponent<EnemyDate>();
        if (enemyDate == null)
            return;

        // Some enemy scripts reset scale in Start/LateUpdate; keep uniform offspring scale.
        int dir = enemyDate.DIR != 0 ? enemyDate.DIR : 1;
        Vector3 target = new Vector3(dir * UniformScale, UniformScale, UniformScale);
        if (enemyDate.scale == target && transform.localScale == target)
            return;

        enemyDate.scale = target;
        transform.localScale = target;
    }
}

internal static class WitchFactionReputation
{
    /// <summary>Active Witch-faction members for the HUD row (alive in hideout; kidnapped/dead excluded).</summary>
    public static int GetAliveOffspringCount()
    {
        if (!PregnancyConfig.IsEnabled)
            return 0;

        return PregnancySlotStore.CountChildrenInHideout();
    }
}
