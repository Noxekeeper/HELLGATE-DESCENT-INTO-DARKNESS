using System.Collections.Generic;
using System;
using System.Reflection;
using HarmonyLib;
using NoREroMod.Systems.EventCore.Host;
using NoREroMod.Systems.Spawn;
using NoREroMod.Systems.Pregnancy.Patches;
using UnityEngine;
using Spine.Unity;

namespace NoREroMod.Systems.CombatAi.Factions;

/// <summary>
/// Isolated runtime storage for faction data.
/// This class does not affect vanilla behavior until module is enabled.
/// </summary>
internal static class EnemyFactionRuntime
{
    internal const int FactionNeutral = FactionIds.Neutral;
    internal const int FactionBandits = FactionIds.Bandits;
    internal const int FactionDemons = FactionIds.Demons;

    private static readonly Dictionary<int, int> _enemyFactionByInstanceId = new Dictionary<int, int>();
    private static readonly Dictionary<int, EnemyDate> _enemyByInstanceId = new Dictionary<int, EnemyDate>();
    private static readonly Dictionary<int, float> _hostileToPlayerUntil = new Dictionary<int, float>();
    private static readonly HashSet<int> _sessionHostileToPlayer = new HashSet<int>();
    private static readonly Dictionary<int, int> _currentTargetByEnemyId = new Dictionary<int, int>();
    private static readonly Dictionary<int, float> _nextTargetRefreshAt = new Dictionary<int, float>();
    private static readonly Dictionary<int, float> _baseMoveSpeedByEnemyId = new Dictionary<int, float>();
    private static readonly Dictionary<int, float> _lastAppliedSpeedMultiplierByEnemyId = new Dictionary<int, float>();
    private static readonly Dictionary<string, float> _nextHitAtByPair = new Dictionary<string, float>();
    private static readonly Dictionary<int, bool> _isBossByInstanceId = new Dictionary<int, bool>();
    private static readonly Dictionary<int, bool> _prevAttackWindowState = new Dictionary<int, bool>();
    private static readonly Dictionary<int, float> _attackPulseUntil = new Dictionary<int, float>();
    private static readonly Dictionary<long, int> _relationByPair = new Dictionary<long, int>();
    private static readonly Dictionary<int, Color> _colorByFaction = new Dictionary<int, Color>();
    private static float _lastRelationReloadAt = -999f;
    private static float _lastColorReloadAt = -999f;
    private static readonly MethodInfo DamagePopMethod = AccessTools.Method(typeof(EnemyDate), "DamagePOP_fun");
    private const int RelationNeutral = 0;
    private const int RelationFriendly = 1;
    private const int RelationHostile = 2;

    private static readonly HashSet<int> _factionCombatCommitted = new HashSet<int>();
    private static readonly HashSet<int> _passiveWaitByEnemyId = new HashSet<int>();
    private static readonly HashSet<string> _seenTypeNames = new HashSet<string>();

    public static bool IsFactionCombatCommitted(GameObject enemyObject)
    {
        if (enemyObject == null)
            return false;

        return _factionCombatCommitted.Contains(enemyObject.GetInstanceID());
    }

    /// <summary>
    /// Once faction combat starts near the player, both sides keep fighting until the engagement ends
    /// (target gone / dead), even if the player leaves the activation radius.
    /// </summary>
    public static void MarkFactionCombatCommitted(EnemyDate self, EnemyDate target)
    {
        if (self != null && self.gameObject != null)
            _factionCombatCommitted.Add(self.gameObject.GetInstanceID());
        if (target != null && target.gameObject != null)
            _factionCombatCommitted.Add(target.gameObject.GetInstanceID());

        if (EnemyFactionsConfig.DebugLogging && self != null && target != null)
        {
            Plugin.Log?.LogInfo("[EnemyFactions] Faction combat committed: " +
                                self.GetType().Name + " vs " + target.GetType().Name);
        }
    }

    public static void ClearFactionCombatCommitted(GameObject enemyObject)
    {
        if (enemyObject == null)
            return;

        _factionCombatCommitted.Remove(enemyObject.GetInstanceID());
    }

    public static void RegisterEnemy(EnemyDate enemy)
    {
        if (enemy == null || enemy.gameObject == null)
            return;

        int id = enemy.gameObject.GetInstanceID();
        _enemyByInstanceId[id] = enemy;

        // Witch offspring (Aradia's children): special handling - always Witch faction
        if (enemy.GetComponent<WitchOffspringController>() != null)
        {
            _enemyFactionByInstanceId[id] = FactionIds.Witch;
            _hostileToPlayerUntil.Remove(id);
            if (EnemyFactionsConfig.DebugLogging)
                Plugin.Log?.LogInfo("[EnemyFactions] Registered Witch offspring '" + enemy.GetType().Name + "' as faction=Witch(700)");
            return;
        }

        // Vanilla suraimu (incl. biscord POI): neutral-aggressive — vanilla AI only,
        // no faction marker, rep, or inter-faction combat. Ignore spawn |faction= overrides.
        if (enemy is suraimu)
        {
            _enemyFactionByInstanceId[id] = FactionIds.Neutral;
            _hostileToPlayerUntil.Remove(id);
            return;
        }

        string typeName = enemy.GetType().Name;
        int factionId = ResolveFactionId(typeName);

        if (EnemyFactionsConfig.DebugLogging && _seenTypeNames.Add(typeName))
        {
            Plugin.Log?.LogInfo("[EnemyFactions] Registered '" + typeName + "' as faction=" + factionId +
                                " (" + FactionIdName(factionId) + ")");
        }
        SpawnFactionOverride overrideComponent = enemy.GetComponent<SpawnFactionOverride>();
        if (overrideComponent == null)
            overrideComponent = enemy.GetComponentInParent<SpawnFactionOverride>();
        if (overrideComponent == null && enemy.transform != null && enemy.transform.root != null)
            overrideComponent = enemy.transform.root.GetComponent<SpawnFactionOverride>();
        if (overrideComponent != null && FactionIds.TryParse(overrideComponent.FactionIdRaw, out int overridden))
            factionId = overridden;
        _enemyFactionByInstanceId[id] = factionId;
        _isBossByInstanceId[id] = FactionBossDetection.IsBossEnemy(enemy);
        // Do not clear _hostileToPlayerUntil here — start_fun re-registers every enemy and
        // would wipe broker-ambush hostility set at spawn. Use ClearHostilityToPlayer() explicitly.
    }

    /// <summary>
    /// Boss enemies always use vanilla combat AI; faction "peaceful" reputation must not freeze them.
    /// </summary>
    public static bool IsBossEnemy(GameObject enemyObject)
    {
        if (enemyObject == null)
            return false;

        int id = enemyObject.GetInstanceID();
        EnemyDate enemy = enemyObject.GetComponent<EnemyDate>();
        if (enemy == null)
            return false;

        // Re-resolve: vanilla sets BOSSflag after start_fun where we first register.
        bool isBoss = FactionBossDetection.IsBossEnemy(enemy);
        _isBossByInstanceId[id] = isBoss;
        return isBoss;
    }

    private static string FactionIdName(int factionId)
    {
        switch (factionId)
        {
            case FactionIds.Neutral: return "Neutral";
            case FactionIds.EventCoreEncounter: return "EventCoreEncounter";
            case FactionIds.Bandits: return "Bandits";
            case FactionIds.BanditsInquisitionLoyal: return "BanditsInquisitionLoyal";
            case FactionIds.BanditsMafiaLoyal: return "BanditsMafiaLoyal";
            case FactionIds.BanditsDemonsLoyal: return "BanditsDemonsLoyal";
            case FactionIds.Church: return "Church";
            case FactionIds.Demons: return "Demons";
            case FactionIds.Mafia: return "Mafia";
            case FactionIds.Undead: return "Undead";
            case FactionIds.Monsters: return "Monsters";
            case FactionIds.Witch: return "Witch";
            default: return "id=" + factionId;
        }
    }

