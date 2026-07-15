using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using NoREroMod;
using NoREroMod.Systems.Cache;
using NoREroMod.Systems.CombatAi.Factions;
using NoREroMod.Systems.CombatAi.Factions;
using NoREroMod.Systems.CombatAi.Factions.Patches;
using NoREroMod.Systems.EventCore.Content;
using NoREroMod.Systems.EventCore.Core;
using NoREroMod.Systems.EventCore.Handlers;
using NoREroMod.Systems.GrabSystem.Patches;
using UnityEngine;
using UnityEngine.UI;

namespace NoREroMod.Systems.EventCore.Host;

internal enum EventCoreEncounterState
{
    Unresolved,
    PeacefulResolved,
    BodyPaymentResolved,
    HostileRevealed,
    CowardlyFled,
}

/// <summary>Voluntary event H (no struggle) vs forced take (hostile after escape).</summary>
internal enum EventCoreHandoffMode
{
    None,
    Consent,
    Forced,
}

/// <summary>
/// Opens an EventCore session when the player enters range of this spawn trigger.
/// The host arms once per enemy instance; a respawn creates a new component and can trigger again.
/// </summary>
internal sealed class EventCoreHost : MonoBehaviour
{
    private const float TriggerDistance = 1.5f;

    /// <summary>After voluntary body-payment, guarantee grab-via-attack before returning to passive shell.</summary>
    private const float ConsentGrabWindowSeconds = 3f;

    private string _eventId = string.Empty;
    private bool _armed = true;
    private bool _initialized;
    private int _rolledRevealFactionId = FactionIds.Bandits;
    private int _peacefulFactionId = FactionIds.EventCoreEncounter;
    private EventCoreEncounterState _state = EventCoreEncounterState.Unresolved;
    private bool _ambushOnProvocationArmed;
    private bool _ambushOnProvocationTriggered;
    private bool _cowardlyFleePending;
    private float _hostileRevealAtUnscaled = -1f;
    private float _consentGrabWindowUntilUnscaled = -1f;
    private EventCoreHandoffMode _handoffMode = EventCoreHandoffMode.None;
    private bool _hostileOnPlayerFreed;
    private bool _isSexPaidEvent;
    private bool _dismissAfterSexScene;
    private Image _passiveExclamation;
    private bool _passiveExclamationResolved;

    internal static EventCoreHost ActiveHandoffHost { get; private set; }

    internal bool IsConsentStruggleLocked =>
        _handoffMode == EventCoreHandoffMode.Consent &&
        UnifiedPlayerCacheManager.GetPlayer() is { eroflag: true };

    internal static bool IsAnyConsentStruggleLocked() =>
        ActiveHandoffHost != null && ActiveHandoffHost.IsConsentStruggleLocked;

    internal void Configure(string eventId)
    {
        _eventId = string.IsNullOrEmpty(eventId) ? string.Empty : eventId.Trim();
        _armed = !string.IsNullOrEmpty(_eventId);
        _initialized = false;
        _rolledRevealFactionId = FactionIds.Bandits;
        _peacefulFactionId = FactionIds.EventCoreEncounter;
        _state = EventCoreEncounterState.Unresolved;
        _ambushOnProvocationArmed = false;
        _ambushOnProvocationTriggered = false;
        _cowardlyFleePending = false;
        _hostileRevealAtUnscaled = -1f;
        _consentGrabWindowUntilUnscaled = -1f;
        _handoffMode = EventCoreHandoffMode.None;
        _hostileOnPlayerFreed = false;
        _isSexPaidEvent = false;
        _dismissAfterSexScene = false;
        if (ActiveHandoffHost == this)
            ActiveHandoffHost = null;
        ClearPassiveExclamation();

        if (_armed)
        {
            EnsureEncounterSetup();
            ApplyFactionOverride(FactionIds.EventCoreEncounter, clearHostility: true);
        }
    }

