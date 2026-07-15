using NoREroMod.Systems.CombatAi.Factions;
using NoREroMod.Systems.Pregnancy.Patches;
using UnityEngine;

namespace NoREroMod.Systems.Pregnancy;

/// <summary>
/// Universal milliliter capture. Instead of hooking the per-enemy <c>EnemyDate.Nakadasi</c>
/// (which many sources bypass — traps like RoseWarm / tentacles / black ooze, and some enemy
/// variants), this polls the game's authoritative cumulative counter
/// <c>PlayerStatus.NakadashiValue</c> every frame and reacts to its positive delta.
///
/// The delta is the ml of the creampie that just happened; we attribute it to the current
/// H-scene partner's faction (or neutral fill when no faction can be resolved — traps, etc.).
/// Driven from <c>PlayerConUpdateDispatcher</c>. Mirrors how NoRBigOCounter reads the counter,
/// but adds faction attribution on top.
///
/// Uses <see cref="Time.unscaledTime"/> for freshness checks so accelerated H-animations
/// (4x speed via Dash/Step key in NoREroMod) do not break faction attribution.
///
/// <b>Sticky Faction:</b> During an active H-scene, once we have identified a valid faction,
/// we keep using it for subsequent creampies even if the tracker window expires. This handles
/// handoff/gangbang scenarios where multiple enemies climax in sequence.
/// </summary>
internal static class WombMeterNakadashiPoller
{
    private const float MaxPlausibleDelta = 1000f; // guards against save/load jumps
    private const float StickyFactionTimeoutSeconds = 10f; // How long to keep sticky faction after H-scene ends

    private static float _lastValue = -1f;
    private static int _stickyFaction = FactionIds.Neutral;
    private static float _stickyFactionSetTime = -999f;
    private static bool _wasInHScene = false;

