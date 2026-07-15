using NoREroMod.Systems.Cache;
using NoREroMod.Systems.Spawn;
using UnityEngine;
using UnityEngine.SceneManagement;



namespace NoREroMod.Patches.Player;



/// <summary>
/// Insomnia bar EV: additive <c>GoInsomnia</c>, inline <c>EVInsomniaBar*</c>.
/// <c>InsomniaTownB</c> (Ragdum) is normal gameplay — must NOT count as EV for spawn refresh.
/// Cross-zone Take Vengeance: when the player dies away from the altar that owns
/// <c>_checkpoint</c> (see <see cref="VanillaAltarCatalog"/>), force a real scene load —
/// vanilla Restart applies checkpoint coords in the current zone → void fall.
/// </summary>

internal static class VanillaCutsceneSceneGuard

{

    /// <summary>Real bar EV scene tokens (Idea_Nowscene / loaded additive). Not InsomniaTownB.</summary>
    private static readonly string[] KnownInsomniaBarEvZoneNames =

    {

        "EvInsomniaB1",

        "EVInsomniaBar",

        "EVInsomniaBarSP",

    };

    /// <summary>Bar area zones for altar-exit targeting (includes Ragdum gameplay map).</summary>
    private static readonly string[] KnownInsomniaBarZoneNames =

    {

        "EvInsomniaB1",

        "EVInsomniaBar",

        "EVInsomniaBarSP",

        "InsomniaTownB",

    };



    private static readonly string[] KnownAdditiveEvSceneNames =

    {

        "GoInsomnia",

    };



    private static readonly string[] InsomniaAreaZonePrefixes =

    {

        "InsomniaTown",

        "EvInsomnia",

        "EVInsomnia",

    };



    /// <summary>
    /// Zones often entered via walk from a parent map. Used for docs / diagnostics only —
    /// latching uses <see cref="VanillaAltarCatalog"/> (local altars like InUnder_over are valid).
    /// </summary>
    private static readonly string[] KnownCheckpointMismatchZones =

    {

        "InundergroundChurch",

    };



    private static bool _pendingBarAltarExit;

    private static bool _vendettaAltarExitPending;

    private static bool _noAltarZoneDeathArmed;



    internal static void MarkVendettaAltarExitPending() => _vendettaAltarExitPending = true;



    /// <summary>
    /// Latch a forced checkpoint-scene load for Take Vengeance. Call from ApplyVengeanceEffects /
    /// lethal-trap death — not from <see cref="PlayerStatus.REstart_menu"/> during fatality playback.
    /// </summary>
    internal static void NotifyPotentialNoAltarZoneDeath()
    {
        if (IsBlockingRespawnRedirectForActiveHSceneDeath())
            return;

        if (!ShouldLatchForcedAltarExitOnDeath())
            return;

        _noAltarZoneDeathArmed = true;
        string active = GetActiveZoneName();
        game_fragmng frag = UnifiedGameControllerCacheManager.GetGameFragMng();
        string checkpointScene = frag?._re_Scenename ?? "?";
        string altarHome = VanillaAltarCatalog.ResolveAltarHomeScene(frag) ?? "?";
        string savepoint = frag?._re_savepoint ?? "?";
        Vector2 pos = frag?._checkpoint ?? Vector2.zero;
        Plugin.Log?.LogInfo(
            $"[EV SCENE EXIT] Armed forced altar exit on death (zone=\"{active}\", "
            + $"checkpointScene=\"{checkpointScene}\", altarHome=\"{altarHome}\", "
            + $"savepoint=\"{savepoint}\", checkpointPos=({pos.x:F1},{pos.y:F1})).");
    }

