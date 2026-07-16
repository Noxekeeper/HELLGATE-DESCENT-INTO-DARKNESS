using System;
using BepInEx.Logging;
using NoREroMod.Patches.UI.MindBroken;
using NoREroMod.Systems.CombatAi.Factions;
using NoREroMod.Systems.Economy;
using NoREroMod.Systems.Pregnancy;
using NoREroMod.Systems.Rage;
using UnityEngine.SceneManagement;

namespace NoREroMod.HellGate.Api;

/// <summary>
/// Stable read-only integration surface for other BepInEx plugins.
/// API 0.x is experimental; consumers must check <see cref="ApiVersion"/>.
/// </summary>
public static class HellGateApi
{
    private const string CurrentApiVersion = "0.1.0";
    private const int CurrentApiMajorVersion = 0;

    private static bool _isReady;
    private static string _pluginVersion = string.Empty;
    private static ManualLogSource _log;

    public static bool IsReady => _isReady;
    public static string ApiVersion => CurrentApiVersion;
    public static int ApiMajorVersion => CurrentApiMajorVersion;
    public static string PluginVersion => _pluginVersion;

    public static event Action ApiReady;
    public static event Action<string> SceneChanged;
    public static event Action<RageStateSnapshot> RageChanged;
    public static event Action<MindBrokenStateSnapshot> MindBrokenChanged;
    public static event Action<GoldStateSnapshot> GoldChanged;

    internal static void Initialize(string pluginVersion, ManualLogSource log)
    {
        if (_isReady)
            return;

        _pluginVersion = pluginVersion ?? string.Empty;
        _log = log;
        RageSystem.OnChanged += HandleRageChanged;
        MindBrokenSystem.OnChanged += HandleMindBrokenChanged;
        GoldWallet.OnChanged += HandleGoldChanged;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        _isReady = true;
        SafeRaise(ApiReady);
    }

    internal static void Shutdown()
    {
        if (!_isReady)
            return;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        GoldWallet.OnChanged -= HandleGoldChanged;
        MindBrokenSystem.OnChanged -= HandleMindBrokenChanged;
        RageSystem.OnChanged -= HandleRageChanged;
        _isReady = false;
        _log = null;
    }

    public static RageStateSnapshot GetRageState()
    {
        return new RageStateSnapshot(
            RageSystem.Enabled,
            RageSystem.Percent,
            RageSystem.IsActive,
            (HellGateRageTier)(int)RageSystem.CurrentTier,
            RageSystem.IsTier3Ready);
    }

    public static MindBrokenStateSnapshot GetMindBrokenState()
    {
        return new MindBrokenStateSnapshot(
            MindBrokenSystem.Enabled,
            MindBrokenSystem.Percent,
            MindBrokenSystem.IsCountdownActive,
            MindBrokenSystem.CountdownTimeRemaining,
            MindBrokenSystem.IsScriptedSequenceActive);
    }

    public static FactionReputationSnapshot GetFactionReputation(HellGateFaction faction)
    {
        return GetFactionReputation((int)faction);
    }

    public static FactionReputationSnapshot GetFactionReputation(int factionId)
    {
        return new FactionReputationSnapshot(
            factionId,
            PlayerFactionReputation.GetScore(factionId),
            PlayerFactionReputation.DescribeRelation(factionId));
    }

    public static GoldStateSnapshot GetGoldState()
    {
        return new GoldStateSnapshot(
            EconomicConfig.Enable,
            GoldWallet.Current,
            GoldWallet.ActiveSlotOneBased);
    }

    public static PregnancyStateSnapshot GetPregnancyState()
    {
        bool enabled = PregnancyConfig.Enable != null && PregnancyConfig.Enable.Value;
        return new PregnancyStateSnapshot(
            enabled,
            WitchPregnancyState.IsActive,
            WitchPregnancyState.HasPending,
            WitchPregnancyState.SourceFaction,
            WitchPregnancyState.PendingFaction,
            WitchPregnancyState.GestationElapsedSeconds,
            WitchPregnancyState.GestationTotalSeconds,
            WitchPregnancyState.ProgressRatio,
            WitchPregnancyState.CurrentTrimester);
    }

    private static void HandleRageChanged()
    {
        SafeRaise(RageChanged, GetRageState());
    }

    private static void HandleMindBrokenChanged()
    {
        SafeRaise(MindBrokenChanged, GetMindBrokenState());
    }

    private static void HandleGoldChanged(long oldValue, long newValue)
    {
        SafeRaise(GoldChanged, GetGoldState());
    }

    private static void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
    {
        SafeRaise(SceneChanged, scene.name ?? string.Empty);
    }

    private static void SafeRaise(Action handlers)
    {
        if (handlers == null)
            return;

        foreach (Action handler in handlers.GetInvocationList())
        {
            try { handler(); }
            catch (Exception ex) { _log?.LogWarning("[HellGateApi] Subscriber failed: " + ex.Message); }
        }
    }

    private static void SafeRaise<T>(Action<T> handlers, T value)
    {
        if (handlers == null)
            return;

        foreach (Action<T> handler in handlers.GetInvocationList())
        {
            try { handler(value); }
            catch (Exception ex) { _log?.LogWarning("[HellGateApi] Subscriber failed: " + ex.Message); }
        }
    }
}
