using System;
using UnityEngine;

namespace NoREroMod.Systems.Economy;

/// <summary>
/// Forward-compat extension hook: Phase 2 systems (Faction Treasury, Rage bonus,
/// MindBroken bonus, …) subscribe and contribute multiplicative factors to the
/// final gold drop amount. Phase 1 has no subscribers, so <see cref="Compute"/>
/// returns 1 and <see cref="EnemyDeathGoldDropPatch"/> behaves identically.
///
/// Subscribers should be cheap (a couple of dictionary lookups). The bus is invoked
/// once per enemy death.
/// </summary>
internal static class DropMultiplierBus
{
    public static event Func<EnemyDate, int, float> Apply;

    public static float Compute(EnemyDate enemy, int factionId)
    {
        if (Apply == null) return 1f;
        float m = 1f;
        Delegate[] handlers = Apply.GetInvocationList();
        for (int i = 0; i < handlers.Length; i++)
        {
            try
            {
                Func<EnemyDate, int, float> handler = (Func<EnemyDate, int, float>)handlers[i];
                float v = handler(enemy, factionId);
                if (float.IsNaN(v) || float.IsInfinity(v) || v <= 0f) continue;
                m *= v;
            }
            catch
            {
                // Single bad subscriber must not break gold awards.
            }
        }
        return Mathf.Clamp(m, 0.01f, 100f);
    }
}
