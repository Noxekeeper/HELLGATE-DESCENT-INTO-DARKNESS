using System.Collections.Generic;
using NoREroMod.Systems.Pregnancy.Patches;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace NoREroMod.Systems.Pregnancy.ShelterAttack;

internal static class ShelterAttackOutcome
{
    internal static void ResolveVictory()
    {
        if (ShelterAttackState.Phase == ShelterAttackPhase.Victory
            || ShelterAttackState.Phase == ShelterAttackPhase.Defeat)
            return;

        ShelterAttackState.Phase = ShelterAttackPhase.Victory;

        int advanced = 0;
        int alreadyMax = 0;
            foreach (ChildData child in PregnancySlotStore.GetAliveChildren())
        {
            if (child.GrowthStage >= 3)
            {
                alreadyMax++;
                continue;
            }

            child.AdvanceGrowthStage();
            OffspringHideoutSpawner.ApplyGrowthStageToSpawnedChild(child);
            advanced++;
        }

        CleanupAfterResolution();

        PregnancySlotStore.MarkDirty();
        ShelterAttackSlotStore.MarkDirty();

        if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
        {
            Plugin.Log?.LogInfo(
                $"[Pregnancy.ShelterAttack] Victory — {advanced} child(ren) advanced growth stage" +
                (alreadyMax > 0 ? $", {alreadyMax} already at max stage (3)." : "."));
        }

        ShelterAttackOutcomePresentation.PlayVictory(advanced);

        if (ShouldResetAfterVictory())
            ResetEventToIdle();
    }

    internal static void ResolveDefeat()
    {
        ResolveDefeat(isTimeout: false, suppressPresentation: false);
    }

    internal static void ResolveTimeoutDefeat()
    {
        ResolveDefeat(isTimeout: true, suppressPresentation: false);
    }

    internal static void ResolveTimeoutDefeatSilent()
    {
        ResolveDefeat(isTimeout: true, suppressPresentation: true);
    }

    private static void ResolveDefeat(bool isTimeout, bool suppressPresentation)
    {
        if (ShelterAttackState.Phase == ShelterAttackPhase.Victory
            || ShelterAttackState.Phase == ShelterAttackPhase.Defeat)
            return;

        ShelterAttackState.Phase = ShelterAttackPhase.Defeat;

        List<ChildData> children = PregnancySlotStore.GetAliveChildrenInHideout();
        int kidnapped = KidnapRandomChildren(children);

        CleanupAfterResolution();

        PregnancySlotStore.MarkDirty();
        ShelterAttackSlotStore.MarkDirty();

        if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
        {
            Plugin.Log?.LogInfo(
                $"[Pregnancy.ShelterAttack] Defeat ({(isTimeout ? "timeout" : "combat")}, silent={suppressPresentation}) — {kidnapped} child(ren) kidnapped.");
        }

        if (!suppressPresentation)
        {
            if (isTimeout)
                ShelterAttackOutcomePresentation.PlayTimeoutDefeat(kidnapped);
            else
                ShelterAttackOutcomePresentation.PlayDefeat(kidnapped);
        }

        if (ShouldResetAfterDefeat())
            ResetEventToIdle();
    }

    private static int KidnapRandomChildren(List<ChildData> children)
    {
        if (children == null || children.Count == 0)
            return 0;

        int maxKidnap = Mathf.Min(5, children.Count);
        int kidnapCount = Random.Range(1, maxKidnap + 1);

        var pool = new List<ChildData>(children);
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            ChildData temp = pool[i];
            pool[i] = pool[j];
            pool[j] = temp;
        }

        int kidnapped = 0;
        for (int i = 0; i < kidnapCount && i < pool.Count; i++)
        {
            ChildData child = pool[i];
            child.State = (int)ChildState.Kidnapped;
            child.IsSpawned = false;
            kidnapped++;
            DespawnHideoutVisual(child.Guid);
        }

        return kidnapped;
    }

    private static void DespawnHideoutVisual(string childGuid)
    {
        if (string.IsNullOrEmpty(childGuid))
            return;

        WitchOffspringController[] controllers = Object.FindObjectsOfType<WitchOffspringController>();
        for (int i = 0; i < controllers.Length; i++)
        {
            WitchOffspringController controller = controllers[i];
            if (controller == null || controller.ChildGuid != childGuid)
                continue;

            if (controller.gameObject != null)
                Object.Destroy(controller.gameObject);
        }
    }

    private static void CleanupAfterResolution()
    {
        ShelterAttackTracker.DestroyAllRemaining();
        ShelterAttackSpawnScheduler.Reset();
        ShelterAttackSceneGuard.Reset();
        ShelterAttackTimerHud.Reset();
    }

    private static bool ShouldResetAfterVictory()
    {
        return PregnancyConfig.ShelterAttackResetOnWin == null || PregnancyConfig.ShelterAttackResetOnWin.Value;
    }

    private static bool ShouldResetAfterDefeat()
    {
        return PregnancyConfig.ShelterAttackResetOnLoss == null || PregnancyConfig.ShelterAttackResetOnLoss.Value;
    }

    private static void ResetEventToIdle()
    {
        ShelterAttackState.Reset();
        ShelterAttackDriver.ResetTransientFlags();
        ShelterAttackSlotStore.MarkDirty();
    }
}
