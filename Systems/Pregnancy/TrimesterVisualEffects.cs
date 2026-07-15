using HarmonyLib;
using NoREroMod.Systems.CombatAi.Factions;
using UnityEngine;

namespace NoREroMod.Systems.Pregnancy;

/// <summary>
/// Spawns a subtle periodic visual effect on the player during the second and third trimesters.
/// The effect prefab is taken from <c>playereffect.Buffeffect[index]</c>; the index is configurable
/// per faction. The effect is parented to the player so it follows movement like the Rage wings.
/// Set the index to -1 to disable the effect for a faction.
/// </summary>
internal static class TrimesterVisualEffects
{
    private static float _nextEffectTime = -1f;

    public static void Process(playercon player, PlayerStatus ps)
    {
        if (!PregnancyConfig.IsEnabled || player == null || ps == null)
            return;
        if (!WitchPregnancyState.IsActive || WitchPregnancyState.CurrentTrimester < 2)
            return;

        float interval = PregnancyConfig.TrimesterVisualIntervalSeconds?.Value ?? 5f;
        if (interval <= 0f)
            return;

        float now = Time.time;
        if (_nextEffectTime < 0f)
            _nextEffectTime = now + interval;

        if (now < _nextEffectTime)
            return;

        _nextEffectTime = now + interval;

        int index = GetVisualEffectIndex(WitchPregnancyState.SourceFaction);
        if (index < 0)
            return;

        var particle = Traverse.Create(player).Field("particle").GetValue<playereffect>();
        if (particle == null)
            return;

        GameObject[] effects = particle.Buffeffect;
        if (effects == null || index >= effects.Length || effects[index] == null)
            return;

        float duration = PregnancyConfig.TrimesterVisualDurationSeconds?.Value ?? 2f;
        float offsetY = PregnancyConfig.TrimesterVisualOffsetY?.Value ?? 0.35f;

        GameObject go = Object.Instantiate(effects[index], player.transform);
        if (go != null)
        {
            go.transform.localPosition = new Vector3(0f, offsetY, 0f);
            if (duration > 0f)
                Object.Destroy(go, duration);
        }
    }

    private static int GetVisualEffectIndex(int faction)
    {
        return PregnancyConfig.NormalizeSourceFaction(faction) switch
        {
            FactionIds.Demons => PregnancyConfig.DemonsVisualEffectIndex?.Value ?? -1,
            FactionIds.Monsters => PregnancyConfig.MonstersVisualEffectIndex?.Value ?? -1,
            FactionIds.Church => PregnancyConfig.ChurchVisualEffectIndex?.Value ?? -1,
            FactionIds.Bandits => PregnancyConfig.BanditsVisualEffectIndex?.Value ?? -1,
            FactionIds.Mafia => PregnancyConfig.MafiaVisualEffectIndex?.Value ?? -1,
            FactionIds.Undead => PregnancyConfig.UndeadVisualEffectIndex?.Value ?? -1,
            _ => -1
        };
    }
}
