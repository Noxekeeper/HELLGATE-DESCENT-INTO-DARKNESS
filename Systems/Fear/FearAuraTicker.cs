using HarmonyLib;
using NoREroMod.Patches.UI.MindBroken;
using NoREroMod.Systems.Cache;
using UnityEngine;

namespace NoREroMod.Systems.Fear;

/// <summary>
/// Aura + soft-decay + combat-effect tick via PlayerConUpdateDispatcher.
/// </summary>
internal static class FearAuraTicker
{
    private static float _scanCooldown;
    private static float _cachedAuraFearPerSecond;
    private static bool _cachedInCombat;
    private const float ScanInterval = 0.25f;

    internal static void Process(playercon player, bool eroflag, int erodown)
    {
        if (!FearConfig.IsEnabled || player == null)
            return;

        // HeartBeat uses unscaled time — run even when MB realtime gate returns 0.
        FearHeartbeatCues.Tick(player, GetPlayerStatus(player));

        float dt = MindBrokenRealtimeGate.GetClampedDeltaTime();
        if (dt <= 0f)
            return;

        FearSystem.TickBlockWindowExpiry();
        FearSystem.ProcessKnockdownEdge(erodown, eroflag);
        FearSystem.ProcessHSceneEdge(eroflag, erodown);

        // H-scene / trap H: flat pressure tick (same eroflag gate as MindBroken H growth).
        // Combat aura is not stacked on top — traps and enemy H both use this path.
        if (eroflag && erodown != 0)
        {
            float hPressure = FearCatalog.ResolveHSceneFearPerSecond();
            hPressure *= FearSystem.GetLowSpAuraMultiplier(player);
            if (hPressure > 0f)
                FearSystem.ApplyAuraDelta(hPressure, dt);
            return;
        }

        _scanCooldown -= dt;
        if (_scanCooldown <= 0f)
        {
            _scanCooldown = ScanInterval;
            RescanAuras(player, eroflag, erodown);
        }

        if (_cachedAuraFearPerSecond > 0f)
        {
            float pressure = _cachedAuraFearPerSecond * FearSystem.GetLowSpAuraMultiplier(player);
            FearSystem.ApplyAuraDelta(pressure, dt);
        }
        else if (!_cachedInCombat)
            FearSystem.ApplySoftDecay(dt);

        FearSystem.ProcessCombatEffects(player, dt);
    }

    private static void RescanAuras(playercon player, bool eroflag, int erodown)
    {
        _cachedAuraFearPerSecond = 0f;
        _cachedInCombat = eroflag || erodown != 0;

        GameObject playerObj = UnifiedPlayerCacheManager.GetPlayerObject();
        Vector3 playerPos = playerObj != null ? playerObj.transform.position : player.transform.position;

        float falloff = FearConfig.AuraFalloffMeters?.Value ?? 12f;
        if (falloff <= 0f) falloff = 12f;
        int topN = FearConfig.AuraStrongestN?.Value ?? 2;
        if (topN < 1) topN = 1;

        float best1 = 0f;
        float best2 = 0f;

        EnemyDate[] enemies;
        try
        {
            enemies = LiveEnemyCache.GetEnemies();
        }
        catch
        {
            return;
        }

        if (enemies == null) return;

        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyDate enemy = enemies[i];
            if (enemy == null || enemy.Hp <= 0f) continue;
            if (enemy.gameObject == null || !enemy.gameObject.activeInHierarchy) continue;

            float dist = Vector3.Distance(playerPos, enemy.transform.position);
            if (dist >= falloff) continue;

            float pressure = FearCatalog.ResolveForEnemy(enemy).fearPerSecond;
            if (pressure <= 0f) continue;

            float t = 1f - (dist / falloff);
            float contrib = pressure * t;
            if (contrib <= 0f) continue;

            _cachedInCombat = true;

            if (contrib > best1)
            {
                best2 = best1;
                best1 = contrib;
            }
            else if (contrib > best2)
            {
                best2 = contrib;
            }
        }

        _cachedAuraFearPerSecond = topN >= 2 ? (best1 + best2) : best1;
    }

    private static PlayerStatus GetPlayerStatus(playercon player)
    {
        if (player == null)
            return null;
        try
        {
            return Traverse.Create(player).Field("playerstatus").GetValue<PlayerStatus>()
                ?? UnifiedPlayerCacheManager.GetPlayerStatus();
        }
        catch
        {
            try { return UnifiedPlayerCacheManager.GetPlayerStatus(); }
            catch { return null; }
        }
    }
}
