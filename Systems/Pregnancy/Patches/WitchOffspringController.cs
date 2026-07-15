using System;
using System.Collections;
using UnityEngine;

namespace NoREroMod.Systems.Pregnancy.Patches;

/// <summary>
/// Marks a MafiaMuscle instance as Aradia's Witch-faction offspring and manages its
/// lifecycle after the birth transformation (short on-screen display, then hideout/move-out).
/// </summary>
public class WitchOffspringController : MonoBehaviour
{
    public int FactionSource { get; private set; }
    public float BirthScale { get; private set; }
    public string ChildGuid { get; set; }

    /// <summary>How long the offspring stays visible before being moved to the hideout or despawned.</summary>
    public float DisplayDelayBeforeHideout { get; set; } = 0f;

    /// <summary>
    /// If true, the child was spawned directly in the hideout (ParishChurch) and should not be
    /// moved/despawned by the display logic.
    /// </summary>
    public bool IsHideoutResident { get; set; } = false;

    private bool _intentionalDespawn;
    private float _lastSyncedHp = -999f;

    public void Initialize(int factionSource, float scale)
    {
        FactionSource = factionSource;
        BirthScale = scale;
    }

    public void SetBirthScale(float scale)
    {
        BirthScale = scale;
    }

    public void StartHideoutMove(ChildData childData)
    {
        if (IsHideoutResident)
        {
            if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
                Plugin.Log?.LogInfo($"[Pregnancy.Offspring] Hideout resident spawned: {ChildGuid}");
            return;
        }

        StartCoroutine(MoveToHideoutDeferred(childData));
    }

    private IEnumerator MoveToHideoutDeferred(ChildData childData)
    {
        if (DisplayDelayBeforeHideout > 0f)
            yield return new WaitForSeconds(DisplayDelayBeforeHideout);

        if (this == null || gameObject == null || childData == null)
            yield break;

        if (HideoutSceneUtility.IsParishHideoutActive())
        {
            childData.State = (int)ChildState.InHideout;
            childData.IsSpawned = true;
            Vector2 nodePos = HideoutSceneUtility.GetNodePosition(childData.HideoutNodeIndex);
            transform.position = new Vector3(nodePos.x, nodePos.y, 0f);

            var setup = GetComponent<WitchOffspringSpawnSetup>();
            if (setup != null)
                setup.IsHideoutCompanion = true;

            PregnancySlotStore.MarkDirty();

            if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
                Plugin.Log?.LogInfo($"[Pregnancy.Offspring] Moved child {ChildGuid} to hideout node {childData.HideoutNodeIndex}");
        }
        else
        {
            // Not in the hideout scene: despawn the visual but keep the child data alive.
            _intentionalDespawn = true;
            childData.State = (int)ChildState.InHideout;
            childData.IsSpawned = false;
            PregnancySlotStore.MarkDirty();

            if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
                Plugin.Log?.LogInfo($"[Pregnancy.Offspring] Child {ChildGuid} despawned outside ParishChurch; will respawn in hideout later");

            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (!IsHideoutResident || string.IsNullOrEmpty(ChildGuid))
            return;

        var enemyDate = GetComponent<EnemyDate>();
        if (enemyDate == null)
            return;

        if (Mathf.Approximately(enemyDate.Hp, _lastSyncedHp))
            return;

        _lastSyncedHp = enemyDate.Hp;
        OffspringHideoutHealth.SyncControllerToStore(this);
    }

    private void OnDestroy()
    {
        if (_intentionalDespawn)
            return;

        if (string.IsNullOrEmpty(ChildGuid))
            return;

        foreach (var child in PregnancySlotStore.GetAllChildren())
        {
            if (child.Guid != ChildGuid)
                continue;

            child.IsSpawned = false;
            PregnancySlotStore.MarkDirty();

            if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
            {
                string kind = IsHideoutResident ? "hideout resident" : "birth visual";
                Plugin.Log?.LogInfo($"[Pregnancy.Offspring] {kind} destroyed for {ChildGuid}; roster entry kept");
            }

            return;
        }
    }
}