    public static void Process(playercon player, PlayerStatus ps)
    {
        if (PregnancyConfig.Enable == null || !PregnancyConfig.Enable.Value)
            return;
        if (player == null || ps == null)
            return;

        bool debug = PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value;
        bool inHScene = player.eroflag || player.erodown != 0;
        float current = ps.NakadashiValue;

        // Update sticky faction from QTESystem when in H-scene (handles handoff/gangbang enemy switches)
        if (inHScene && Time.frameCount % 30 == 0) // Check every 30 frames (~0.5 sec)
        {
            TryUpdateFactionFromQTE(debug);
        }

        // Reset sticky faction when leaving H-scene (with delay) or entering new H-scene after gap
        if (_wasInHScene && !inHScene)
        {
            float timeSinceExit = Time.unscaledTime - _stickyFactionSetTime;
            if (timeSinceExit > StickyFactionTimeoutSeconds)
            {
                if (debug && _stickyFaction != FactionIds.Neutral)
                    Plugin.Log?.LogInfo($"[Pregnancy.Poller] Sticky faction cleared (timeSinceExit={timeSinceExit:0.#}s)");
                _stickyFaction = FactionIds.Neutral;
            }
        }
        // Reset when entering H-scene from clear state (new encounter)
        else if (!_wasInHScene && inHScene && _stickyFaction != FactionIds.Neutral)
        {
            float timeSinceLastSet = Time.unscaledTime - _stickyFactionSetTime;
            if (timeSinceLastSet > StickyFactionTimeoutSeconds)
            {
                if (debug)
                    Plugin.Log?.LogInfo($"[Pregnancy.Poller] Sticky faction reset on new H-scene (gap={timeSinceLastSet:0.#}s)");
                _stickyFaction = FactionIds.Neutral;
            }
        }
        _wasInHScene = inHScene;

        if (_lastValue < 0f)
        {
            _lastValue = current; // baseline on first frame
            if (debug)
                Plugin.Log?.LogInfo($"[Pregnancy.Poller] Baseline set: {current:0.#}ml");
            return;
        }

        float delta = current - _lastValue;

        // No change this frame -> nothing to do (keep baseline).
        if (delta == 0f)
            return;

        // Counter went down (vanilla reset / new game) or jumped hugely (save load):
        // re-baseline without counting.
        if (delta < 0f || delta > MaxPlausibleDelta)
        {
            if (debug)
                Plugin.Log?.LogInfo($"[Pregnancy.Poller] Counter jump delta={delta:0.#} (current={current:0.#}) -> re-baseline, no count.");
            _lastValue = current;
            _stickyFaction = FactionIds.Neutral; // Reset on save/load
            return;
        }

        // Positive plausible delta -> a creampie of this many ml just happened.
        _lastValue = current;

        int faction;
        string diag;

        // 1. Check if tracker has fresh faction from Nakadasi hook
        float timeSinceTrack = Time.unscaledTime - PregnancyPartnerTrackerPatch.LastUnscaledTime;
        bool trackerFresh = timeSinceTrack < PregnancyPartnerTrackerPatch.FreshnessWindowSeconds &&
                            PregnancyPartnerTrackerPatch.LastFaction != FactionIds.Neutral;

        if (trackerFresh)
        {
            faction = PregnancyPartnerTrackerPatch.LastFaction;
            diag = "tracker";
            // Update sticky faction when we get a fresh tracker reading
            if (_stickyFaction != faction)
            {
                if (debug)
                    Plugin.Log?.LogInfo($"[Pregnancy.Poller] Sticky faction updated: {_stickyFaction}->{faction}");
                _stickyFaction = faction;
                _stickyFactionSetTime = Time.unscaledTime;
            }
        }
        // 2. Use sticky faction if we're in H-scene and have one
        else if (inHScene && _stickyFaction != FactionIds.Neutral)
        {
            faction = _stickyFaction;
            diag = "sticky";
            if (debug && timeSinceTrack >= PregnancyPartnerTrackerPatch.FreshnessWindowSeconds)
            {
                Plugin.Log?.LogInfo($"[Pregnancy.Poller] Using sticky faction {_stickyFaction} (tracker expired {timeSinceTrack:0.##}s ago, lastQTE={_lastQteEnemy})");
            }
        }
        // 3. Fall back to QTE resolver
        else
        {
            faction = PregnancySourceResolver.Resolve(null, out diag);
            // If resolver found a faction, update sticky
            if (faction != FactionIds.Neutral && inHScene)
            {
                _stickyFaction = faction;
                _stickyFactionSetTime = Time.unscaledTime;
                diag += "+sticky";
            }
        }

        if (debug)
        {
            string factionName = faction == FactionIds.Neutral ? "NEUTRAL" : $"{faction}";
            string stickyInfo = _stickyFaction != FactionIds.Neutral ? $" sticky={_stickyFaction}" : "";
            string trackerStatus = trackerFresh ? "FRESH" : (timeSinceTrack < 5f ? $"stale({timeSinceTrack:0.##}s)" : "expired");
            Plugin.Log?.LogInfo($"[Pregnancy.Poller] +{delta:0.#}ml -> {factionName} [{diag}]" +
                $" (tracker={trackerStatus}{stickyInfo} inHScene={inHScene})");
        }

        WitchWombMeter.AddSeed(faction, delta);
    }

    /// <summary>
    /// Updates sticky faction from QTESystem current enemy (for handoff/gangbang when Nakadasi isn't called again).
    /// </summary>
    private static void TryUpdateFactionFromQTE(bool debug)
    {
        try
        {
            object hsceneEnemy = QTESystem.GetCurrentEnemyInstance();
            if (hsceneEnemy == null)
            {
                if (debug && _lastQteEnemy != null)
                    Plugin.Log?.LogInfo($"[Pregnancy.Poller] QTE enemy lost (was: {_lastQteEnemy})");
                _lastQteEnemy = null;
                return;
            }

            GameObject hsceneGo = hsceneEnemy as GameObject;
            if (hsceneEnemy is Component comp && hsceneGo == null)
                hsceneGo = comp.gameObject;
            if (hsceneGo == null) return;

            string enemyName = hsceneGo.name;
            int newFaction = EnemyFactionRuntime.GetFaction(hsceneGo);

            // Log enemy changes for debugging
            if (enemyName != _lastQteEnemy)
            {
                if (debug)
                    Plugin.Log?.LogInfo($"[Pregnancy.Poller] QTE enemy changed: {_lastQteEnemy} -> {enemyName} (faction={newFaction})");
                _lastQteEnemy = enemyName;
            }

            if (newFaction != FactionIds.Neutral && newFaction != _stickyFaction)
            {
                if (debug)
                    Plugin.Log?.LogInfo($"[Pregnancy.Poller] Handoff faction update: {_stickyFaction}->{newFaction} ({enemyName})");
                _stickyFaction = newFaction;
                _stickyFactionSetTime = Time.unscaledTime;
            }
        }
        catch { }
    }

    private static string _lastQteEnemy = null;
}