    internal void BeginBrokerConsentGrab(EventCoreHandoffMode mode = EventCoreHandoffMode.Consent)
    {
        _handoffMode = mode;
        ActiveHandoffHost = this;
        if (_isSexPaidEvent)
            _dismissAfterSexScene = true;
        _consentGrabWindowUntilUnscaled = Time.unscaledTime + ConsentGrabWindowSeconds;
        SetPassiveExclamationVisible(false);
        Plugin.Log?.LogInfo(
            $"[EventCoreHost] Handoff grab ({mode}) window open for {ConsentGrabWindowSeconds:0.#}s on '{gameObject.name}'.");
        StartCoroutine(ConsentGrabStrikeRoutine());
    }

    internal void MarkHostileOnPlayerFreed()
    {
        if (string.IsNullOrEmpty(_eventId))
            return;
        _hostileOnPlayerFreed = true;
    }

    internal void ApplyConsentStruggleLockIfActive()
    {
        if (_handoffMode != EventCoreHandoffMode.Consent)
            return;

        try
        {
            StruggleSystem.setStruggleLevel(10);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[EventCoreHost] Consent struggle lock failed: {ex.Message}");
        }
    }

    internal static void NotifyPlayerFreedFromHScene()
    {
        EventCoreHost target = ActiveHandoffHost;
        if (target == null)
        {
            EnemyDate[] enemies = UnityEngine.Object.FindObjectsOfType<EnemyDate>();
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyDate enemy = enemies[i];
                if (enemy == null || !enemy.eroflag)
                    continue;

                EventCoreHost host = enemy.GetComponent<EventCoreHost>();
                if (host != null && host.NeedsPostEscapeHostility())
                {
                    target = host;
                    break;
                }
            }
        }

