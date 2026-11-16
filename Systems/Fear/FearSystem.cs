using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using NoREroMod.Patches.HellTraps;
using NoREroMod.Systems.CombatAi.Factions;
using NoREroMod.Systems.GrabSystem;
using NoREroMod.Systems.Rage;
using UnityEngine;

namespace NoREroMod.Systems.Fear;

/// <summary>
/// Morale axis [-100, +100]. Single ApplyDelta entry; negative Δ = more fear.
/// </summary>
internal static class FearSystem
{
    private const float MoraleMin = -100f;
    private const float MoraleMax = 100f;

    private static float _morale;
    private static bool _initialized;

    /// <summary>True while post-vengeance trauma (rise → hold → floor) is driving Morale.</summary>
    private static bool _traumaRunning;
    private static Coroutine _traumaCoroutine;

    internal static bool IsVengeanceTraumaRunning => _traumaRunning;

    // Block → counter window
    private static float _blockWindowUntil = -1f;
    private static bool _blockWasHeavy;
    private static bool _counterAwardedThisWindow;

    // Combo every 5 hits
    private static int _comboHits;
    private static float _lastComboHitTime = -999f;
    private const float ComboTimeout = 2.5f;

    // Kill dedupe
    private static readonly HashSet<int> _registeredKills = new HashSet<int>();
    private static float _lastHeavyHitTime = -999f;
    private static float _lastHeavyAppliedMag;
    private static float _lastKnockdownTime = -999f;
    private static int _lastErodown;
    private static bool _wasHSceneActive;
    private static float _lastHStartSpikeTime = -999f;
    private static float _lastFinSpikeTime = -999f;

    // Dash SP cost baseline
    private static float _cachedBaseStepsp = -1f;
    private static bool _hasCachedStepsp;

    private static FieldInfoCache _fields;

    internal static event Action? OnChanged;
    internal static event Action<float, float>? OnMoraleChanged;

    internal static bool Enabled => FearConfig.IsEnabled;
    internal static float Morale => _morale;

    internal static float PanicT
    {
        get
        {
            float band = FearConfig.PanicBandThreshold?.Value ?? -50f;
            if (_morale > band) return 0f;
            float span = MoraleMin - band;
            if (Mathf.Abs(span) < 0.001f) return 1f;
            return Mathf.Clamp01((_morale - band) / span);
        }
    }

    internal static float ConfidenceT
    {
        get
        {
            float band = FearConfig.ConfidenceBandThreshold?.Value ?? 50f;
            if (_morale < band) return 0f;
            float span = MoraleMax - band;
            if (Mathf.Abs(span) < 0.001f) return 1f;
            return Mathf.Clamp01((_morale - band) / span);
        }
    }

    internal static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        _morale = 0f;
        _fields = new FieldInfoCache();

        try
        {
            RageSystem.OnActivated += OnRageActivated;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[Fear] Failed to subscribe Rage.OnActivated: " + ex.Message);
        }