    public static int GetFaction(GameObject enemyObject)
    {
        if (enemyObject == null)
            return 0;

        int value;
        if (_enemyFactionByInstanceId.TryGetValue(enemyObject.GetInstanceID(), out value))
            return value;

        return 0;
    }

    /// <summary>
    /// Explicitly set faction for an enemy object. Used for special cases like Witch offspring.
    /// </summary>
    public static void SetFaction(GameObject enemyObject, int factionId)
    {
        if (enemyObject == null)
            return;

        _enemyFactionByInstanceId[enemyObject.GetInstanceID()] = factionId;
    }

    public static bool IsBanditFamily(GameObject enemyObject)
    {
        return FactionIds.IsBanditFamily(GetFaction(enemyObject));
    }

    public static bool AreHostile(GameObject leftEnemyObject, GameObject rightEnemyObject)
    {
        if (leftEnemyObject == null || rightEnemyObject == null)
            return false;
        return AreHostile(GetFaction(leftEnemyObject), GetFaction(rightEnemyObject));
    }

    public static bool TryGetFactionTintColor(GameObject enemyObject, out Color color)
    {
        color = Color.white;
        int factionId = GetFaction(enemyObject);
        return TryGetFactionTintColor(factionId, out color);
    }

    /// <summary>
    /// Overload used by the reputation HUD to fetch a color without an EnemyDate instance in hand.
    /// </summary>
    public static bool TryGetFactionTintColor(int factionId, out Color color)
    {
        color = Color.white;
        if (FactionIds.IsPassiveNonCombat(factionId))
            return false;
        EnsureColorsLoaded();
        if (_colorByFaction.TryGetValue(factionId, out color))
        {
            // Guard against "configured but visually neutral" values (pure white) on key factions.
            if (IsNearWhite(color) && TryGetFallbackFactionColor(factionId, out Color fallback))
            {
                color = fallback;
            }
            return true;
        }

        if (TryGetFallbackFactionColor(factionId, out Color fallbackColor))
        {
            color = fallbackColor;
            return true;
        }

        return false;
    }

    public static void RemoveFaction(GameObject enemyObject)
    {
        if (enemyObject == null)
            return;

        int id = enemyObject.GetInstanceID();
        _enemyFactionByInstanceId.Remove(id);
        _isBossByInstanceId.Remove(id);
        _enemyByInstanceId.Remove(id);
        _hostileToPlayerUntil.Remove(id);
        _sessionHostileToPlayer.Remove(id);
        _currentTargetByEnemyId.Remove(id);
        _nextTargetRefreshAt.Remove(id);
        _baseMoveSpeedByEnemyId.Remove(id);
        _lastAppliedSpeedMultiplierByEnemyId.Remove(id);
        _prevAttackWindowState.Remove(id);
        _attackPulseUntil.Remove(id);
        _factionCombatCommitted.Remove(id);
        _passiveWaitByEnemyId.Remove(id);
    }

    public static void Reset()
    {
        _enemyFactionByInstanceId.Clear();
        _isBossByInstanceId.Clear();
        _enemyByInstanceId.Clear();
        _hostileToPlayerUntil.Clear();
        _sessionHostileToPlayer.Clear();
        _currentTargetByEnemyId.Clear();
        _nextTargetRefreshAt.Clear();
        _baseMoveSpeedByEnemyId.Clear();
        _lastAppliedSpeedMultiplierByEnemyId.Clear();
        _nextHitAtByPair.Clear();
        _prevAttackWindowState.Clear();
        _attackPulseUntil.Clear();
        _factionCombatCommitted.Clear();
        _passiveWaitByEnemyId.Clear();
        _relationByPair.Clear();
        _lastRelationReloadAt = -999f;
        _colorByFaction.Clear();
        _lastColorReloadAt = -999f;
    }

    public static bool TryGetNearestHostile(EnemyDate self, out EnemyDate target)
    {
        target = null;
        if (self == null || self.gameObject == null || self.Hp <= 0f)
            return false;

        int selfId = self.gameObject.GetInstanceID();
        int selfFaction = GetFaction(self.gameObject);
        if (FactionIds.IsPassiveNonCombat(selfFaction))
            return false;

        float now = Time.time;
        float refreshAt;
        _nextTargetRefreshAt.TryGetValue(selfId, out refreshAt);
        if (now >= refreshAt)
        {
            _nextTargetRefreshAt[selfId] = now + 0.2f;
            _currentTargetByEnemyId[selfId] = FindNearestHostileId(self, selfFaction);
        }

        int targetId;
        if (!_currentTargetByEnemyId.TryGetValue(selfId, out targetId))
            return false;

        EnemyDate resolved;
        if (!_enemyByInstanceId.TryGetValue(targetId, out resolved) || resolved == null || resolved.Hp <= 0f)
            return false;

        if (!IsFactionTargetWithinEngageRange(self, resolved))
        {
            _currentTargetByEnemyId[selfId] = 0;
            return false;
        }

        target = resolved;
        return true;
    }

    /// <summary>
    /// When a mob is hostile to the player but a hostile-faction enemy is closer (or combat is committed),
    /// redirect AI to the faction target instead of idling toward a far-away player.
    /// </summary>
    public static bool ShouldPreferFactionTargetOverPlayer(EnemyDate self)
    {
        if (self == null || self.gameObject == null)
            return false;
        if (IsFactionCombatCommitted(self.gameObject))
            return true;

        EnemyDate factionTarget;
        if (!TryGetNearestHostile(self, out factionTarget))
            return false;

        float dxFaction = Mathf.Abs(factionTarget.transform.position.x - self.transform.position.x);
        float dxPlayer;
        float dyPlayer;
        if (!TryGetRealPlayerOffset(self, out dxPlayer, out dyPlayer))
            dxPlayer = Mathf.Abs(self.distance);
        else
            dxPlayer = Mathf.Abs(dxPlayer);
        return dxFaction < dxPlayer;
    }

    /// <summary>
    /// When the enemy is also hostile to the player, only redirect to a faction brawl if the player
    /// left the activation bubble, combat is already committed, or a non-offspring faction target
    /// is closer than the player.
    /// </summary>
    public static bool ShouldEngageHostileFactionOverPlayer(EnemyDate self, float activationDistance)
    {
        if (self == null || self.gameObject == null)
            return false;
        if (IsFactionCombatCommitted(self.gameObject))
            return true;
        if (activationDistance > 0f && !IsPlayerWithinActivationZone(self, activationDistance))
            return true;

        EnemyDate factionTarget;
        if (!TryGetNearestHostile(self, out factionTarget))
            return false;
        if (WitchOffspringCombatRules.IsOffspring(factionTarget))
            return false;

        return ShouldPreferFactionTargetOverPlayer(self);
    }

    /// <summary>Most melee enemies only leave IDLE toward a target inside ~11m (see Mutude/SlaveBigAxe fun_Idle).</summary>
    private const float VanillaApproachSnapDistance = 10.5f;

    /// <summary>Point vanilla AI at the nearest hostile-faction enemy and mark both sides committed.</summary>
    public static bool TryRedirectToNearestHostileTarget(EnemyDate self)
    {
        if (self == null || self.gameObject == null)
            return false;

        EnemyDate target;
        if (!TryGetNearestHostile(self, out target))
        {
            ClearFactionCombatCommitted(self.gameObject);
            return false;
        }

        ExitPassiveWaitState(self);
        ApplyFactionTargetApproachFields(self, target);
        MarkFactionCombatCommitted(self, target);
        return true;
    }