        target?.NotifyPlayerFreedFromHSceneInternal();
    }

    internal static void NotifyPlayerHSceneEnded()
    {
        EventCoreHost target = ActiveHandoffHost;
        if (target == null)
            return;

        target.NotifyPlayerHSceneEndedInternal();
    }

    private bool NeedsPostEscapeHostility() =>
        _handoffMode == EventCoreHandoffMode.Forced || _hostileOnPlayerFreed;

    private void NotifyPlayerFreedFromHSceneInternal()
    {
        if (NeedsPostEscapeHostility())
        {
            _dismissAfterSexScene = false;
            _hostileRevealAtUnscaled = -1f;
            ResolveHostile();
            Plugin.Log?.LogInfo($"[EventCoreHost] Post-escape hostility applied on '{gameObject.name}'.");
        }
        else if (_handoffMode == EventCoreHandoffMode.Consent)
        {
            try
            {
                StruggleSystem.setStruggleLevel(-1);
            }
            catch
            {
            }
        }

        _handoffMode = EventCoreHandoffMode.None;
        _hostileOnPlayerFreed = false;
        if (ActiveHandoffHost == this)
            ActiveHandoffHost = null;
    }

    private void NotifyPlayerHSceneEndedInternal()
    {
        if (_dismissAfterSexScene)
        {
            ResolveDismissAfterEncounter();
            return;
        }

        if (_handoffMode == EventCoreHandoffMode.Consent)
        {
            try
            {
                StruggleSystem.setStruggleLevel(-1);
            }
            catch
            {
            }
        }

        _handoffMode = EventCoreHandoffMode.None;
        if (ActiveHandoffHost == this)
            ActiveHandoffHost = null;
    }

    internal void ResolveDismissAfterEncounter()
    {
        if (string.IsNullOrEmpty(_eventId))
            return;

        _armed = false;
        _ambushOnProvocationArmed = false;
        _ambushOnProvocationTriggered = false;
        _cowardlyFleePending = false;
        _hostileRevealAtUnscaled = -1f;
        _consentGrabWindowUntilUnscaled = -1f;
        _handoffMode = EventCoreHandoffMode.None;
        _hostileOnPlayerFreed = false;
        _dismissAfterSexScene = false;
        if (ActiveHandoffHost == this)
            ActiveHandoffHost = null;
        SetPassiveExclamationVisible(false);
        _state = EventCoreEncounterState.CowardlyFled;

        EnemyDate enemy = GetComponent<EnemyDate>();
        if (enemy != null)
            FactionBoneMarkerAttachment.Remove(enemy);

        Plugin.Log?.LogInfo($"[EventCoreHost] Dismissed encounter NPC '{gameObject.name}' (event '{_eventId}').");
        gameObject.SetActive(false);
    }

    private IEnumerator ConsentGrabStrikeRoutine()
    {
        yield return null;

        if (!IsConsentGrabWindowActive())
            yield break;

        EnemyDate enemy = GetComponent<EnemyDate>();
        playercon player = UnifiedPlayerCacheManager.GetPlayer();
        if (enemy == null || player == null || player.eroflag || player.erodown != 0)
            yield break;

        EnemyFactionRuntime.RestoreVanillaPlayerApproach(enemy);
        enemy.Look = true;
        enemy.enmATKnow = true;
        if (enemy.Choose == 0)
            enemy.Choose = 1;

        PlayerStatus playerStatus = ResolvePlayerStatus(player);
        if (playerStatus == null)
            yield break;

        if (GrabViaAttackPatch.TryForceConsentGrab(enemy, playerStatus))
        {
            _consentGrabWindowUntilUnscaled = -1f;
            ApplyConsentStruggleLockIfActive();
            Plugin.Log?.LogInfo($"[EventCoreHost] Handoff grab executed on '{gameObject.name}' ({_handoffMode}).");
        }
    }

    private static PlayerStatus ResolvePlayerStatus(playercon player)
    {
        if (player == null)
            return null;

        GameObject gc = GameObject.FindGameObjectWithTag("GameController");
        PlayerStatus fromController = gc != null ? gc.GetComponent<PlayerStatus>() : null;
        if (fromController != null)
            return fromController;

        return player.GetComponent<PlayerStatus>();
    }

    internal bool IsConsentGrabWindowActive()
    {
        return _consentGrabWindowUntilUnscaled > 0f && Time.unscaledTime < _consentGrabWindowUntilUnscaled;
    }

    internal bool ShouldForcePassive(bool hostileToPlayer)
    {
        if (Plugin.eventCoreEnable != null && !Plugin.eventCoreEnable.Value)
            return false;
        if (string.IsNullOrEmpty(_eventId))
            return false;
        if (IsConsentGrabWindowActive())
            return false;
        if (_state == EventCoreEncounterState.HostileRevealed)
            return false;
        return !hostileToPlayer;
    }

    internal bool ShouldSuppressCombatThreats(bool hostileToPlayer)
    {
        if (Plugin.eventCoreEnable != null && !Plugin.eventCoreEnable.Value)
            return false;
        if (string.IsNullOrEmpty(_eventId))
            return false;
        if (_state == EventCoreEncounterState.HostileRevealed)
            return false;

        // While the broker is still in encounter-shell mode, let combat-only threat
        // phrases speak only during a real temporary provocation window.
        return !hostileToPlayer;
    }

    internal void ResolvePeaceful()
    {
        if (string.IsNullOrEmpty(_eventId))
            return;

        EnsureEncounterSetup();
        _armed = false;
        _state = EventCoreEncounterState.PeacefulResolved;
        _ambushOnProvocationArmed = true;
        _ambushOnProvocationTriggered = false;
        _cowardlyFleePending = false;
        _hostileRevealAtUnscaled = -1f;
        _consentGrabWindowUntilUnscaled = -1f;
        ApplyFactionOverride(_peacefulFactionId, clearHostility: true);
    }

    internal void ResolveBodyPayment()
    {
        if (string.IsNullOrEmpty(_eventId))
            return;

        EnsureEncounterSetup();
        _armed = false;
        _state = EventCoreEncounterState.BodyPaymentResolved;
        _ambushOnProvocationArmed = true;
        _ambushOnProvocationTriggered = false;
        _cowardlyFleePending = false;
        _hostileRevealAtUnscaled = -1f;
        ApplyFactionOverride(_peacefulFactionId, clearHostility: true);
    }

    internal void ScheduleHostileReveal(float delaySeconds)
    {
        if (string.IsNullOrEmpty(_eventId))
            return;

        MarkHostileOnPlayerFreed();
        float delay = Mathf.Max(0f, delaySeconds);
        _hostileRevealAtUnscaled = Time.unscaledTime + delay;
    }

    internal void ResolveHostile()
    {
        if (string.IsNullOrEmpty(_eventId))
            return;

        EnsureEncounterSetup();
        _armed = false;
        _state = EventCoreEncounterState.HostileRevealed;
        _ambushOnProvocationArmed = false;
        _cowardlyFleePending = false;
        _hostileRevealAtUnscaled = -1f;
        _consentGrabWindowUntilUnscaled = -1f;
        _hostileOnPlayerFreed = false;
        SetPassiveExclamationVisible(false);

        int revealFactionId = FactionIds.IsPassiveNonCombat(_rolledRevealFactionId)
            ? FactionIds.Bandits
            : _rolledRevealFactionId;

        ApplyFactionOverride(revealFactionId, clearHostility: false);

        EnemyDate enemy = GetComponent<EnemyDate>();
        if (enemy != null)
            EnemyFactionRuntime.MarkSessionHostileToPlayer(enemy);
    }

    internal void MarkCowardlyFleePending()
    {
        if (string.IsNullOrEmpty(_eventId))
            return;
        _cowardlyFleePending = true;
    }

    internal void ResolveCowardlyFlee()
    {
        if (string.IsNullOrEmpty(_eventId))
            return;

        _armed = false;
        _ambushOnProvocationArmed = false;
        _cowardlyFleePending = false;
        _hostileRevealAtUnscaled = -1f;
        _consentGrabWindowUntilUnscaled = -1f;
        SetPassiveExclamationVisible(false);
        _state = EventCoreEncounterState.CowardlyFled;
        gameObject.SetActive(false);
    }

    internal bool ShouldResolveCowardlyFleeOnPassDone()
    {
        return _cowardlyFleePending;
    }

    internal void HandlePlayerProvoked()
    {
        if (!_ambushOnProvocationArmed || _ambushOnProvocationTriggered)
            return;
        if (_state != EventCoreEncounterState.PeacefulResolved &&
            _state != EventCoreEncounterState.BodyPaymentResolved)
            return;

        _ambushOnProvocationTriggered = true;
        ResolveHostile();

        EventCoreDefinitionRegistry.EnsureLoaded();
        if (!EventCoreDefinitionRegistry.TryGet(_eventId, out EventCoreEventDefinitionFile def) || def == null)
        {
            Plugin.Log?.LogWarning($"[EventCore] Post-resolution ambush skipped: event '{_eventId}' not found.");
            return;
        }

        EventCoreBrokerGateFlow.TrySpawnBrokerAmbush(def, EventCoreBrokerGateFlow.StepThreatRetort, this);
    }

    internal int GetRolledRevealFactionId()
    {
        EnsureEncounterSetup();
        return FactionIds.IsPassiveNonCombat(_rolledRevealFactionId)
            ? FactionIds.Bandits
            : _rolledRevealFactionId;
    }

    private void Update()
    {
        if (_hostileRevealAtUnscaled > 0f && Time.unscaledTime >= _hostileRevealAtUnscaled)
        {
            _hostileRevealAtUnscaled = -1f;
            ResolveHostile();
        }

        if (_consentGrabWindowUntilUnscaled > 0f && Time.unscaledTime >= _consentGrabWindowUntilUnscaled)
            _consentGrabWindowUntilUnscaled = -1f;

        SyncPassiveExclamation();

        if (string.IsNullOrEmpty(_eventId))
            return;

        EnsureEncounterSetup();

        if (Plugin.eventCoreEnable == null || !Plugin.eventCoreEnable.Value)
            return;

        if (EventCoreRuntime.IsSessionOpen)
            return;

        GameObject player = UnifiedPlayerCacheManager.GetPlayerObject();
        if (player == null)
            return;

        Vector2 here = new Vector2(transform.position.x, transform.position.y);
        Vector2 there = new Vector2(player.transform.position.x, player.transform.position.y);
        float d = Vector2.Distance(here, there);

        if (!_armed)
            return;

        if (d <= TriggerDistance && EventCoreRuntime.TryBeginSession(_eventId, this))
            _armed = false;
    }

    private void EnsureEncounterSetup()
    {
        if (_initialized || string.IsNullOrEmpty(_eventId))
            return;

        _initialized = true;
        EventCoreDefinitionRegistry.EnsureLoaded();
        if (!EventCoreDefinitionRegistry.TryGet(_eventId, out EventCoreEventDefinitionFile def) || def == null)
        {
            Plugin.Log?.LogError("[EventCoreHost] Unknown event '" + _eventId + "' on '" + gameObject.name +
                                 "'. Check eventcore_manifest.json and restart the game after adding JSON.");
            _armed = false;
            return;
        }

        _rolledRevealFactionId = RollRevealFaction(def);
        _peacefulFactionId = ResolvePeacefulFaction(def);
        _isSexPaidEvent = EventCoreFactionSocialFlow.IsSexPaid(def);
    }

    private static int ResolvePeacefulFaction(EventCoreEventDefinitionFile def)
    {
        if (def != null &&
            !string.IsNullOrEmpty(def.peacefulFactionId) &&
            FactionIds.TryParse(def.peacefulFactionId, out int configuredFaction))
            return configuredFaction;

        return FactionIds.EventCoreEncounter;
    }

    private static int RollRevealFaction(EventCoreEventDefinitionFile def)
    {
        if (def?.revealFactionPool != null && def.revealFactionPool.Length > 0)
        {
            var valid = new List<int>();
            for (int i = 0; i < def.revealFactionPool.Length; i++)
            {
                string raw = def.revealFactionPool[i];
                if (string.IsNullOrEmpty(raw))
                    continue;
                if (!FactionIds.TryParse(raw, out int candidateFaction))
                    continue;
                if (FactionIds.IsPassiveNonCombat(candidateFaction))
                    continue;
                valid.Add(candidateFaction);
            }

            if (valid.Count > 0)
                return valid[UnityEngine.Random.Range(0, valid.Count)];
        }

        return FactionIds.Bandits;
    }

    private void ApplyFactionOverride(int factionId, bool clearHostility)
    {
        if (gameObject == null)
            return;

        SpawnFactionOverride overrideComponent = GetComponent<SpawnFactionOverride>();
        if (overrideComponent == null)
            overrideComponent = gameObject.AddComponent<SpawnFactionOverride>();
        overrideComponent.FactionIdRaw = factionId.ToString(CultureInfo.InvariantCulture);

        EnemyDate enemy = GetComponent<EnemyDate>();
        if (enemy == null)
            return;

        EnemyFactionRuntime.RegisterEnemy(enemy);
        if (clearHostility)
            EnemyFactionRuntime.ClearHostilityToPlayer(enemy);

        if (FactionIds.IsPassiveNonCombat(factionId))
            FactionBoneMarkerAttachment.Remove(enemy);
        else if (!FactionMarkerVisibility.ShouldSuppress(enemy))
            EnemyDateFactionColorBootstrapPatch.ApplyFactionMarker(enemy);
        else
            FactionBoneMarkerAttachment.Remove(enemy);
    }

    private void OnDisable()
    {
        SetPassiveExclamationVisible(false);
    }

    private bool ShouldShowPassiveExclamation()
    {
        if (_state == EventCoreEncounterState.HostileRevealed ||
            _state == EventCoreEncounterState.CowardlyFled)
            return false;
        if (IsConsentGrabWindowActive())
            return false;
        if (EventCoreRuntime.IsSessionOpen)
            return false;
        return _state == EventCoreEncounterState.Unresolved ||
               _state == EventCoreEncounterState.PeacefulResolved;
    }

    private void SyncPassiveExclamation()
    {
        SetPassiveExclamationVisible(ShouldShowPassiveExclamation());
    }

    private void ResolvePassiveExclamation()
    {
        if (_passiveExclamationResolved)
            return;

        _passiveExclamationResolved = true;
        if (transform == null)
            return;

        Transform marker = transform.Find("Canvas/exclamation");
        if (marker == null)
            return;

        _passiveExclamation = marker.GetComponent<Image>();
    }

    private void SetPassiveExclamationVisible(bool visible)
    {
        ResolvePassiveExclamation();
        if (_passiveExclamation == null)
            return;

        if (_passiveExclamation.enabled == visible &&
            _passiveExclamation.gameObject.activeSelf == visible)
            return;

        _passiveExclamation.enabled = visible;
        if (_passiveExclamation.gameObject.activeSelf != visible)
            _passiveExclamation.gameObject.SetActive(visible);
    }

    private void ClearPassiveExclamation()
    {
        SetPassiveExclamationVisible(false);
        _passiveExclamation = null;
        _passiveExclamationResolved = false;
    }
}
