using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;
using NoREroMod.Systems.Dialogue;
using NoREroMod.Systems.Rewards;

namespace NoREroMod.Patches.Enemy.HeckGateEnemy;

/// <summary>
/// Runtime behavior module for the custom HellGate slime variant "biscord".
/// Handles stat overrides, panic movement, and rewards. (Grab/H-scene Harmony was removed — it conflicted with global grab flow.)
/// </summary>
internal static class HeckGateSlimeModule
{
    private const string BiscodSpawnName = "biscord";
    private const float BiscodBaseMaxHp = 5000f;
    private const float BiscodAttack = 1f;
    private const float BiscodBaseMoveSpeed = 2.6f;
    private const int BiscodExpMultiplier = 20;
    private const float PanicMoveSpeed = 4.4f;
    private const float PanicDurationOnHit = 2.75f;
    private const string BiscordDropConfigFolder = @"BepInEx\plugins\HellGateJson\DropSystem";
    private const string BiscordDropConfigFile = "biscord-drop-table.json";
    private static DropTableConfig s_biscordDropConfig;
    private static bool s_biscordDropConfigLoaded;
    private static readonly HashSet<int> RewardedBiscordInstanceIds = new HashSet<int>();

    private static readonly Dictionary<int, float> LastKnownHpByInstance = new Dictionary<int, float>();
    private static readonly Dictionary<int, float> PanicTimerByInstance = new Dictionary<int, float>();