    /// <summary>
    /// Re-apply faction approach fields at the end of enemy Update, after vanilla's
    /// "|distance| &gt; 12 → IDLE" gate has already run for this frame.
    /// </summary>
    public static void SustainFactionCombatApproach(EnemyDate self)
    {
        if (self == null || self.gameObject == null || self.Hp <= 0f)
            return;
        if (!EnemyFactionsConfig.Enable || IsBossEnemy(self.gameObject))
            return;
        if (ShouldRespectEventCorePassiveShell(self))
            return;
        if (self.com_player != null && self.com_player.erodown != 0)
            return;
        if (EnemyFactionsConfig.FreezeFactionAiDuringHScene &&
            self.com_player != null && self.com_player.eroflag)
            return;

        if (IsFactionCombatCommitted(self.gameObject))
        {
            TryRedirectToNearestHostileTarget(self);
            return;
        }

        // Do not start new inter-faction fights from Update sustain — only Distance_fun TryEngage
        // after the player enters the activation bubble (or pulse retaliation marks committed).
    }

    private static void ApplyFactionTargetApproachFields(EnemyDate self, EnemyDate target)
    {
        Vector3 attackerPos = self.transform.position;
        Vector3 targetPos = target.transform.position;
        float dx = targetPos.x - attackerPos.x;
        float dy = targetPos.y - attackerPos.y;

        // Vanilla Distance_fun postfixes run before Update's ">12 => IDLE" gate. Snap the
        // horizontal approach vector so church/demon AI actually walks in and attacks.
        if (Mathf.Abs(dx) > VanillaApproachSnapDistance)
            dx = Mathf.Sign(dx == 0f ? 1f : dx) * VanillaApproachSnapDistance;

        self.playerPos = attackerPos + new Vector3(dx, dy, 0f);
        self.distance = dx;
        self.distance_y = dy;
        self.Look = true;
        self.enmATKnow = true;
        if (self.Choose == 0)
            self.Choose = 1;
    }

    /// <summary>Victim retaliates after taking inter-faction damage (pulse or projectile).</summary>
    public static void NotifyFactionAttacked(EnemyDate victim, EnemyDate attacker)
    {
        if (victim == null || victim.gameObject == null || attacker == null || attacker.gameObject == null)
            return;
        if (victim.Hp <= 0f || attacker.Hp <= 0f)
            return;

        int victimId = victim.gameObject.GetInstanceID();
        int attackerId = attacker.gameObject.GetInstanceID();
        MarkFactionCombatCommitted(victim, attacker);
        _currentTargetByEnemyId[victimId] = attackerId;
        _nextTargetRefreshAt[victimId] = Time.time;
    }

    private static bool IsFactionTargetWithinEngageRange(EnemyDate self, EnemyDate candidate)
    {
        if (self == null || candidate == null)
            return false;

        float maxHorizontal = ResolveFactionTargetMaxHorizontal(self);
        float maxVertical = EnemyFactionsConfig.FactionInterTargetMaxVerticalDelta;
        float dx = candidate.transform.position.x - self.transform.position.x;
        float dy = candidate.transform.position.y - self.transform.position.y;
        if (maxHorizontal < float.MaxValue && Mathf.Abs(dx) > maxHorizontal)
            return false;
        if (maxVertical > 0f && Mathf.Abs(dy) > maxVertical)
            return false;
        return true;
    }