    /// <summary>
    /// Fatality / grab deaths call <see cref="PlayerStatus.REstart_menu"/> while eroflag is still true
    /// (SpDeath runs before Death=true). Never redirect or arm during that playback.
    /// </summary>
    internal static bool IsBlockingRespawnRedirectForActiveHSceneDeath()
    {
        try
        {
            playercon player = UnifiedPlayerCacheManager.GetPlayer();
            if (player == null)
                return false;

            return player.eroflag || player._eroflag2;
        }
        catch
        {
            return false;
        }
    }

    internal static void MarkAltarExitInProgress() => _pendingBarAltarExit = true;

    private static bool ShouldLatchForcedAltarExitOnDeath()
    {
        // Latch only when death zone ≠ the altar that owns the checkpoint.
        // InundergroundChurch HAS local altars (savepoint_InUnder / _over) — saving there
        // and dying there must NOT redirect to scapegoatEntrance (void at IUC coords).
        // Cross-zone death (IUC with scapegoat save, or leaked _re_Scenename) still latches
        // via VanillaAltarCatalog savepoint/coords resolution.
        return IsActiveZoneDifferentFromCheckpoint();
    }

    /// <summary>
    /// Physical gameplay zone differs from the altar that owns the stored checkpoint
    /// (savepoint token / coords, not only raw <c>_re_Scenename</c>).
    /// </summary>
    internal static bool IsActiveZoneDifferentFromCheckpoint()
    {
        try
        {
            game_fragmng frag = UnifiedGameControllerCacheManager.GetGameFragMng();
            if (frag == null)
                return false;

            string active = GetActiveZoneName();
            if (string.IsNullOrEmpty(active))
                return false;

            return VanillaAltarCatalog.IsActiveZoneAwayFromAltarHome(active, frag);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>True when a death in a no-altar zone latched a forced altar exit (target = scapegoatEntrance).</summary>
    internal static bool IsNoAltarZoneDeathPending() => _noAltarZoneDeathArmed;



    /// <summary>
    /// True only for a genuine transient EV/cutscene (inline bar EV tokens, additive boss EV, GoInsomnia)
    /// or an altar exit already in progress. Used to suppress normal spawn/transition logic.
    /// Must NOT be true for plain <c>InsomniaTownB</c> gameplay — that permanently skipped walk spawn.
    /// Checkpoint mismatch is handled only at respawn (see <see cref="ShouldForceAltarSceneLoad"/>).
    /// </summary>
    internal static bool IsInsomniaBarEvSceneActive()

    {

        if (_pendingBarAltarExit || _vendettaAltarExitPending)

            return true;



        return IsInsomniaBarInlineEvActive() || IsAdditiveBossEvSceneActive();

    }



    /// <summary>

    /// True when bar/vendetta exit must run LoadSceneAndWait to altar — not vanilla savepoint in current zone.

    /// </summary>

    internal static bool ShouldForceAltarSceneLoad()

    {

        if (_vendettaAltarExitPending)

            return true;



        if (IsInsomniaBarInlineEvActive() || IsAdditiveBossEvSceneActive())

        {

            _pendingBarAltarExit = true;

            return true;

        }

        if (IsBlockingRespawnRedirectForActiveHSceneDeath())
            return false;

        if (_noAltarZoneDeathArmed)
            return true;

        // Cross-zone death: redirect respawn to checkpoint map. Do not set _pendingBarAltarExit
        // here — that flag blocks spawn refresh during normal exploration.
        if (IsActiveZoneDifferentFromCheckpoint())
            return true;

        return _pendingBarAltarExit;

    }



    /// <summary>
    /// True only while a real bar EV token is active/loaded — never for plain InsomniaTownB gameplay.
    /// Used by spawn guards via <see cref="IsAdditiveEvSceneActive"/>; must stay narrow.
    /// </summary>
    internal static bool IsInsomniaBarInlineEvActive()

    {

        try

        {

            if (MatchesZoneList(GetActiveZoneName(), KnownInsomniaBarEvZoneNames))

                return true;



            for (int i = 0; i < KnownInsomniaBarEvZoneNames.Length; i++)

            {

                if (IsSceneLoaded(KnownInsomniaBarEvZoneNames[i]))

                    return true;

            }

        }

        catch

        {

            // fall through

        }



        return false;

    }

    /// <summary>True while altar-exit / vendetta load is in progress (blocks spawn refresh).</summary>
    internal static bool IsAltarExitInProgress() =>
        _pendingBarAltarExit || _vendettaAltarExitPending;



    internal static bool IsAdditiveBossEvSceneActive()

    {

        try

        {

            PlayerStatus status = UnifiedPlayerCacheManager.GetPlayerStatus();

            if (status != null)

            {

                string bossScene = status._BossScene;

                if (!string.IsNullOrEmpty(bossScene)

                    && !bossScene.Equals("non", System.StringComparison.OrdinalIgnoreCase)

                    && IsSceneLoaded(bossScene))

                {

                    return true;

                }

            }



            for (int i = 0; i < KnownAdditiveEvSceneNames.Length; i++)

            {

                if (IsSceneLoaded(KnownAdditiveEvSceneNames[i]))

                    return true;

            }

        }

        catch

        {

            // fall through

        }



        return false;

    }



    /// <summary>
    /// Active zone differs from the altar home that owns the checkpoint
    /// (e.g. InundergroundChurch vs scapegoatEntrance save).
    /// Vanilla REstrat_invoke calls Restart() in the current scene at checkpoint coords → void fall.
    /// </summary>

    internal static bool IsCheckpointSceneMismatch()

    {

        // Death latch: live zone may already read as the checkpoint scene name.
        if (_noAltarZoneDeathArmed)

            return true;



        try

        {

            game_fragmng frag = UnifiedGameControllerCacheManager.GetGameFragMng();

            if (frag == null)

                return false;



            string active = GetActiveZoneName();

            if (string.IsNullOrEmpty(active))

                return false;



            return VanillaAltarCatalog.IsActiveZoneAwayFromAltarHome(active, frag);

        }

        catch

        {

            return false;

        }

    }



    internal static bool IsAdditiveEvSceneActive() => IsInsomniaBarEvSceneActive();



    internal static void ClearPendingBarAltarExit()

    {

        _pendingBarAltarExit = false;

        _vendettaAltarExitPending = false;

        _noAltarZoneDeathArmed = false;

    }



    internal static string GetActiveZoneName() => HellGateLocationSpawnRefresh.GetActiveGameplayZone();



    internal static bool IsInsomniaBarZoneName(string zoneName) => MatchesInsomniaBarZone(zoneName);



    internal static bool IsInsomniaEvArea(string zoneName)

    {

        if (string.IsNullOrEmpty(zoneName))

            return false;



        for (int i = 0; i < InsomniaAreaZonePrefixes.Length; i++)

        {

            if (zoneName.StartsWith(InsomniaAreaZonePrefixes[i], System.StringComparison.OrdinalIgnoreCase))

                return true;

        }



        return MatchesInsomniaBarZone(zoneName);

    }



    internal static bool IsKnownCheckpointMismatchZone(string zoneName) => MatchesZoneList(zoneName, KnownCheckpointMismatchZones);



    private static bool MatchesInsomniaBarZone(string zoneName) => MatchesZoneList(zoneName, KnownInsomniaBarZoneNames);



    private static bool MatchesZoneList(string zoneName, string[] zones)

    {

        if (string.IsNullOrEmpty(zoneName))

            return false;



        for (int i = 0; i < zones.Length; i++)

        {

            if (zoneName.Equals(zones[i], System.StringComparison.OrdinalIgnoreCase))

                return true;

        }



        return false;

    }



    private static bool IsSceneLoaded(string sceneName)

    {

        if (string.IsNullOrEmpty(sceneName))

            return false;



        Scene scene = SceneManager.GetSceneByName(sceneName);

        return scene.IsValid() && scene.isLoaded;

    }

}