    private static bool IsBiscod(suraimu slime)
    {
        if (slime == null) return false;
        if (slime.GetComponent<BiscodMarker>() != null) return true;
        if (string.Equals(slime.gameObject.name, BiscodSpawnName, StringComparison.OrdinalIgnoreCase)) return true;
        return slime.gameObject.name != null && slime.gameObject.name.IndexOf(BiscodSpawnName, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    [HarmonyPatch(typeof(suraimu), "Start")]
    [HarmonyPostfix]
    private static void BiscodStartPostfix(suraimu __instance)
    {
        if (!IsBiscod(__instance)) return;

        // Vanilla enemies spawn loot via serialized EnemyDate.Drop + Instantiate; slime prefabs often omit Drop — borrow from a vanilla prefab.
        VanillaEnemyDropWiring.EnsureEnemyDateDropReference(__instance);

        int id = __instance.GetInstanceID();
        RewardedBiscordInstanceIds.Remove(id);
        ApplyBiscodStats(__instance, fullHeal: true);
        LastKnownHpByInstance[id] = __instance.Hp;
        PanicTimerByInstance[id] = 0f;
    }

    [HarmonyPatch(typeof(suraimu), "Update")]
    [HarmonyPostfix]
    private static void BiscodUpdatePostfix(suraimu __instance)
    {
        if (!IsBiscod(__instance)) return;

        if (__instance.Hp <= 0f)
        {
            TryAwardBiscordRewardsOnce(__instance);
            return;
        }

        int id = __instance.GetInstanceID();
        float hp = __instance.Hp;

        if (!LastKnownHpByInstance.TryGetValue(id, out float lastHp))
            lastHp = hp;

        if (hp < lastHp - 0.01f)
        {
            PanicTimerByInstance[id] = PanicDurationOnHit;
        }

        LastKnownHpByInstance[id] = hp;

        float timer = 0f;
        if (PanicTimerByInstance.TryGetValue(id, out float existing))
            timer = Mathf.Max(0f, existing - Time.deltaTime);

        bool playerNear = Mathf.Abs(__instance.distance) <= 6f && Mathf.Abs(__instance.distance_y) <= 5f;
        if (playerNear)
            timer = Mathf.Max(timer, 0.2f);

        PanicTimerByInstance[id] = timer;

        if (timer <= 0f)
        {
            __instance.enmMovespeed = BiscodBaseMoveSpeed;
            if (!IsBiscodInActiveHSceneWithPlayer(__instance)
                && __instance.state == suraimu.enemystate.EROWALK)
                __instance.state = suraimu.enemystate.BLANK;
            return;
        }

        __instance.enmMovespeed = PanicMoveSpeed;
        if (!IsBiscodInActiveHSceneWithPlayer(__instance)
            && __instance.state == suraimu.enemystate.EROWALK)
            __instance.state = suraimu.enemystate.BLANK;
    }

    private static bool IsBiscodInActiveHSceneWithPlayer(suraimu slime)
    {
        if (slime == null || slime.com_player == null)
            return false;
        return slime.com_player.eroflag && slime.eroflag;
    }

    [HarmonyPatch(typeof(suraimu), "OnDestroy")]
    [HarmonyPostfix]
    private static void BiscodOnDestroyPostfix(suraimu __instance)
    {
        if (__instance == null) return;
        if (IsBiscod(__instance))
            TryAwardBiscordRewardsOnce(__instance);

        int id = __instance.GetInstanceID();
        LastKnownHpByInstance.Remove(id);
        PanicTimerByInstance.Remove(id);
        // Do not remove RewardedBiscordInstanceIds: Unity reuses GetInstanceID() after destroy — removing allowed duplicate payouts.
        BiscordDialogues.ClearInstance(id);
    }

    /// <summary>
    /// HellGate loot must only run on real death. <c>OnDestroy</c> also fires on scene unload / despawn while alive — that used to spawn pickups at the enemy position (looked like "free drop on spawn").
    /// </summary>
    private static bool IsBiscordDeadForLoot(suraimu slime)
    {
        if (slime == null)
            return false;
        return slime.Hp <= 0f || slime.state == suraimu.enemystate.DEATH;
    }

    private static void TryAwardBiscordRewardsOnce(suraimu slime)
    {
        if (slime == null)
            return;

        int id = slime.GetInstanceID();
        if (RewardedBiscordInstanceIds.Contains(id))
            return;

        if (!IsBiscordDeadForLoot(slime))
            return;

        Plugin.Log?.LogInfo($"[biscord] TryAward rewards: instanceId={id}, hp={slime.Hp:0.##}, state={slime.state}");

        // IMPORTANT: do not mark rewarded until Award succeeds. Marking early blocked OnDestroy retry and caused zero drops forever.
        if (!AwardBiscordRewardsOnDeath(slime))
            return;

        RewardedBiscordInstanceIds.Add(id);
        Plugin.Log?.LogInfo($"[biscord] Rewards committed (exp + drop pipeline) for instanceId={id}");
    }

    private static void ApplyBiscodStats(suraimu slime, bool fullHeal)
    {
        if (slime == null) return;

        float targetMaxHp = GetBiscodMaxHp(slime);
        slime.MaxHp = targetMaxHp;
        slime.Exp = 0;
        slime.enmATK = BiscodAttack;
        slime.enmMovespeed = BiscodBaseMoveSpeed;
        if (fullHeal)
            slime.Hp = targetMaxHp;
    }

    private static float GetBiscodMaxHp(suraimu slime)
    {
        float scale = 1f;
        try
        {
            EnemyDate enemy = slime as EnemyDate;
            if (enemy != null)
            {
                int difficulty = 1;
                try
                {
                    difficulty = Traverse.Create(enemy).Field("GameDifficultyFlag").GetValue<int>();
                }
                catch
                {
                    try
                    {
                        difficulty = Traverse.Create(enemy).Property("GameDifficultyFlag").GetValue<int>();
                    }
                    catch { }
                }

                switch (difficulty)
                {
                    case 0:
                        scale = 0.6f;
                        break;
                    case 3:
                        scale = 1.1f;
                        break;
                    default:
                        scale = 1f;
                        break;
                }
            }
        }
        catch { }

        return Mathf.Max(1f, BiscodBaseMaxHp * scale);
    }

    private static int GetGameDifficultyFlag(EnemyDate enemy)
    {
        if (enemy == null)
            return 1;

        int difficulty = 1;
        try
        {
            difficulty = Traverse.Create(enemy).Field("GameDifficultyFlag").GetValue<int>();
        }
        catch
        {
            try
            {
                difficulty = Traverse.Create(enemy).Property("GameDifficultyFlag").GetValue<int>();
            }
            catch { }
        }
        return difficulty;
    }

    private static void EnsureBiscordDropConfigLoaded()
    {
        if (s_biscordDropConfigLoaded)
            return;

        s_biscordDropConfigLoaded = true;
        s_biscordDropConfig = null;
        try
        {
            string gameRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string configPath = Path.Combine(Path.Combine(gameRoot, BiscordDropConfigFolder), BiscordDropConfigFile);
            if (DropSystem.TryLoadConfig(configPath, out DropTableConfig config) && config != null)
            {
                s_biscordDropConfig = config;
                if (s_biscordDropConfig.drops == null || s_biscordDropConfig.drops.Length == 0 ||
                    !DropSystem.HasAnyWeightedTypedDrop(s_biscordDropConfig))
                {
                    Plugin.Log?.LogWarning($"[biscord] drop JSON has no playable rows (path={configPath}); using emergency ring table.");
                    s_biscordDropConfig = DropSystem.CreateBiscordEmergencyDropTable();
                }
            }
            else
            {
                Plugin.Log?.LogWarning($"[biscord] drop config missing or invalid: {configPath}; using emergency ring table.");
                s_biscordDropConfig = DropSystem.CreateBiscordEmergencyDropTable();
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[biscord] failed to load drop config: {ex.Message}; using emergency ring table.");
            try
            {
                s_biscordDropConfig = DropSystem.CreateBiscordEmergencyDropTable();
            }
            catch { }
        }
    }

    /// <summary>
    /// Returns false if loot pipeline could not start (so <see cref="TryAwardBiscordRewardsOnce"/> can retry, e.g. from <c>OnDestroy</c>).
    /// EXP is granted only after drop config + Drop prefab are known good, to avoid double EXP on retry.
    /// </summary>
    private static bool AwardBiscordRewardsOnDeath(suraimu slime)
    {
        if (slime == null)
            return false;

        EnemyDate enemy = slime as EnemyDate;
        if (enemy == null)
            return false;

        EnsureBiscordDropConfigLoaded();
        if (s_biscordDropConfig == null)
        {
            Plugin.Log?.LogWarning("[biscord] drop config is null after load/emergency; cannot award.");
            return false;
        }

        GameObject dropPrefab = DropSystem.ResolveEnemyDropPickupPrefab(enemy);
        if (dropPrefab == null)
        {
            Plugin.Log?.LogWarning("[biscord] EnemyDate.Drop unresolved; cannot spawn pickup (will retry if another death signal arrives).");
            return false;
        }

        int difficulty = GetGameDifficultyFlag(enemy);
        int rewardExp = DropSystem.ApplyVanillaExpDifficultyScaling(120 * BiscodExpMultiplier, difficulty);

        try
        {
            PlayerStatus playerStatus = Traverse.Create(enemy).Field("playerstatus").GetValue<PlayerStatus>();
            if (playerStatus != null)
            {
                playerStatus.ExpALL += rewardExp;
                playerStatus.Exppoint += rewardExp;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[biscord] EXP grant failed: {ex.Message}");
        }

        try
        {
            int rolls = Mathf.Max(1, s_biscordDropConfig.settings != null ? s_biscordDropConfig.settings.rollCount : 1);
            bool autoPickup = s_biscordDropConfig.settings != null && s_biscordDropConfig.settings.autoPickup;
            var alreadyResolved = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < rolls; i++)
            {
                DropRollResult roll = DropSystem.Roll(s_biscordDropConfig, alreadyResolved);
                if (!roll.IsValid)
                {
                    Plugin.Log?.LogWarning("[biscord] drop roll invalid (table empty or no GDE id for rolled key).");
                    break;
                }

                if (!DropSystem.TrySpawnDrop(slime.transform, dropPrefab, roll, autoPickup))
                    Plugin.Log?.LogWarning("[biscord] TrySpawnDrop failed (see [drop-system] logs).");
                else if (roll.RewardType != DropRewardType.None)
                    alreadyResolved.Add(DropSystem.ResolvedRewardKey(roll.RewardType, roll.RewardId));
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[biscord] drop reward exception: {ex.Message}");
            return false;
        }

        return true;
    }

}

internal sealed class BiscodMarker : MonoBehaviour
{
}

/// <summary>
/// Runtime safety profile for biscord.
/// Does not rely on Harmony patch ordering and keeps key stats pinned.
/// </summary>
internal sealed class BiscodRuntimeProfile : MonoBehaviour
{
    private const float BiscodBaseMaxHp = 5000f;
    private const float BiscodAttack = 1f;
    private const float BiscodBaseMoveSpeed = 2.6f;
    private const int DamageDialogueEveryHits = 5;

    private suraimu _slime;
    private float _lastHpForDialogue = -1f;
    private int _damageHitCounter;
    private bool _initialStatsApplied;

    private void Awake()
    {
        _slime = GetComponent<suraimu>();
        TryApplyInitialStats();
    }

    private void Start()
    {
        TryApplyInitialStats();
    }

    private void Update()
    {
        if (_slime == null) return;
        // Never restore HP once vanilla death has started.
        // `suraimu` enters DEATH when Hp <= 0.
        if (_slime.Hp <= 0f || _slime.state == suraimu.enemystate.DEATH)
            return;

        float hpBeforeClamp = _slime.Hp;
        if (_lastHpForDialogue < 0f)
            _lastHpForDialogue = hpBeforeClamp;
        else if (hpBeforeClamp < _lastHpForDialogue - 0.01f)
        {
            _damageHitCounter++;
            if (_damageHitCounter >= DamageDialogueEveryHits)
            {
                _damageHitCounter = 0;
                BiscordDialogues.TryShowOnDamageForce(_slime);
            }
        }
        BiscordDialogues.TryShowOnStateUpdate(_slime);

        // Keep custom profile stable even if other systems overwrite Start values.
        // Other systems may rewrite slime HP back to vanilla values.
        // Re-pin max HP every frame for alive biscord, while preserving taken damage.
        float targetMaxHp = GetBiscodMaxHp();
        float currentMaxHp = Mathf.Max(1f, _slime.MaxHp);
        float currentHp = Mathf.Clamp(_slime.Hp, 0f, currentMaxHp);
        float missingHp = Mathf.Max(0f, currentMaxHp - currentHp);
        _slime.MaxHp = targetMaxHp;
        _slime.Hp = Mathf.Clamp(targetMaxHp - missingHp, 0f, targetMaxHp);
        _slime.Exp = 0;
        _slime.enmATK = BiscodAttack;
        if (_slime.enmMovespeed < BiscodBaseMoveSpeed)
            _slime.enmMovespeed = BiscodBaseMoveSpeed;

        // biscord must not drive vanilla H-scene walk. While the player is already in an active
        // H-scene with this slime, leave erodata enabled so struggle escape can finish cleanly.
        bool playerInHWithThisSlime = _slime.com_player != null
            && _slime.com_player.eroflag
            && _slime.eroflag;

        if (_slime.state == suraimu.enemystate.EROWALK && !playerInHWithThisSlime)
            _slime.state = suraimu.enemystate.BLANK;

        if (!playerInHWithThisSlime)
            DisableHSceneObjects();

        _lastHpForDialogue = _slime.Hp;
    }

    private void TryApplyInitialStats()
    {
        if (_initialStatsApplied || _slime == null)
            return;
        if (_slime.Hp <= 0f || _slime.state == suraimu.enemystate.DEATH)
            return;

        // Apply the intended initial boss HP once per spawn.
        float targetMaxHp = GetBiscodMaxHp();
        _slime.MaxHp = targetMaxHp;
        _slime.Hp = targetMaxHp;
        _slime.Exp = 0;
        _slime.enmATK = BiscodAttack;
        if (_slime.enmMovespeed < BiscodBaseMoveSpeed)
            _slime.enmMovespeed = BiscodBaseMoveSpeed;

        _initialStatsApplied = true;
    }

    private float GetBiscodMaxHp()
    {
        float scale = 1f;
        try
        {
            EnemyDate enemy = _slime as EnemyDate;
            if (enemy != null)
            {
                int difficulty = 1;
                try
                {
                    difficulty = Traverse.Create(enemy).Field("GameDifficultyFlag").GetValue<int>();
                }
                catch
                {
                    try
                    {
                        difficulty = Traverse.Create(enemy).Property("GameDifficultyFlag").GetValue<int>();
                    }
                    catch { }
                }

                switch (difficulty)
                {
                    case 0:
                        scale = 0.6f;
                        break;
                    case 3:
                        scale = 1.1f;
                        break;
                    default:
                        scale = 1f;
                        break;
                }
            }
        }
        catch { }

        return Mathf.Max(1f, BiscodBaseMaxHp * scale);
    }

    private void DisableHSceneObjects()
    {
        // `suraimu` stores private H-scene object references; disable them every frame for biscord.
        try
        {
            object eroDataObj = Traverse.Create(_slime).Field("erodata").GetValue();
            if (eroDataObj is GameObject eroData && eroData.activeSelf)
                eroData.SetActive(false);
        }
        catch { }

        try
        {
            object ero2DataObj = Traverse.Create(_slime).Field("ero2data").GetValue();
            if (ero2DataObj is GameObject ero2Data && ero2Data.activeSelf)
                ero2Data.SetActive(false);
        }
        catch { }
    }
}