        Plugin.Log?.LogInfo("[Fear] FearSystem initialized (Enable=" + Enabled + ")");
    }

    internal static void ResetState()
    {
        float old = _morale;
        ResetRuntimeState();
        _morale = 0f;
        if (!Mathf.Approximately(old, _morale))
        {
            try { OnMoraleChanged?.Invoke(old, _morale); } catch { }
            try { OnChanged?.Invoke(); } catch { }
        }
    }

    /// <summary>
    /// Clears transient combat/H/dash caches without changing Morale.
    /// Call before RestorePersistedMorale on load so windows/dedupe do not leak across sessions.
    /// </summary>
    internal static void ResetRuntimeState()
    {
        _comboHits = 0;
        _lastComboHitTime = -999f;
        _blockWindowUntil = -1f;
        _blockWasHeavy = false;
        _counterAwardedThisWindow = false;
        _registeredKills.Clear();
        _lastHeavyHitTime = -999f;
        _lastHeavyAppliedMag = 0f;
        _lastKnockdownTime = -999f;
        _lastErodown = 0;
        _wasHSceneActive = false;
        _lastHStartSpikeTime = -999f;
        _lastFinSpikeTime = -999f;
        _cachedBaseStepsp = -1f;
        _hasCachedStepsp = false;
        if (_traumaRunning && _traumaCoroutine != null)
        {
            try
            {
                if (Plugin.Instance != null)
                    Plugin.Instance.StopCoroutine(_traumaCoroutine);
            }
            catch { }
        }
        _traumaRunning = false;
        _traumaCoroutine = null;
        try { Patches.FearCombatHooks.ClearKillDedupe(); } catch { }
        try { FearHeartbeatAudio.StopAll(); } catch { }
    }

    internal static void RestorePersistedMorale(float value)
    {
        ResetRuntimeState();
        float old = _morale;
        _morale = Mathf.Clamp(value, MoraleMin, MoraleMax);
        if (!Mathf.Approximately(old, _morale))
        {
            try { OnMoraleChanged?.Invoke(old, _morale); } catch { }
            try { OnChanged?.Invoke(); } catch { }
        }
    }

    /// <summary>Apply ΔMorale. Negative Δ scaled by PrideFall when Morale &gt; 0.</summary>
    internal static void ApplyDelta(float delta, string reason)
    {
        if (!Enabled) return;
        if (Mathf.Abs(delta) < 0.0001f) return;

        float applied = delta;
        if (applied < 0f && _morale > 0f)
        {
            float pride = FearConfig.PrideFallMult?.Value ?? 0.5f;
            applied *= 1f + (_morale / 100f) * pride;
        }

        float old = _morale;
        _morale = Mathf.Clamp(_morale + applied, MoraleMin, MoraleMax);
        if (Mathf.Approximately(old, _morale)) return;

        try { OnMoraleChanged?.Invoke(old, _morale); } catch { }
        try { OnChanged?.Invoke(); } catch { }
    }

    /// <summary>
    /// Ordinary death Take Vengeance (no trap/fatality pending): snap Morale to NormalRespawnMorale.
    /// </summary>
    internal static void ResetOnNormalVengeanceRespawn()
    {
        if (!Enabled || _traumaRunning)
            return;

        float target = FearConfig.NormalRespawnMorale?.Value ?? -10f;
        target = Mathf.Clamp(target, MoraleMin, MoraleMax);
        float old = _morale;
        if (Mathf.Approximately(old, target))
            return;

        SetMoraleDirect(target);
        Plugin.Log?.LogInfo(
            "[Fear] Normal vengeance respawn — Morale "
            + old.ToString("0.#")
            + " -> "
            + target.ToString("0.#"));
    }

    /// <summary>
    /// Parallel to lethal-trap MB shock: Morale → −100 (rise), hold, then settle to LethalRespawnFloor.
    /// Called when trap/fatality pending is consumed on Take Vengeance.
    /// </summary>
    internal static void TryStartVengeanceTraumaSequence()
    {
        if (!Enabled)
            return;
        if (_traumaRunning)
            return;
        if (Plugin.Instance == null)
            return;

        if (_traumaCoroutine != null)
        {
            Plugin.Instance.StopCoroutine(_traumaCoroutine);
            _traumaCoroutine = null;
        }

        _traumaCoroutine = Plugin.Instance.StartCoroutine(VengeanceTraumaCoroutine());
    }

    /// <summary>Altar rest: cancel trauma and snap Morale to 0 (neutral).</summary>
    internal static void ResetOnAltarRest()
    {
        if (!Enabled)
            return;
        StopVengeanceTraumaIfRunning();
        float old = _morale;
        SetMoraleDirect(0f, "altar_rest");
        Plugin.Log?.LogInfo("[Fear] Altar rest — Morale " + old.ToString("0.#") + " -> 0");
    }

    internal static void StopVengeanceTraumaIfRunning()
    {
        if (!_traumaRunning && _traumaCoroutine == null)
            return;

        if (Plugin.Instance != null && _traumaCoroutine != null)
            Plugin.Instance.StopCoroutine(_traumaCoroutine);

        _traumaCoroutine = null;
        _traumaRunning = false;
    }

    private static IEnumerator VengeanceTraumaCoroutine()
    {
        _traumaRunning = true;

        float floor = FearConfig.LethalRespawnFloor?.Value ?? -30f;
        floor = Mathf.Clamp(floor, MoraleMin, MoraleMax);
        float peak = MoraleMin;
        float start = _morale;

        Plugin.Log?.LogInfo(
            "[Fear] Vengeance trauma — Morale "
            + start.ToString("0.#")
            + " -> "
            + peak.ToString("0.#")
            + " then floor "
            + floor.ToString("0.#"));

        float riseDuration = LethalTrapVengeanceShockTuning.RiseToMaxSeconds;
        float riseElapsed = 0f;
        while (riseElapsed < riseDuration)
        {
            riseElapsed += Time.deltaTime;
            float tRaw = riseDuration > 0f ? Mathf.Clamp01(riseElapsed / riseDuration) : 1f;
            float t = SmoothStep01(tRaw);
            SetMoraleDirect(Mathf.Lerp(start, peak, t));
            yield return null;
        }

        SetMoraleDirect(peak);

        float holdElapsed = 0f;
        while (holdElapsed < LethalTrapVengeanceShockTuning.HoldAtMaxSeconds)
        {
            holdElapsed += Time.deltaTime;
            SetMoraleDirect(peak);
            yield return null;
        }

        float decayDuration = LethalTrapVengeanceShockTuning.DecayToFloorSeconds;
        float decayElapsed = 0f;
        while (decayElapsed < decayDuration)
        {
            decayElapsed += Time.deltaTime;
            float tRaw = decayDuration > 0f ? Mathf.Clamp01(decayElapsed / decayDuration) : 1f;
            float t = SmoothStep01(tRaw);
            SetMoraleDirect(Mathf.Lerp(peak, floor, t));
            yield return null;
        }

        SetMoraleDirect(floor);

        Plugin.Log?.LogInfo(
            "[Fear] Vengeance trauma complete — Morale "
            + _morale.ToString("0.#"));

        _traumaRunning = false;
        _traumaCoroutine = null;
    }

    private static void SetMoraleDirect(float value)
    {
        float old = _morale;
        _morale = Mathf.Clamp(value, MoraleMin, MoraleMax);
        if (Mathf.Approximately(old, _morale))
            return;
        try { OnMoraleChanged?.Invoke(old, _morale); } catch { }
        try { OnChanged?.Invoke(); } catch { }
    }

    /// <summary>Scripted H-phase set (bypasses PrideFall): absolute JSON beats and High Inquisition Female ramp.</summary>
    internal static void SetMoraleDirect(float value, string reason)
    {
        if (!Enabled) return;
        SetMoraleDirect(value);
    }

    private static float SmoothStep01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    internal static void NotifyRageActivation()
    {
        ApplyDelta(FearConfig.RageActivationMorale?.Value ?? 5f, "rage_activation");
    }

    internal static void NotifyVengeanceStrikeSpend()
    {
        if (!Enabled) return;
        ApplyDelta(FearConfig.VengeanceStrikeMorale?.Value ?? 15f, "vengeance_strike");
    }

    internal static void NotifyStruggleQteSuccess()
    {
        ApplyDelta(FearConfig.StruggleQteMorale?.Value ?? 2f, "struggle_qte");
    }

    internal static void NotifyGrabLand()
    {
        NotifyGrabLand(GrabViaAttackContext.ResolveAttackerForFear());
    }

    /// <summary>
    /// GrabViaAttack land. Per-enemy JSON <c>grabFear</c> (size/threat graded) overrides cfg magnitude;
    /// bosses also get BossCombatFearMult.
    /// </summary>
    internal static void NotifyGrabLand(EnemyDate attacker)
    {
        if (!Enabled) return;
        float mag = FearConfig.GrabLandMorale?.Value ?? -8f;
        if (attacker != null)
        {
            float fromEnemy = FearCatalog.ResolveForEnemy(attacker).grabFear;
            if (fromEnemy > 0f)
                mag = -fromEnemy;
        }
        if (mag > 0f) mag = -mag;
        mag *= ResolveBossCombatMult(attacker);
        if (Mathf.Abs(mag) < 0.0001f) return;
        ApplyDelta(mag, IsBossAttacker(attacker) ? "grab_land_boss" : "grab_land");
        _lastHStartSpikeTime = Time.unscaledTime;
    }

    /// <summary>Rising edge of managed grab H / trap H (covers vanilla grabs, not only GrabViaAttack).</summary>
    internal static void NotifyHSceneStart()
    {
        if (!Enabled) return;
        // GrabViaAttack already spiked within the last half-second — do not double.
        if (Time.unscaledTime - _lastHStartSpikeTime < 0.5f)
            return;
        _lastHStartSpikeTime = Time.unscaledTime;
        ApplyDelta(FearConfig.HSceneStartMorale?.Value ?? -4f, "h_scene_start");
    }

    /// <summary>FIN / climax beat (one spike per activation window).</summary>
    internal static void NotifyFinClimax(string reason = "fin_climax")
    {
        if (!Enabled) return;
        if (Time.unscaledTime - _lastFinSpikeTime < 1.2f)
            return;
        _lastFinSpikeTime = Time.unscaledTime;
        float delta = FearConfig.FinClimaxMorale?.Value ?? -3.33f;
        if (delta > 0f) delta = -delta;
        ApplyDelta(delta, reason);
    }

    /// <summary>
    /// Mirror MindBroken H-phase pressure into Fear.
    /// <paramref name="mbPercentPerSecond"/> is the same cfg number as MB AddPercent
    /// (e.g. 3 = +3% MB/s → −3 Morale/s).
    /// </summary>
    internal static void ApplyHPhaseFearMbRate(float mbPercentPerSecond, float dt, string reason)
    {
        if (!Enabled || dt <= 0f) return;
        if (Mathf.Abs(mbPercentPerSecond) < 0.0001f) return;
        // Positive MB% → negative Morale (more fear).
        float rate = mbPercentPerSecond;
        if (rate > 0f) rate = -rate;
        ApplyDelta(rate * dt, reason);
    }

    /// <summary>
    /// Named H-phase / beat spike (JSON or code). Negative Δ = more fear.
    /// Flat axis shift (no PrideFall) so −30 at Morale +70 lands at +40.
    /// </summary>
    internal static void NotifyHPhaseSpike(float deltaMorale, string reason)
    {
        if (!Enabled) return;
        if (Mathf.Abs(deltaMorale) < 0.0001f) return;
        if (deltaMorale > 0f) deltaMorale = -deltaMorale;
        SetMoraleDirect(_morale + deltaMorale, reason);
    }

    internal static void ProcessHSceneEdge(bool eroflag, int erodown)
    {
        if (!Enabled) return;
        bool active = eroflag && erodown != 0;
        if (active && !_wasHSceneActive)
            NotifyHSceneStart();
        _wasHSceneActive = active;
    }

    internal static void NotifyPerfectParry(EnemyDate attacker)
    {
        ApplyDelta(FearConfig.PerfectParryMorale?.Value ?? 10f, "perfect_parry");
    }

    internal static void NotifyAvoid()
    {
        ApplyDelta(FearConfig.AvoidMorale?.Value ?? 2f, "avoid");
    }

    internal static void NotifySuccessfulBlock(bool wasHeavy)
    {
        if (!Enabled) return;
        ApplyDelta(FearConfig.BlockMorale?.Value ?? 1f, "block");
        _blockWasHeavy = wasHeavy;
        _counterAwardedThisWindow = false;
        _blockWindowUntil = Time.unscaledTime + (FearConfig.CounterWindowSeconds?.Value ?? 3f);
    }

    internal static void NotifyPlayerHitOnEnemy()
    {
        if (!Enabled) return;

        float now = Time.unscaledTime;
        if (now - _lastComboHitTime > ComboTimeout)
            _comboHits = 0;
        _lastComboHitTime = now;
        _comboHits++;

        if (_blockWindowUntil > 0f && now <= _blockWindowUntil && !_counterAwardedThisWindow)
        {
            _counterAwardedThisWindow = true;
            _blockWindowUntil = -1f;
            ApplyDelta(FearConfig.CounterMorale?.Value ?? 2f, "counter_after_block");
        }

        float hit = FearConfig.HitMorale?.Value ?? 2f;
        if (hit != 0f)
            ApplyDelta(hit, "hit");

        float every5 = FearConfig.ComboEvery5Morale?.Value ?? 0f;
        if (every5 != 0f && _comboHits > 0 && _comboHits % 5 == 0)
            ApplyDelta(every5, "combo_every_5");
    }

    internal static void TickBlockWindowExpiry()
    {
        if (!Enabled) return;
        if (_blockWindowUntil < 0f) return;
        if (Time.unscaledTime < _blockWindowUntil) return;
        if (_counterAwardedThisWindow)
        {
            _blockWindowUntil = -1f;
            return;
        }

        float penalty = _blockWasHeavy
            ? (FearConfig.BlockHeavyNoCounterMorale?.Value ?? -2.5f)
            : (FearConfig.TurtleBlockNoCounterMorale?.Value ?? -1f);
        _blockWindowUntil = -1f;
        ApplyDelta(penalty, _blockWasHeavy ? "block_heavy_no_counter" : "turtle_block_no_counter");
    }

    /// <summary>Light melee damage that got through (not a block, not power).</summary>
    internal static void NotifyHitTaken(EnemyDate attacker)
    {
        if (!Enabled) return;
        float mag = FearConfig.HitTakenMorale?.Value ?? -2f;
        if (mag > 0f) mag = -mag;
        mag *= ResolveBossCombatMult(attacker);
        if (Mathf.Abs(mag) < 0.0001f) return;
        ApplyDelta(mag, IsBossAttacker(attacker) ? "hit_taken_boss" : "hit_taken");
    }

    /// <summary>Heavy / power hit (kickback). Per-enemy heavyHitFear overrides magnitude when set.</summary>
    internal static void NotifyHeavyHitOrKnockdown(EnemyDate attacker)
    {
        float now = Time.unscaledTime;
        if (now - _lastHeavyHitTime < 0.35f)
            return;
        _lastHeavyHitTime = now;

        float mag = FearConfig.HeavyHitMorale?.Value ?? -6f;
        if (attacker != null)
        {
            float fromEnemy = FearCatalog.ResolveForEnemy(attacker).heavyHitFear;
            if (fromEnemy > 0f)
                mag = -fromEnemy;
        }
        else if (mag > 0f)
        {
            mag = -mag;
        }
        mag *= ResolveBossCombatMult(attacker);
        _lastHeavyAppliedMag = Mathf.Abs(mag);
        ApplyDelta(mag, IsBossAttacker(attacker) ? "heavy_hit_boss" : "heavy_hit");
    }

    /// <summary>
    /// Knockdown / knockout (erodown). Stronger than heavy; if a heavy hit just applied,
    /// only the remaining difference is added so totals land near KnockdownMorale.
    /// Boss KO always exceeds the just-applied heavy by BossKnockdownBonus.
    /// </summary>
    internal static void NotifyKnockdown(EnemyDate attacker)
    {
        if (!Enabled) return;
        float now = Time.unscaledTime;
        if (now - _lastKnockdownTime < 0.45f)
            return;
        _lastKnockdownTime = now;

        float mag = FearConfig.KnockdownMorale?.Value ?? -10f;
        if (mag > 0f) mag = -mag;
        bool boss = IsBossAttacker(attacker);
        float abs = Mathf.Abs(mag) * ResolveBossCombatMult(attacker);
        if (boss)
        {
            float bonus = Mathf.Abs(FearConfig.BossKnockdownBonus?.Value ?? 8f);
            abs = Mathf.Max(abs, _lastHeavyAppliedMag + bonus);
        }
        if (abs < 0.0001f) return;

        if (now - _lastHeavyHitTime < 0.45f && _lastHeavyAppliedMag > 0f)
        {
            float extra = abs - _lastHeavyAppliedMag;
            if (extra > 0.01f)
                ApplyDelta(-extra, boss ? "knockdown_extra_boss" : "knockdown_extra");
            return;
        }

        ApplyDelta(-abs, boss ? "knockdown_boss" : "knockdown");
    }

    /// <summary>Knockdown edge from dispatcher (avoids a second playercon.Update patch).</summary>
    internal static void ProcessKnockdownEdge(int erodown, bool eroflag)
    {
        if (!Enabled) return;
        if (eroflag)
        {
            _lastErodown = erodown;
            return;
        }
        if (_lastErodown == 0 && erodown != 0)
            NotifyKnockdown(GrabViaAttackContext.ResolveAttackerForFear());
        _lastErodown = erodown;
    }

    internal static void NotifyProjectileHit(EnemyDate attacker)
    {
        float mag = FearCatalog.DefaultStats.projectileFear;
        if (attacker != null)
            mag = FearCatalog.ResolveForEnemy(attacker).projectileFear;
        if (mag <= 0f) mag = 1.5f;
        mag *= ResolveBossCombatMult(attacker);
        ApplyDelta(-mag, IsBossAttacker(attacker) ? "projectile_hit_boss" : "projectile_hit");
    }

    private static bool IsBossAttacker(EnemyDate attacker)
    {
        return attacker != null && FactionBossDetection.IsBossEnemy(attacker);
    }

    /// <summary>1 for normals; BossCombatFearMult (≥1) for bosses.</summary>
    private static float ResolveBossCombatMult(EnemyDate attacker)
    {
        if (!IsBossAttacker(attacker))
            return 1f;
        return Mathf.Max(1f, FearConfig.BossCombatFearMult?.Value ?? 2.25f);
    }

    internal static void NotifyEnemyKill(EnemyDate enemy, bool isBoss)
    {
        if (!Enabled || enemy == null) return;
        int id = enemy.GetInstanceID();
        if (_registeredKills.Contains(id)) return;
        if (enemy.Hp > 0f) return;
        _registeredKills.Add(id);
        float delta = isBoss
            ? (FearConfig.KillBossMorale?.Value ?? 15f)
            : (FearConfig.KillNormalMorale?.Value ?? 5f);
        ApplyDelta(delta, isBoss ? "kill_boss" : "kill_normal");
    }

    internal static void ApplyAuraDelta(float fearPerSecondSum, float dt)
    {
        if (!Enabled || dt <= 0f) return;
        if (fearPerSecondSum <= 0f) return;
        ApplyDelta(-fearPerSecondSum * dt, "aura");
    }

    /// <summary>0..1 SP ratio, or 1 if unknown.</summary>
    internal static float GetSpRatio(playercon player)
    {
        if (player == null) return 1f;
        try
        {
            if (_fields.PlayerStatus == null)
                _fields.PlayerStatus = AccessTools.Field(typeof(playercon), "playerstatus");
            PlayerStatus status = _fields.PlayerStatus != null
                ? _fields.PlayerStatus.GetValue(player) as PlayerStatus
                : null;
            if (status == null) return 1f;
            float maxSp = status.AllMaxSP();
            if (maxSp <= 0.01f) return 1f;
            return Mathf.Clamp01(status.Sp / maxSp);
        }
        catch
        {
            return 1f;
        }
    }

    /// <summary>Multiplies aura / H pressure when SP is low (default ×2 at ≤30%).</summary>
    internal static float GetLowSpAuraMultiplier(playercon player)
    {
        float threshold = FearConfig.LowSpAuraThreshold?.Value ?? 0.3f;
        float mult = FearConfig.LowSpAuraMult?.Value ?? 2f;
        if (threshold <= 0f || mult <= 1f) return 1f;
        if (GetSpRatio(player) > threshold) return 1f;
        return mult;
    }

    internal static void ApplySoftDecay(float dt)
    {
        if (!Enabled || dt <= 0f || _traumaRunning) return;
        float rate = FearConfig.SoftDecayPerSecond?.Value ?? 0.15f;
        if (rate <= 0f) return;
        if (Mathf.Abs(_morale) < 0.001f) return;
        float step = rate * dt;
        if (_morale > 0f)
            ApplyDelta(-Mathf.Min(step, _morale), "soft_decay");
        else
            ApplyDelta(Mathf.Min(step, -_morale), "soft_decay");
    }

    internal static void ProcessCombatEffects(playercon player, float dt)
    {
        if (!Enabled || !(FearConfig.EnableCombatEffects?.Value ?? true) || player == null)
            return;

        EnsureStepspCache(player);
        ApplyDashCostModifier(player);
        ApplyConfidenceStaminaRegen(player, dt);
    }

    private static void EnsureStepspCache(playercon player)
    {
        if (_hasCachedStepsp) return;
        float stepsp = ReadStepsp(player);
        if (stepsp > 0f)
        {
            _cachedBaseStepsp = stepsp;
            _hasCachedStepsp = true;
        }
    }

    private static void ApplyDashCostModifier(playercon player)
    {
        if (!_hasCachedStepsp || _cachedBaseStepsp <= 0f) return;
        float t = PanicT;
        float mult = 1f + t * (FearConfig.DashCostPanicMax?.Value ?? 0.4f);
        WriteStepsp(player, _cachedBaseStepsp * mult);
    }

    private static void ApplyConfidenceStaminaRegen(playercon player, float dt)
    {
        float t = ConfidenceT;
        if (t <= 0f || dt <= 0f) return;
        if (player.Attacknow || player.Actstate || player.stepfrag || player.magicnow)
            return;
        try
        {
            if (_fields.Parry == null)
                _fields.Parry = AccessTools.Field(typeof(playercon), "Parry");
            if (_fields.Parry != null && (bool)_fields.Parry.GetValue(player))
                return;
        }
        catch { }
        if (Time.timeScale == 0f) return;

        PlayerStatus status = null;
        try
        {
            if (_fields.PlayerStatus == null)
                _fields.PlayerStatus = AccessTools.Field(typeof(playercon), "playerstatus");
            if (_fields.PlayerStatus != null)
                status = _fields.PlayerStatus.GetValue(player) as PlayerStatus;
        }
        catch { }
        if (status == null) return;
        float maxSp = status.AllMaxSP();
        if (status.Sp >= maxSp) return;

        float bonusFrac = t * (FearConfig.StaminaRegenConfidenceMax?.Value ?? 0.25f);
        float add = (maxSp / 2f) * bonusFrac * dt;
        status.Sp = Mathf.Min(maxSp, status.Sp + add);
    }

    private static void OnRageActivated()
    {
        NotifyRageActivation();
    }

    private static float ReadStepsp(playercon player)
    {
        try
        {
            if (_fields.Stepsp == null)
                _fields.Stepsp = AccessTools.Field(typeof(playercon), "stepsp");
            if (_fields.Stepsp != null)
                return (float)_fields.Stepsp.GetValue(player);
        }
        catch { }
        return 0f;
    }

    private static void WriteStepsp(playercon player, float value)
    {
        try
        {
            if (_fields.Stepsp == null)
                _fields.Stepsp = AccessTools.Field(typeof(playercon), "stepsp");
            _fields.Stepsp?.SetValue(player, value);
        }
        catch { }
    }

    private sealed class FieldInfoCache
    {
        internal System.Reflection.FieldInfo Stepsp;
        internal System.Reflection.FieldInfo PlayerStatus;
        internal System.Reflection.FieldInfo Parry;
    }
}