    public static bool IsHostileToPlayer(GameObject enemyObject)
    {
        if (enemyObject == null)
            return false;

        // Check for WitchOffspringController - Aradia's offspring are NEVER hostile to player
        if (enemyObject.GetComponent<WitchOffspringController>() != null)
            return false;

        // Witch (Aradia's offspring) are never hostile to the player
        int faction = GetFaction(enemyObject);
        if (faction == FactionIds.Witch)
            return false;

        int id = enemyObject.GetInstanceID();
        if (_sessionHostileToPlayer.Contains(id))
            return true;

        if (HasSpawnLockedHostility(enemyObject))
            return true;

        float until;
        if (!_hostileToPlayerUntil.TryGetValue(id, out until))
            return false;
        if (Time.time > until)
        {
            _hostileToPlayerUntil.Remove(id);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Broker ambush extras and revealed EventCore hosts stay hostile for the whole encounter.
    /// </summary>
    public static void MarkSessionHostileToPlayer(EnemyDate enemy)
    {
        if (enemy == null || enemy.gameObject == null)
            return;

        int id = enemy.gameObject.GetInstanceID();
        _sessionHostileToPlayer.Add(id);
        _hostileToPlayerUntil[id] = Time.time + 3600f;
    }

    private static bool HasSpawnLockedHostility(GameObject enemyObject)
    {
        SpawnManagedInstance managed = enemyObject.GetComponent<SpawnManagedInstance>();
        return managed != null && managed.SpawnHostileToPlayer;
    }

    /// <summary>
    /// Flag an enemy as hostile to the player for a very long duration — used by the
    /// reputation layer when the player's score drops below <see cref="EnemyFactionsConfig.ReputationHostileThreshold"/>.
    /// Runs every tick the enemy is inside the activation radius, so it effectively
    /// stays hostile as long as the hostile-level reputation holds.
    /// </summary>
    public static void MarkPermanentlyHostileToPlayer(EnemyDate enemy)
    {
        if (enemy == null || enemy.gameObject == null)
            return;
        int id = enemy.gameObject.GetInstanceID();
        // Long refresh, not float.MaxValue — lets the flag naturally decay once the
        // player moves out of range AND the reputation climbs above the threshold.
        _hostileToPlayerUntil[id] = Time.time + 60f;
        PreparePlayerCombatEngagement(enemy);
    }

    public static void ClearHostilityToPlayer(EnemyDate enemy)
    {
        if (enemy == null || enemy.gameObject == null)
            return;
        int id = enemy.gameObject.GetInstanceID();
        _hostileToPlayerUntil.Remove(id);
        _sessionHostileToPlayer.Remove(id);
    }

    public static void MarkProvokedByPlayer(EnemyDate victim)
    {
        if (victim == null || victim.gameObject == null)
            return;
        if (WitchOffspringCombatRules.IsOffspring(victim))
            return;
        if (!EnemyFactionsConfig.EnablePlayerProvocation)
            return;
        if (EnemyFactionsConfig.PlayerProvocationBanditsOnly &&
            !FactionIds.IsBanditFamily(GetFaction(victim.gameObject)))
            return;

        int victimFaction = GetFaction(victim.gameObject);
        if (FactionIds.IsPlayerNativeFaction(victimFaction))
            return;

        // Reputation Friendly: player's hit still nudges reputation down, but the
        // hostile-until timer stays clear. This keeps "allied" factions forgiving —
        // they can be angered over many hits instead of one.
        if (FactionReputationBehavior.ShouldBlockProvocation(victim.gameObject))
        {
            PlayerFactionReputation.NotifyPlayerAttackedFaction(victimFaction);
            if (EnemyFactionsConfig.DebugLogging)
                Plugin.Log?.LogInfo("[EnemyFactions] Provocation blocked by Friendly reputation for " + victim.GetType().Name);
            return;
        }

        int victimId = victim.gameObject.GetInstanceID();
        bool victimWasHostile = IsHostileToPlayer(victim.gameObject);
        float duration = Mathf.Max(1f, EnemyFactionsConfig.PlayerProvocationDurationSeconds);
        _hostileToPlayerUntil[victimId] = Time.time + duration;
        PreparePlayerCombatEngagement(victim);

        PlayerFactionReputation.NotifyPlayerAttackedFaction(victimFaction);

        float radius = Mathf.Max(0.1f, EnemyFactionsConfig.PlayerProvocationRadius);
        float radiusSq = radius * radius;

        foreach (KeyValuePair<int, EnemyDate> kvp in _enemyByInstanceId)
        {
            EnemyDate candidate = kvp.Value;
            if (candidate == null || candidate.gameObject == null || candidate.Hp <= 0f)
                continue;

            if (EnemyFactionsConfig.PlayerProvocationSameFactionOnly &&
                GetFaction(candidate.gameObject) != victimFaction)
                continue;

            float dx = candidate.transform.position.x - victim.transform.position.x;
            float dy = candidate.transform.position.y - victim.transform.position.y;
            float sq = dx * dx + dy * dy;
            if (sq <= radiusSq)
            {
                _hostileToPlayerUntil[candidate.gameObject.GetInstanceID()] = Time.time + duration;
                PreparePlayerCombatEngagement(candidate);
            }
        }

        if (EnemyFactionsConfig.DebugLogging && !victimWasHostile)
        {
            Plugin.Log?.LogInfo("[EnemyFactions] Player provoked " + victim.GetType().Name + " at radius " + radius.ToString("0.##") + " for " + duration.ToString("0.##") + "s");
        }
    }

    /// <summary>
    /// Phase A dynamics: scale enemy move speed from player's relation to that faction.
    /// Neutral relation keeps multiplier at 1.0. Hostile reputation speeds enemies up,
    /// friendly reputation slows them down.
    /// </summary>
    public static void ApplyRelationMoveSpeed(EnemyDate enemy)
    {
        if (enemy == null || enemy.gameObject == null)
            return;
        if (FactionBossDetection.IsBossEnemy(enemy))
            return;
        if (!EnemyFactionsConfig.EnableRelationSpeedScaling)
            return;

        int factionId = GetFaction(enemy.gameObject);
        if (FactionIds.IsPassiveNonCombat(factionId))
            return;

        int id = enemy.gameObject.GetInstanceID();
        float currentSpeed = enemy.enmMovespeed;
        if (currentSpeed <= 0f)
            return;

        float lastApplied = 1f;
        _lastAppliedSpeedMultiplierByEnemyId.TryGetValue(id, out lastApplied);
        if (lastApplied <= 0.001f)
            lastApplied = 1f;

        float inferredBaseSpeed = currentSpeed / lastApplied;
        if (inferredBaseSpeed <= 0f || float.IsNaN(inferredBaseSpeed) || float.IsInfinity(inferredBaseSpeed))
            inferredBaseSpeed = currentSpeed;
        _baseMoveSpeedByEnemyId[id] = inferredBaseSpeed;

        float score = Mathf.Clamp(PlayerFactionReputation.GetScore(factionId), -100f, 100f);
        float scoreNorm = score / 100f;
        float targetMultiplier;
        if (scoreNorm < 0f)
        {
            targetMultiplier = Mathf.Lerp(1f, EnemyFactionsConfig.SpeedMultiplierAtMinus100, -scoreNorm);
        }
        else
        {
            targetMultiplier = Mathf.Lerp(1f, EnemyFactionsConfig.SpeedMultiplierAtPlus100, scoreNorm);
        }

        float minClamp = Mathf.Max(0.01f, EnemyFactionsConfig.MinSpeedMultiplierClamp);
        float maxClamp = Mathf.Max(minClamp, EnemyFactionsConfig.MaxSpeedMultiplierClamp);
        targetMultiplier = Mathf.Clamp(targetMultiplier, minClamp, maxClamp);

        enemy.enmMovespeed = inferredBaseSpeed * targetMultiplier;
        _lastAppliedSpeedMultiplierByEnemyId[id] = targetMultiplier;
    }

    public static void ClearFactionCombatTarget(EnemyDate enemy)
    {
        if (enemy == null || enemy.gameObject == null)
            return;
        _currentTargetByEnemyId.Remove(enemy.gameObject.GetInstanceID());
    }

    /// <summary>
    /// Faction passivity leaves Choose=0 and may pin fake playerPos for vanilla AI.
    /// HellGate logic must use <see cref="IsInPassiveWaitState"/> and real-world geometry,
    /// not <see cref="EnemyDate.distance"/> while the passive flag is set.
    /// </summary>
    public static void RestoreVanillaPlayerApproach(EnemyDate enemy)
    {
        ExitPassiveWaitState(enemy);
        ApplyRealPlayerApproachFields(enemy);
    }

    /// <summary>
    /// Marks the enemy as passively waiting (ignores the player). Vanilla fields may still
    /// be faked for legacy AI; the explicit flag drives all HellGate distance/target checks.
    /// </summary>
    public static void EnterPassiveWaitState(EnemyDate enemy)
    {
        if (enemy == null || enemy.gameObject == null)
            return;

        _passiveWaitByEnemyId.Add(enemy.gameObject.GetInstanceID());
        ApplyVanillaPassiveSuppressionFields(enemy);
    }

    /// <summary>Clears faction passivity and restores real player targeting fields.</summary>
    public static void ExitPassiveWaitState(EnemyDate enemy)
    {
        if (enemy == null || enemy.gameObject == null)
            return;
        if (!_passiveWaitByEnemyId.Remove(enemy.gameObject.GetInstanceID()))
            return;

        ApplyRealPlayerApproachFields(enemy);
    }

    /// <summary>True when HellGate has marked this enemy as passively ignoring the player.</summary>
    public static bool IsInPassiveWaitState(EnemyDate enemy)
    {
        if (enemy == null || enemy.gameObject == null)
            return false;

        return _passiveWaitByEnemyId.Contains(enemy.gameObject.GetInstanceID());
    }

    private static void ApplyRealPlayerApproachFields(EnemyDate enemy)
    {
        if (enemy == null || enemy.gameObject == null || enemy.com_player == null)
            return;

        Transform playerTransform = enemy.com_player.transform;
        if (playerTransform == null)
            return;

        Vector3 selfPos = enemy.transform.position;
        Vector3 realPlayerPos = playerTransform.position;
        enemy.playerPos = realPlayerPos;
        enemy.distance = realPlayerPos.x - selfPos.x;
        enemy.distance_y = realPlayerPos.y - selfPos.y;
        enemy.Look = true;
        enemy.enmATKnow = true;

        if (enemy.Choose == 0)
            enemy.Choose = 1;

        if (!IsFactionCombatCommitted(enemy.gameObject))
            ClearFactionCombatTarget(enemy);
    }

    private static void ApplyVanillaPassiveSuppressionFields(EnemyDate enemy)
    {
        Vector3 selfPos = enemy.transform.position;
        float awaySign = 1f;
        if (enemy.com_player != null)
        {
            float dx = enemy.com_player.transform.position.x - selfPos.x;
            awaySign = dx >= 0f ? -1f : 1f;
        }

        // Vanilla Distance_fun reads these; HellGate must not use them while passive (see flag above).
        enemy.playerPos = selfPos + new Vector3(awaySign * 9999f, 0f, 0f);
        enemy.distance = 9999f;
        enemy.distance_y = 9999f;
        enemy.Look = false;
        enemy.enmATKnow = false;
        enemy.Choose = 0;

        Rigidbody2D body = enemy.GetComponent<Rigidbody2D>();
        if (body != null)
            body.velocity = new Vector2(0f, body.velocity.y);
    }

    /// <summary>Undo passive wait state so provoked or reputation-hostile enemies can engage the player.</summary>
    public static void PreparePlayerCombatEngagement(EnemyDate enemy)
    {
        if (enemy == null || enemy.gameObject == null)
            return;
        if (IsBossEnemy(enemy.gameObject))
            return;

        RestoreVanillaPlayerApproach(enemy);
    }

    /// <summary>Real offset to the player; always uses world geometry, never passive fake fields.</summary>
    public static bool TryGetRealPlayerOffset(EnemyDate self, out float dx, out float dy)
    {
        dx = 0f;
        dy = 0f;
        if (self == null)
            return false;

        if (self.com_player != null)
        {
            Vector3 selfPos = self.transform.position;
            Vector3 playerPos = self.com_player.transform.position;
            dx = playerPos.x - selfPos.x;
            dy = playerPos.y - selfPos.y;
            return true;
        }

        dx = self.distance;
        dy = self.distance_y;
        return true;
    }

    /// <summary>Whether the player is inside the faction activation bubble (real geometry).</summary>
    public static bool IsPlayerWithinActivationZone(EnemyDate self, float radius)
    {
        if (self == null || radius <= 0f)
            return true;

        float dx;
        float dy;
        TryGetRealPlayerOffset(self, out dx, out dy);

        if (EnemyFactionsConfig.ActivationDistanceHorizontalOnly)
            return Mathf.Abs(dx) <= radius;

        return (dx * dx + dy * dy) <= radius * radius;
    }

    /// <summary>
    /// Player activation bubble for <em>starting</em> inter-faction combat (horizontal + vertical caps from JSON).
    /// Committed fights are not gated here — see <see cref="CanBeginOrSustainFactionBrawl"/>.
    /// </summary>
    public static bool IsPlayerWithinFactionActivationBubble(EnemyDate self)
    {
        if (self == null)
            return false;

        float activationDistance = EnemyFactionsConfig.ActivationDistanceFromPlayer;
        if (activationDistance <= 0f)
            return true;

        if (!IsPlayerWithinActivationZone(self, activationDistance))
            return false;

        float verticalCap = EnemyFactionsConfig.ActivationMaxVerticalDelta;
        if (verticalCap <= 0f)
            return true;

        float dy;
        if (!TryGetRealPlayerOffset(self, out _, out dy))
            return true;

        return Mathf.Abs(dy) <= verticalCap;
    }

    /// <summary>
    /// New brawls require the player inside the activation bubble; committed pairs keep fighting after the player leaves.
    /// </summary>
    public static bool CanBeginOrSustainFactionBrawl(EnemyDate self)
    {
        if (self == null || self.gameObject == null)
            return false;
        if (IsFactionCombatCommitted(self.gameObject))
            return true;
        return IsPlayerWithinFactionActivationBubble(self);
    }

    /// <summary>
    /// EventCore broker / sex-paid shells stay passive until the encounter reveals hostility
    /// or the player provokes them during an armed ambush window.
    /// </summary>
    public static bool ShouldRespectEventCorePassiveShell(EnemyDate enemy)
    {
        if (enemy == null || enemy.gameObject == null)
            return false;

        EventCoreHost host = enemy.GetComponent<EventCoreHost>();
        if (host == null)
            return false;

        return host.ShouldForcePassive(IsHostileToPlayer(enemy.gameObject));
    }

    /// <summary>Redirect toward the nearest hostile-faction enemy and apply pulse damage if in range.</summary>
    public static bool TryEngageNearestHostileFaction(EnemyDate self)
    {
        if (self == null || self.gameObject == null || IsBossEnemy(self.gameObject))
            return false;
        if (ShouldRespectEventCorePassiveShell(self))
            return false;
        if (!CanBeginOrSustainFactionBrawl(self))
            return false;
        if (!TryRedirectToNearestHostileTarget(self))
            return false;

        TryApplyPulseDamage(self);
        return true;
    }

    /// <summary>
    /// Linear player-vision distance from relation score:
    /// -100 => VisionDistanceAtMinus100, +100 => VisionDistanceAtPlus100.
    /// </summary>
    public static float GetRelationVisionDistance(GameObject enemyObject)
    {
        if (enemyObject == null)
            return EnemyFactionsConfig.VisionDistanceAtMinus100;

        int factionId = GetFaction(enemyObject);
        if (FactionIds.IsPassiveNonCombat(factionId))
            return EnemyFactionsConfig.VisionDistanceAtMinus100;

        float score = Mathf.Clamp(PlayerFactionReputation.GetScore(factionId), -100f, 100f);
        float t = (score + 100f) / 200f; // -100..100 -> 0..1
        float maxVision = Mathf.Max(0.1f, EnemyFactionsConfig.VisionDistanceAtMinus100);
        float minVision = Mathf.Max(0.1f, EnemyFactionsConfig.VisionDistanceAtPlus100);
        return Mathf.Lerp(maxVision, minVision, t);
    }

    /// <summary>
    /// Pulse-based enemy-vs-enemy damage.
    /// The vanilla game has no physical collision path between two enemies
    /// (ChildColliderTrigger only listens to player ATK* tags, playerDamage only
    /// listens to the player's playerDAMAGEcol hurtbox). To emulate a hitbox we
    /// sample the attacker's own playerDamage collider bounds and test them
    /// against each hostile enemy's ChildColliderTrigger bounds during the
    /// attack window. Falls back to a proximity check if bounds are unavailable.
    /// </summary>
    public static void TryApplyPulseDamage(EnemyDate attacker)
    {
        try
        {
            if (attacker == null || attacker.gameObject == null || attacker.Hp <= 0f)
                return;
            if (EnemyFactionsConfig.DisableFactionDamageDuringHScene &&
                attacker.com_player != null && attacker.com_player.eroflag)
                return;
            if (!IsWithinAttackPulse(attacker))
                return;
            if (EnemyFactionsConfig.RequireAttackAnimationForFactionDamage &&
                !IsAttackAnimationActive(attacker))
                return;
            if (ShouldSkipPulseForProjectileAttack(attacker))
                return;
            if (!HasActiveAttackHitbox(attacker))
                return;

            int selfFaction = GetFaction(attacker.gameObject);
            if (FactionIds.IsPassiveNonCombat(selfFaction))
                return;

            Bounds? attackerHitbox = TryGetAttackerHitboxBounds(attacker);
            if (!attackerHitbox.HasValue)
                return;

            float verticalReach = ResolveFactionVerticalReach(attacker);
            float horizontalReach = ResolveFactionHorizontalReach(attacker);
            horizontalReach = ApplyLungeMeleeReachCap(attacker, horizontalReach);
            Vector2 attackerPos = attacker.transform.position;
            float now = Time.time;

            foreach (KeyValuePair<int, EnemyDate> kvp in _enemyByInstanceId)
            {
                EnemyDate candidate = kvp.Value;
                if (candidate == null || candidate == attacker || candidate.Hp <= 0f)
                    continue;
                if (!AreHostile(selfFaction, GetFaction(candidate.gameObject)))
                    continue;
                if (EnemyFactionsConfig.DisableFactionDamageDuringHScene &&
                    candidate.com_player != null && candidate.com_player.eroflag)
                    continue;

                Vector2 candidatePos = candidate.transform.position;
                float dx = Mathf.Abs(candidatePos.x - attackerPos.x);
                if (dx > horizontalReach)
                    continue;
                if (!EnemyFactionsConfig.FactionDamageHorizontalRangeOnly)
                {
                    float dy = Mathf.Abs(candidatePos.y - attackerPos.y);
                    if (dy > verticalReach)
                        continue;
                }

                if (!TryGetDefenderHurtboxBounds(candidate, out Bounds defenderBounds))
                    continue;

                Bounds meleeHitbox = ClampBoundsHorizontalFromRoot(
                    attackerHitbox.Value, attackerPos.x, horizontalReach);
                if (meleeHitbox.size.sqrMagnitude <= 0f || !meleeHitbox.Intersects(defenderBounds))
                    continue;

                int attackerId = attacker.gameObject.GetInstanceID();
                int targetId = candidate.gameObject.GetInstanceID();
                string pairKey = attackerId + "->" + targetId;
                float nextHitAt;
                _nextHitAtByPair.TryGetValue(pairKey, out nextHitAt);
                if (now < nextHitAt)
                    continue;
                _nextHitAtByPair[pairKey] = now + Mathf.Max(0.1f, EnemyFactionsConfig.HitCooldownSeconds);

                ApplyFactionDamage(attacker, candidate, "hitbox");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError("[EnemyFactions] TryApplyPulseDamage exception: " + ex);
        }
    }

    private static Bounds? TryGetAttackerHitboxBounds(EnemyDate attacker)
    {
        try
        {
            playerDamage[] triggers = attacker.GetComponentsInChildren<playerDamage>(false);
            if (triggers == null || triggers.Length == 0)
                return null;
            Bounds? result = null;
            for (int i = 0; i < triggers.Length; i++)
            {
                playerDamage trigger = triggers[i];
                if (trigger == null || !trigger.isActiveAndEnabled)
                    continue;
                Collider2D col = trigger.GetComponent<Collider2D>();
                if (col == null || !col.enabled)
                    continue;
                if (result.HasValue)
                {
                    Bounds b = result.Value;
                    b.Encapsulate(col.bounds);
                    result = b;
                }
                else
                {
                    result = col.bounds;
                }
            }
            return result;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryGetDefenderHurtboxBounds(EnemyDate defender, out Bounds bounds)
    {
        bounds = default(Bounds);
        try
        {
            ChildColliderTrigger[] triggers = defender.GetComponentsInChildren<ChildColliderTrigger>(false);
            if (triggers == null || triggers.Length == 0)
                return false;
            bool has = false;
            for (int i = 0; i < triggers.Length; i++)
            {
                ChildColliderTrigger trigger = triggers[i];
                if (trigger == null || !trigger.isActiveAndEnabled)
                    continue;
                Collider2D col = trigger.GetComponent<Collider2D>();
                if (col == null || !col.enabled)
                    continue;
                if (has)
                    bounds.Encapsulate(col.bounds);
                else
                {
                    bounds = col.bounds;
                    has = true;
                }
            }
            return has;
        }
        catch
        {
            return false;
        }
    }

    private static bool HasActiveAttackHitbox(EnemyDate attacker)
    {
        try
        {
            playerDamage[] triggers = attacker.GetComponentsInChildren<playerDamage>(false);
            if (triggers == null || triggers.Length == 0)
                return false;

            for (int i = 0; i < triggers.Length; i++)
            {
                playerDamage trigger = triggers[i];
                if (trigger == null || !trigger.isActiveAndEnabled)
                    continue;
                Collider2D col = trigger.GetComponent<Collider2D>();
                if (col != null && col.enabled)
                    return true;
            }
        }
        catch
        {
            // ignored
        }

        return false;
    }

    private static bool IsAttackAnimationActive(EnemyDate attacker)
    {
        try
        {
            string anim = TryGetAnimationName(attacker);
            if (string.IsNullOrEmpty(anim))
                return false;

            if (anim.StartsWith("ATK", StringComparison.OrdinalIgnoreCase) ||
                anim.StartsWith("ATTACK", StringComparison.OrdinalIgnoreCase))
                return true;
            if (anim.StartsWith("STAB", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static string TryGetAnimationName(EnemyDate attacker)
    {
        if (attacker == null)
            return string.Empty;

        FieldInfo spineField = AccessTools.Field(attacker.GetType(), "mySpine") ?? AccessTools.Field(attacker.GetType(), "myspine");
        if (spineField == null)
            return string.Empty;

        SkeletonAnimation spine = spineField.GetValue(attacker) as SkeletonAnimation;
        return spine != null ? spine.AnimationName ?? string.Empty : string.Empty;
    }

    private static bool ShouldSkipPulseForProjectileAttack(EnemyDate attacker)
    {
        string anim = TryGetAnimationName(attacker);
        if (string.IsNullOrEmpty(anim))
            return false;

        if (anim.StartsWith("SHOOT", StringComparison.OrdinalIgnoreCase) ||
            anim.IndexOf("ARROW", StringComparison.OrdinalIgnoreCase) >= 0 ||
            anim.StartsWith("ATKDOWN", StringComparison.OrdinalIgnoreCase) ||
            anim.StartsWith("ATKUP", StringComparison.OrdinalIgnoreCase))
            return true;

        string typeName = attacker.GetType().Name;
        if ((typeName == "SinnerslaveCrossbow" || typeName == "Dorei") &&
            anim.StartsWith("ATK", StringComparison.OrdinalIgnoreCase) &&
            !anim.StartsWith("ATK3", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static float ResolveFactionVerticalReach(EnemyDate attacker)
    {
        float configured = EnemyFactionsConfig.FactionDamageMaxVerticalDelta;
        if (configured > 0f)
            return configured;

        float horizontal = ResolveFactionHorizontalReach(attacker);
        float fromAtk = attacker != null && attacker.Atkdistance > 0f ? attacker.Atkdistance + 1f : 2.5f;
        return Mathf.Min(fromAtk, horizontal + 1.5f);
    }

    /// <summary>
    /// Faction melee uses <see cref="EnemyFactionsConfig.BanditsVsDemonsRange"/> as the hard cap.
    /// Vanilla Atkdistance is often a chase/leash radius (bosses can be ~9+) and must not widen melee.
    /// </summary>
    private static float ResolveFactionHorizontalReach(EnemyDate attacker)
    {
        float configured = Mathf.Max(0.5f, EnemyFactionsConfig.BanditsVsDemonsRange);
        float typeCap = EnemyFactionsConfig.TryGetMeleeReachOverride(attacker);
        if (typeCap > 0f)
            configured = Mathf.Min(configured, typeCap);
        if (attacker != null && attacker.Atkdistance > 0f)
            return Mathf.Min(configured, attacker.Atkdistance + 0.25f);

        return configured;
    }

    /// <summary>
    /// MummyDog/Wolf pick ATK3/ATK4 from 3–6 units away; faction pulse must not land before the lunge closes.
    /// </summary>
    private static float ApplyLungeMeleeReachCap(EnemyDate attacker, float baseReach)
    {
        if (attacker == null)
            return baseReach;

        string anim = TryGetAnimationName(attacker);
        if (anim != "ATK3" && anim != "ATK4")
            return baseReach;

        string typeName = attacker.GetType().Name;
        string objectName = attacker.gameObject != null ? attacker.gameObject.name : string.Empty;
        if (typeName != "MummyDog" && objectName != "Wolf")
            return baseReach;

        float lungeCap = EnemyFactionsConfig.TryGetMeleeReachOverride(attacker);
        if (lungeCap <= 0f)
            lungeCap = 3f;
        return Mathf.Min(baseReach, lungeCap);
    }

    /// <summary>
    /// Weapon colliders can extend far beyond the body; clamp them to melee reach from the attacker's root.
    /// </summary>
    private static Bounds ClampBoundsHorizontalFromRoot(Bounds bounds, float rootX, float maxReach)
    {
        float minX = rootX - maxReach;
        float maxX = rootX + maxReach;
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        if (min.x < minX)
            min.x = minX;
        if (max.x > maxX)
            max.x = maxX;
        if (min.x >= max.x)
            return new Bounds(new Vector3(rootX, bounds.center.y, bounds.center.z), Vector3.zero);

        return new Bounds((min + max) * 0.5f, max - min);
    }

    private static bool IsWithinAttackPulse(EnemyDate attacker)
    {
        if (attacker == null || attacker.gameObject == null)
            return false;

        int id = attacker.gameObject.GetInstanceID();
        float now = Time.time;
        float pulseDuration = Mathf.Max(0.35f, EnemyFactionsConfig.HitCooldownSeconds);

        if (IsAttackAnimationActive(attacker))
            _attackPulseUntil[id] = now + pulseDuration;

        float pulseUntil;
        if (_attackPulseUntil.TryGetValue(id, out pulseUntil) && now <= pulseUntil)
            return true;

        return false;
    }

    private static void TryShowDamagePopup(EnemyDate target, float hitDamage)
    {
        if (target == null || target.gameObject == null)
            return;
        if (DamagePopMethod == null)
            return;

        try
        {
            DamagePopMethod.Invoke(target, new object[] { Mathf.Max(0f, hitDamage) });

            // Vanilla DamagePOP_fun sets EnemyDate.damePOP to the freshly-spawned
            // popup GameObject. Shrink it so faction hits look visually smaller
            // than player's own damage numbers and do not flood the screen.
            float scale = Mathf.Clamp(EnemyFactionsConfig.FactionDamagePopupScale, 0.2f, 1f);
            if (scale < 0.999f && target.damePOP != null)
            {
                target.damePOP.transform.localScale *= scale;
            }
        }
        catch
        {
            // Silently ignore popup failures: combat logic should remain stable.
        }
    }

    private static void ApplyFactionDamage(EnemyDate attacker, EnemyDate target, string source)
    {
        float hitDamage = ResolveAttackerDamage(attacker);
        target.Hp -= Mathf.Max(0f, hitDamage);
        NotifyFactionAttacked(target, attacker);
        TryShowDamagePopup(target, hitDamage);
        if (EnemyFactionsConfig.DebugLogging)
        {
            float dx = attacker != null && target != null
                ? Mathf.Abs(attacker.transform.position.x - target.transform.position.x)
                : 0f;
            Plugin.Log?.LogInfo("[EnemyFactions] Hit(" + source + ") dx=" + dx.ToString("0.##") +
                " " + attacker.GetType().Name + " -> " + target.GetType().Name + " for " +
                hitDamage.ToString("0.##") + " (target HP: " + target.Hp.ToString("0.##") + ")");
        }
    }

    public static void ApplyProjectileFactionDamage(EnemyDate attacker, EnemyDate target, float damage, string projectileSource)
    {
        if (attacker == null || target == null || attacker.gameObject == null || target.gameObject == null)
            return;
        if (attacker == target || attacker.Hp <= 0f || target.Hp <= 0f)
            return;
        if (!AreHostile(attacker.gameObject, target.gameObject))
            return;
        if (EnemyFactionsConfig.DisableFactionDamageDuringHScene &&
            ((attacker.com_player != null && attacker.com_player.eroflag) ||
             (target.com_player != null && target.com_player.eroflag)))
            return;

        float hitDamage = damage > 0f ? damage : ResolveAttackerDamage(attacker);
        target.Hp -= Mathf.Max(0f, hitDamage);
        NotifyFactionAttacked(target, attacker);
        TryShowDamagePopup(target, hitDamage);
        if (EnemyFactionsConfig.DebugLogging)
        {
            Plugin.Log?.LogInfo("[EnemyFactions] Hit(" + projectileSource + ") " + attacker.GetType().Name + " -> " + target.GetType().Name + " for " + hitDamage.ToString("0.##") + " (target HP: " + target.Hp.ToString("0.##") + ")");
        }
    }

    /// <summary>
    /// Resolve damage from attacker's actual stats, falling back to the configured per-family value
    /// and finally to a sane default. This keeps every enemy doing its own vanilla damage in faction fights.
    /// </summary>
    private static float ResolveAttackerDamage(EnemyDate attacker)
    {
        if (attacker != null && attacker.enmATK > 0f)
            return attacker.enmATK;

        int attackerFaction = attacker != null && attacker.gameObject != null ? GetFaction(attacker.gameObject) : FactionNeutral;
        if (attackerFaction == FactionDemons && EnemyFactionsConfig.DemonsDamagePerHit > 0f)
            return EnemyFactionsConfig.DemonsDamagePerHit;
        if (EnemyFactionsConfig.BanditsDamagePerHit > 0f)
            return EnemyFactionsConfig.BanditsDamagePerHit;
        return 8f;
    }

    internal static bool TryGetEnemyByInstanceId(int instanceId, out EnemyDate enemy)
    {
        return _enemyByInstanceId.TryGetValue(instanceId, out enemy);
    }

    internal static IEnumerable<KeyValuePair<int, EnemyDate>> EnumerateEnemies()
    {
        return _enemyByInstanceId;
    }

    /// <summary>
    /// Public access to the config-list based type-name resolution (Factions.json type lists).
    /// Returns <see cref="FactionIds.Neutral"/> for unknown names. Used by the Pregnancy module
    /// to classify ERO-only objects whose runtime instance was never registered.
    /// </summary>
    public static int ResolveFactionByTypeName(string enemyTypeName)
    {
        return ResolveFactionId(enemyTypeName);
    }

    private static int ResolveFactionId(string enemyTypeName)
    {
        if (ContainsTypeName(EnemyFactionsConfig.BanditTypes, enemyTypeName))
            return FactionIds.Bandits;
        if (ContainsTypeName(EnemyFactionsConfig.DemonTypes, enemyTypeName))
            return FactionIds.Demons;
        if (ContainsTypeName(EnemyFactionsConfig.ChurchTypes, enemyTypeName))
            return FactionIds.Church;
        if (ContainsTypeName(EnemyFactionsConfig.MafiaTypes, enemyTypeName))
            return FactionIds.Mafia;
        if (ContainsTypeName(EnemyFactionsConfig.UndeadTypes, enemyTypeName))
            return FactionIds.Undead;
        if (ContainsTypeName(EnemyFactionsConfig.MonsterTypes, enemyTypeName))
            return FactionIds.Monsters;
        if (ContainsTypeName(EnemyFactionsConfig.NeutralTypes, enemyTypeName))
            return FactionIds.Neutral;
        if (EnemyFactionsConfig.DebugLogging)
            Plugin.Log?.LogInfo("[EnemyFactions] Unknown enemy type '" + enemyTypeName + "', assigned Neutral. Add to Factions.json to enable.");
        return FactionIds.Neutral;
    }

    private static bool ContainsTypeName(string[] names, string typeName)
    {
        if (names == null || string.IsNullOrEmpty(typeName))
            return false;

        for (int i = 0; i < names.Length; i++)
        {
            if (string.Equals(names[i], typeName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static int FindNearestHostileId(EnemyDate self, int selfFaction)
    {
        float bestSq = float.MaxValue;
        int bestId = 0;
        float maxHorizontal = ResolveFactionTargetMaxHorizontal(self);
        float maxVertical = EnemyFactionsConfig.FactionInterTargetMaxVerticalDelta;
        bool horizontalOnly = maxVertical <= 0f;
        foreach (KeyValuePair<int, EnemyDate> kvp in _enemyByInstanceId)
        {
            EnemyDate candidate = kvp.Value;
            if (candidate == null || candidate == self || candidate.Hp <= 0f)
                continue;

            int candidateFaction = GetFaction(candidate.gameObject);
            if (!AreHostile(selfFaction, candidateFaction))
                continue;

            float dx = candidate.transform.position.x - self.transform.position.x;
            float dy = candidate.transform.position.y - self.transform.position.y;
            if (maxHorizontal < float.MaxValue && Mathf.Abs(dx) > maxHorizontal)
                continue;
            if (maxVertical > 0f && Mathf.Abs(dy) > maxVertical)
                continue;

            float sq = horizontalOnly ? dx * dx : dx * dx + dy * dy;
            if (sq < bestSq)
            {
                bestSq = sq;
                bestId = kvp.Key;
            }
        }

        return bestId;
    }

    private static float ResolveFactionTargetMaxHorizontal(EnemyDate self)
    {
        if (self != null && self.gameObject != null && IsFactionCombatCommitted(self.gameObject))
            return float.MaxValue;

        float configured = EnemyFactionsConfig.FactionInterTargetMaxHorizontalDistance;
        if (configured <= 0f)
            return float.MaxValue;

        float typeCap = EnemyFactionsConfig.TryGetFactionTargetRangeOverride(self);
        if (typeCap > 0f)
            return Mathf.Min(configured, typeCap);

        return configured;
    }

    private static bool AreHostile(int leftFaction, int rightFaction)
    {
        if (leftFaction == rightFaction)
        {
            if (WitchOffspringCombatRules.ShouldBlockWitchFactionFriendlyFire(leftFaction, rightFaction))
                return false;
            if (leftFaction == FactionIds.Witch)
                return true;
            return false;
        }
        if (FactionIds.IsPassiveNonCombat(leftFaction) || FactionIds.IsPassiveNonCombat(rightFaction))
            return false;
        // Witch (Aradia's offspring) are hostile to all active combat factions (but never to player)
        bool leftIsWitch = leftFaction == FactionIds.Witch;
        bool rightIsWitch = rightFaction == FactionIds.Witch;
        if (leftIsWitch || rightIsWitch)
            return true; // Witch attacks everyone except player (handled in IsHostileToPlayer) and neutrals
        if (!EnemyFactionsConfig.EnableFriendlyFire && IsSameFactionFamily(leftFaction, rightFaction))
            return false;
        int relation = GetRelation(leftFaction, rightFaction);
        if (relation == RelationHostile)
            return true;
        if (relation == RelationFriendly)
            return false;
        return true; // default policy for active non-neutral factions
    }

    private static bool IsSameFactionFamily(int leftFaction, int rightFaction)
    {
        if (FactionIds.IsBanditFamily(leftFaction) && FactionIds.IsBanditFamily(rightFaction))
            return true;
        if (leftFaction == rightFaction && !FactionIds.IsPassiveNonCombat(leftFaction))
            return true;
        return false;
    }

    private static int GetRelation(int leftFaction, int rightFaction)
    {
        EnsureRelationsLoaded();
        long key = ComposePairKey(leftFaction, rightFaction);
        int relation;
        if (_relationByPair.TryGetValue(key, out relation))
            return relation;
        return RelationNeutral;
    }

    private static void EnsureRelationsLoaded()
    {
        float now = Time.realtimeSinceStartup;
        if (now - _lastRelationReloadAt < 2f)
            return;

        _lastRelationReloadAt = now;
        _relationByPair.Clear();
        EnemyFactionsConfig.FactionRelationEntry[] entries = EnemyFactionsConfig.FactionRelations;
        if (entries == null || entries.Length == 0)
            return;

        for (int i = 0; i < entries.Length; i++)
        {
            EnemyFactionsConfig.FactionRelationEntry entry = entries[i];
            if (entry == null)
                continue;
            if (!FactionIds.TryParse(entry.Left, out int leftFaction))
                continue;
            if (!FactionIds.TryParse(entry.Right, out int rightFaction))
                continue;
            int relation = ParseRelation(entry.Relation);
            long key = ComposePairKey(leftFaction, rightFaction);
            _relationByPair[key] = relation;
        }
    }

    private static void EnsureColorsLoaded()
    {
        float now = Time.realtimeSinceStartup;
        if (now - _lastColorReloadAt < 2f)
            return;

        _lastColorReloadAt = now;
        _colorByFaction.Clear();
        EnemyFactionsConfig.FactionColorEntry[] entries = EnemyFactionsConfig.FactionColors;
        if (entries == null || entries.Length == 0)
            return;

        for (int i = 0; i < entries.Length; i++)
        {
            EnemyFactionsConfig.FactionColorEntry entry = entries[i];
            if (entry == null)
                continue;
            if (!FactionIds.TryParse(entry.Faction, out int factionId))
                continue;
            if (string.IsNullOrEmpty(entry.Color))
                continue;
            if (!ColorUtility.TryParseHtmlString(entry.Color, out Color parsed))
                continue;
            _colorByFaction[factionId] = parsed;
        }
    }

    private static int ParseRelation(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return RelationNeutral;
        if (raw.Equals("hostile", StringComparison.OrdinalIgnoreCase))
            return RelationHostile;
        if (raw.Equals("friendly", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("allied", StringComparison.OrdinalIgnoreCase))
            return RelationFriendly;
        return RelationNeutral;
    }

    private static long ComposePairKey(int leftFaction, int rightFaction)
    {
        int a = Mathf.Min(leftFaction, rightFaction);
        int b = Mathf.Max(leftFaction, rightFaction);
        return ((long)(uint)a << 32) | (uint)b;
    }

    private static bool TryGetFallbackFactionColor(int factionId, out Color color)
    {
        // Palette is intentionally spread across the hue wheel so that any two
        // factions are never visually close. These mirror Factions.json defaults
        // and act as a safety net when the JSON color section is missing.
        color = Color.white;
        if (factionId == FactionIds.Bandits)
        {
            color = new Color(1.00f, 0.56f, 0.12f, 1f); // orange
            return true;
        }
        if (factionId == FactionIds.BanditsInquisitionLoyal)
        {
            color = new Color(0.31f, 0.76f, 0.97f, 1f); // sky blue
            return true;
        }
        if (factionId == FactionIds.BanditsMafiaLoyal)
        {
            color = new Color(1.00f, 0.76f, 0.03f, 1f); // amber
            return true;
        }
        if (factionId == FactionIds.BanditsDemonsLoyal)
        {
            color = new Color(0.90f, 0.22f, 0.21f, 1f); // crimson
            return true;
        }
        if (factionId == FactionIds.Church)
        {
            color = new Color(0.93f, 0.94f, 0.95f, 1f); // near-white / silver
            return true;
        }
        if (factionId == FactionIds.Demons)
        {
            color = new Color(0.48f, 0.12f, 0.64f, 1f); // deep purple
            return true;
        }
        if (factionId == FactionIds.Mafia)
        {
            color = new Color(0.16f, 0.21f, 0.58f, 1f); // dark indigo
            return true;
        }
        if (factionId == FactionIds.Undead)
        {
            color = new Color(0.46f, 1.00f, 0.01f, 1f); // toxic lime
            return true;
        }
        if (factionId == FactionIds.Monsters)
        {
            color = new Color(0.43f, 0.30f, 0.25f, 1f); // warm brown
            return true;
        }
        if (factionId == FactionIds.Witch)
        {
            color = new Color(0.72f, 0.28f, 0.92f, 1f); // violet (Aradia's faction)
            return true;
        }
        return false;
    }

    private static bool IsNearWhite(Color color)
    {
        return color.r > 0.97f && color.g > 0.97f && color.b > 0.97f;
    }
}
