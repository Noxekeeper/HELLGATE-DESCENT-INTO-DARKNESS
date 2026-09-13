using System.Collections.Generic;
using UnityEngine;

namespace NoREroMod.Systems.EnemyFatality;

/// <summary>
/// Picks which clip folder to play for a fatality trigger.
/// Per-clip Enable / HpThreshold / Chance (relative weight) from
/// <see cref="EnemyFatalityClipDefs"/>. Per-enemy <c>Chance</c> is the
/// fatality trigger probability (0–1) after a clip is eligible.
/// ClipPath override forces one folder (still gated by that clip’s settings when registered).
/// </summary>
internal static class EnemyFatalityClipPicker
{
    /// <summary>
    /// Returns a relative clip path, or null if no clip is eligible
    /// (disabled / HP / zero weight / trigger Chance miss).
    /// </summary>
    internal static string PickRelative(IEnemyFatalityProfile profile, float hpRatio)
    {
        if (profile == null)
            return null;

        string[] pool = GetCandidateRelatives(profile);
        if (pool == null || pool.Length == 0)
            return null;

        var eligible = new List<Candidate>(pool.Length);
        for (int i = 0; i < pool.Length; i++)
        {
            string rel = pool[i];
            if (string.IsNullOrEmpty(rel))
                continue;

            Candidate c;
            if (!TryBuildCandidate(profile, rel, hpRatio, out c))
                continue;

            eligible.Add(c);
        }

        if (eligible.Count == 0)
        {
            EnemyFatalityConfig.LogDebug(
                "[" + profile.Id + "] No eligible clips (Enable/HP/weight) — skip");
            return null;
        }

        // Per-enemy Chance = trigger probability once at least one clip passed HP/weight.
        float triggerChance = Mathf.Clamp01(profile.Chance);
        if (triggerChance < 0.999f && Random.value > triggerChance)
        {
            EnemyFatalityConfig.LogDebug(
                "[" + profile.Id + "] Trigger Chance miss ("
                + triggerChance.ToString("0.###") + ") — skip");
            return null;
        }

        string picked = PickWeighted(eligible);
        EnemyFatalityConfig.LogDebug(
            "[" + profile.Id + "] Clip pick → " + picked
            + " (eligible=" + eligible.Count + ")");
        return picked;
    }

    /// <summary>All relatives that should be preloaded for this profile.</summary>
    internal static string[] GetPreloadRelatives(IEnemyFatalityProfile profile)
    {
        return GetCandidateRelatives(profile) ?? new string[0];
    }

    private struct Candidate
    {
        internal string Relative;
        internal float Weight;
    }

    private static string[] GetCandidateRelatives(IEnemyFatalityProfile profile)
    {
        if (profile == null)
            return new string[0];

        var bound = profile as EnemyFatalityBoundProfile;
        if (bound != null && bound.HasClipPathOverride)
            return new[] { bound.ClipPathRelative };

        string[] pool = profile.ClipPoolRelatives;
        if (pool != null && pool.Length > 0)
            return pool;

        return string.IsNullOrEmpty(profile.DefaultClipRelative)
            ? new string[0]
            : new[] { profile.DefaultClipRelative };
    }

    private static bool TryBuildCandidate(
        IEnemyFatalityProfile profile,
        string relative,
        float hpRatio,
        out Candidate candidate)
    {
        candidate = default(Candidate);

        EnemyFatalityClipDefs.ClipDef def = EnemyFatalityClipDefs.GetByRelative(relative);
        if (def != null)
        {
            if (!def.IsEnabled)
            {
                EnemyFatalityConfig.LogDebug(
                    "[" + profile.Id + "] Clip " + def.Id + " disabled — skip");
                return false;
            }

            if (!EnemyFatalityClipDefs.PassesHpGate(def.HpThreshold, hpRatio))
            {
                EnemyFatalityConfig.LogDebug(
                    "[" + profile.Id + "] Clip " + def.Id
                    + " HP ratio " + hpRatio.ToString("0.###")
                    + " >= threshold " + def.HpThreshold.ToString("0.###") + " — skip");
                return false;
            }

            float weight = def.ChanceWeight;
            if (weight <= 0.0001f)
            {
                EnemyFatalityConfig.LogDebug(
                    "[" + profile.Id + "] Clip " + def.Id + " Chance weight 0 — skip");
                return false;
            }

            candidate.Relative = def.RelativePath;
            candidate.Weight = weight;
            return true;
        }

        // Unregistered path (custom ClipPath): fall back to per-enemy HP gate.
        // Weight is always 1 here — per-enemy Chance is the trigger roll in PickRelative.
        if (!EnemyFatalityClipDefs.PassesHpGate(profile.HpThresholdPercent, hpRatio))
            return false;

        candidate.Relative = relative.Trim();
        candidate.Weight = 1f;
        return true;
    }

    private static string PickWeighted(List<Candidate> eligible)
    {
        if (eligible.Count == 1)
            return eligible[0].Relative;

        float total = 0f;
        for (int i = 0; i < eligible.Count; i++)
            total += eligible[i].Weight;

        if (total <= 0.0001f)
            return eligible[0].Relative;

        float roll = Random.value * total;
        float acc = 0f;
        for (int i = 0; i < eligible.Count; i++)
        {
            acc += eligible[i].Weight;
            if (roll <= acc)
                return eligible[i].Relative;
        }

        return eligible[eligible.Count - 1].Relative;
    }
}
