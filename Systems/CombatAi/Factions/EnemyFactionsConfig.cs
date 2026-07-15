using System;
using System.IO;
using BepInEx;
using UnityEngine;

namespace NoREroMod.Systems.CombatAi.Factions;

/// <summary>
/// Isolated config for Enemy Factions module.
/// Stored in HellGateJson/CombatAi/Factions.json to keep it independent from other modules.
/// </summary>
internal static class EnemyFactionsConfig
{
    private static EnemyFactionsSettings _cached;
    private static float _lastLoadTime = -999f;
    private const float ReloadInterval = 2f;

    public static bool Enable => Get().Enable;
    public static bool DebugLogging => Get().DebugLogging;
    public static bool BanditsIgnorePlayer => Get().BanditsIgnorePlayer;
    public static float BanditsVsDemonsRange => Get().BanditsVsDemonsRange;
    public static float ActivationDistanceFromPlayer => Get().ActivationDistanceFromPlayer;
    public static bool ActivationDistanceHorizontalOnly => Get().ActivationDistanceHorizontalOnly;
    /// <summary>Max |dy| (vertical) between enemy and player for faction activation / auto-provoke. 0 = unlimited (floors ignored). Applies even when ActivationDistanceHorizontalOnly is true.</summary>
    public static float ActivationMaxVerticalDelta => Get().ActivationMaxVerticalDelta;
    public static bool EnablePlayerProvocation => Get().EnablePlayerProvocation;
    public static float PlayerProvocationRadius => Get().PlayerProvocationRadius;
    public static bool PlayerProvocationSameFactionOnly => Get().PlayerProvocationSameFactionOnly;
    public static bool PlayerProvocationFromMagic => Get().PlayerProvocationFromMagic;
    public static bool PlayerProvocationBanditsOnly => Get().PlayerProvocationBanditsOnly;
    public static float PlayerProvocationDurationSeconds => Get().PlayerProvocationDurationSeconds;
    public static bool RequireAttackAnimationForFactionDamage => Get().RequireAttackAnimationForFactionDamage;
    public static bool DisableFactionDamageDuringHScene => Get().DisableFactionDamageDuringHScene;
    public static bool FreezeFactionAiDuringHScene => Get().FreezeFactionAiDuringHScene;
    public static bool EnableFriendlyFire => Get().EnableFriendlyFire;
    public static float BanditsDamagePerHit => Get().BanditsDamagePerHit;
    public static float DemonsDamagePerHit => Get().DemonsDamagePerHit;
    public static float HitCooldownSeconds => Get().HitCooldownSeconds;
    public static float FactionDamagePopupScale => Get().FactionDamagePopupScale;
    /// <summary>Max |dy| for faction pulse hits. 0 = use attacker Atkdistance + 1.</summary>
    public static float FactionDamageMaxVerticalDelta => Get().FactionDamageMaxVerticalDelta;
    /// <summary>When true, pulse proximity uses horizontal distance only (matches vanilla distance).</summary>
    public static bool FactionDamageHorizontalRangeOnly => Get().FactionDamageHorizontalRangeOnly;
    /// <summary>Max horizontal distance for inter-faction target pick (0 = unlimited). Vanilla crossbow engages at ~13.</summary>
    public static float FactionInterTargetMaxHorizontalDistance => Get().FactionInterTargetMaxHorizontalDistance;
    /// <summary>Max |dy| for inter-faction target pick (0 = unlimited). Crossbow AttackKind uses ~10.</summary>
    public static float FactionInterTargetMaxVerticalDelta => Get().FactionInterTargetMaxVerticalDelta;

    /// <summary>Per-enemy melee cap for faction pulse hits (matches spawn name or C# type).</summary>
    public static float TryGetMeleeReachOverride(EnemyDate attacker)
    {
        if (attacker == null || attacker.gameObject == null)
            return 0f;

        FactionMeleeReachEntry[] overrides = Get().FactionMeleeReachOverrides;
        if (overrides == null || overrides.Length == 0)
            return 0f;

        string objectName = attacker.gameObject.name ?? string.Empty;
        int cloneIdx = objectName.IndexOf("(Clone)", StringComparison.Ordinal);
        if (cloneIdx >= 0)
            objectName = objectName.Substring(0, cloneIdx).Trim();

        string typeName = attacker.GetType().Name ?? string.Empty;
        float best = 0f;
        for (int i = 0; i < overrides.Length; i++)
        {
            FactionMeleeReachEntry entry = overrides[i];
            if (entry == null || entry.Reach <= 0f || string.IsNullOrEmpty(entry.EnemyType))
                continue;
            if (string.Equals(entry.EnemyType, objectName, StringComparison.Ordinal) ||
                string.Equals(entry.EnemyType, typeName, StringComparison.Ordinal))
            {
                if (best <= 0f || entry.Reach < best)
                    best = entry.Reach;
            }
        }

        return best;
    }

    /// <summary>Per-enemy cap for faction target acquisition range (spawn name or C# type).</summary>
    public static float TryGetFactionTargetRangeOverride(EnemyDate self)
    {
        if (self == null || self.gameObject == null)
            return 0f;

        FactionTargetRangeEntry[] overrides = Get().FactionTargetRangeOverrides;
        if (overrides == null || overrides.Length == 0)
            return 0f;

        string objectName = self.gameObject.name ?? string.Empty;
        int cloneIdx = objectName.IndexOf("(Clone)", StringComparison.Ordinal);
        if (cloneIdx >= 0)
            objectName = objectName.Substring(0, cloneIdx).Trim();

        string typeName = self.GetType().Name ?? string.Empty;
        float best = 0f;
        for (int i = 0; i < overrides.Length; i++)
        {
            FactionTargetRangeEntry entry = overrides[i];
            if (entry == null || entry.Range <= 0f || string.IsNullOrEmpty(entry.EnemyType))
                continue;
            if (string.Equals(entry.EnemyType, objectName, StringComparison.Ordinal) ||
                string.Equals(entry.EnemyType, typeName, StringComparison.Ordinal))
            {
                if (best <= 0f || entry.Range < best)
                    best = entry.Range;
            }
        }

        return best;
    }

    // ---- Reputation → AI behavior thresholds ----
    public static float ReputationHostileThreshold => Get().ReputationHostileThreshold;
    public static float ReputationFriendlyThreshold => Get().ReputationFriendlyThreshold;
    public static bool HostileAutoProvokeInRadius => Get().HostileAutoProvokeInRadius;
    public static bool HostileBreaksBanditIgnore => Get().HostileBreaksBanditIgnore;
    public static bool FriendlyDisablesVanillaAggro => Get().FriendlyDisablesVanillaAggro;
    public static bool FriendlyBlocksProvocation => Get().FriendlyBlocksProvocation;
    /// <summary>
    /// At or above this reputation score, hits do not apply the provocation hostile timer
    /// (95 and below still react). Missing/zero in JSON defaults to 96 after load.
    /// </summary>
    public static float ProvocationIgnoredReputationThreshold => Get().ProvocationIgnoredReputationThreshold;
    public static bool EnableSignBasedPlayerAggro => Get().EnableSignBasedPlayerAggro;
    public static float PlayerAggroPeaceThreshold => Get().PlayerAggroPeaceThreshold;
    public static float HSceneCompletedReputationDelta => Get().HSceneCompletedReputationDelta;
    public static float HandoffReputationDelta => Get().HandoffReputationDelta;
    public static float KillReputationDelta => Get().KillReputationDelta;
    public static float KillReputationDeltaWhileRage => Get().KillReputationDeltaWhileRage;
    public static float PlayerAttackReputationDelta => Get().PlayerAttackReputationDelta;
    public static float PositiveDeltaMindBrokenThreshold => Get().PositiveDeltaMindBrokenThreshold;
    public static float PositiveDeltaMindBrokenMultiplier => Get().PositiveDeltaMindBrokenMultiplier;
    public static bool EnableRelationSpeedScaling => Get().EnableRelationSpeedScaling;
    public static float SpeedMultiplierAtMinus100 => Get().SpeedMultiplierAtMinus100;
    public static float SpeedMultiplierAtPlus100 => Get().SpeedMultiplierAtPlus100;
    public static float MinSpeedMultiplierClamp => Get().MinSpeedMultiplierClamp;
    public static float MaxSpeedMultiplierClamp => Get().MaxSpeedMultiplierClamp;
    public static bool EnableRelationVisionOverride => Get().EnableRelationVisionOverride;
    public static float VisionDistanceAtMinus100 => Get().VisionDistanceAtMinus100;
    public static float VisionDistanceAtPlus100 => Get().VisionDistanceAtPlus100;
    public static bool EnableDeescalationRollEvent => Get().EnableDeescalationRollEvent;
    public static float DeescalationRadius => Get().DeescalationRadius;
    public static float DeescalationDurationSeconds => Get().DeescalationDurationSeconds;
    public static float DeescalationLateAttackPenaltyStartSeconds => Get().DeescalationLateAttackPenaltyStartSeconds;
    public static float DeescalationRewardRelationDelta => Get().DeescalationRewardRelationDelta;
    public static float DeescalationLateAttackPenaltyDelta => Get().DeescalationLateAttackPenaltyDelta;
    public static float DeescalationTickSeconds => Get().DeescalationTickSeconds;

    public static string[] BanditTypes => Get().BanditTypes;
    public static string[] DemonTypes => Get().DemonTypes;
    public static string[] ChurchTypes => Get().ChurchTypes;
    public static string[] MafiaTypes => Get().MafiaTypes;
    public static string[] UndeadTypes => Get().UndeadTypes ?? new string[0];
    public static string[] MonsterTypes => Get().MonsterTypes ?? new string[0];
    public static string[] NeutralTypes => Get().NeutralTypes;
    public static string[] BossTypes => Get().BossTypes ?? new string[0];
    public static string[] BossExcludeTypes => Get().BossExcludeTypes ?? new string[0];
    public static FactionRelationEntry[] FactionRelations => Get().FactionRelations;
    public static FactionColorEntry[] FactionColors => Get().FactionColors;

    public static EnemyFactionsSettings Get()
    {
        if (_cached != null && Time.realtimeSinceStartup - _lastLoadTime < ReloadInterval)
            return _cached;

        _cached = LoadFromFile();
        _lastLoadTime = Time.realtimeSinceStartup;
        return _cached;
    }

    private static EnemyFactionsSettings LoadFromFile()
    {
        string path = GetConfigPath();
        if (!File.Exists(path))
            return EnemyFactionsSettings.Default();

        try
        {
            string json = File.ReadAllText(path);
            EnemyFactionsSettings loaded = JsonUtility.FromJson<EnemyFactionsSettings>(json);
            if (loaded == null)
                return EnemyFactionsSettings.Default();

            if (loaded.ProvocationIgnoredReputationThreshold <= 0f)
                loaded.ProvocationIgnoredReputationThreshold = 96f;

            return loaded;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[EnemyFactions] Failed to load config: " + ex.Message + ". Using defaults.");
            return EnemyFactionsSettings.Default();
        }
    }

    private static string GetConfigPath()
    {
        try
        {
            string combatAiDir = Path.Combine(Path.Combine(Paths.PluginPath, "HellGateJson"), "CombatAi");
            return Path.Combine(combatAiDir, "Factions.json");
        }
        catch
        {
            string basePath = Path.Combine(Application.dataPath, "..");
            string bepInEx = Path.Combine(basePath, "BepInEx");
            string plugins = Path.Combine(bepInEx, "plugins");
            string hellGateJson = Path.Combine(plugins, "HellGateJson");
            string combatAi = Path.Combine(hellGateJson, "CombatAi");
            return Path.Combine(combatAi, "Factions.json");
        }
    }

    [Serializable]
    public class EnemyFactionsSettings
    {
        public bool Enable = false;
        public bool DebugLogging = false;
        public bool BanditsIgnorePlayer = true;
        public float BanditsVsDemonsRange = 1.6f;
        public float ActivationDistanceFromPlayer = 10f;
        public bool ActivationDistanceHorizontalOnly = true;
        public float ActivationMaxVerticalDelta = 0f;
        public bool EnablePlayerProvocation = true;
        public float PlayerProvocationRadius = 12f;
        public bool PlayerProvocationSameFactionOnly = true;
        public bool PlayerProvocationFromMagic = false;
        public bool PlayerProvocationBanditsOnly = false;
        public float PlayerProvocationDurationSeconds = 15f;
        public bool RequireAttackAnimationForFactionDamage = true;
        public bool DisableFactionDamageDuringHScene = true;
        public bool FreezeFactionAiDuringHScene = true;
        public bool EnableFriendlyFire = false;
        public float BanditsDamagePerHit = 7f;
        public float DemonsDamagePerHit = 10f;
        public float HitCooldownSeconds = 0.65f;
        public float FactionDamagePopupScale = 0.55f;
        public float FactionDamageMaxVerticalDelta = 0f;
        public bool FactionDamageHorizontalRangeOnly = true;
        public float FactionInterTargetMaxHorizontalDistance = 13f;
        public float FactionInterTargetMaxVerticalDelta = 10f;
        /// <summary>EnemyType = spawn name (e.g. Wolf) or C# type (e.g. MummyDog).</summary>
        public FactionMeleeReachEntry[] FactionMeleeReachOverrides = new FactionMeleeReachEntry[]
        {
            new FactionMeleeReachEntry { EnemyType = "Wolf", Reach = 3f },
            new FactionMeleeReachEntry { EnemyType = "MummyDog", Reach = 3f }
        };
        public FactionTargetRangeEntry[] FactionTargetRangeOverrides = new FactionTargetRangeEntry[]
        {
            new FactionTargetRangeEntry { EnemyType = "SinnerslaveCrossbow", Range = 12f },
            new FactionTargetRangeEntry { EnemyType = "Dorei", Range = 12f }
        };

        // Reputation behavior thresholds. Scores are clamped to [-100, 100].
        // At or below HostileThreshold — faction members become permanently hostile to the player.
        // At or above FriendlyThreshold — faction members ignore the player (and optionally cannot be provoked).
        public float ReputationHostileThreshold = -50f;
        public float ReputationFriendlyThreshold = 65f;
        public bool HostileAutoProvokeInRadius = true;
        public bool HostileBreaksBanditIgnore = true;
        public bool FriendlyDisablesVanillaAggro = true;
        public bool FriendlyBlocksProvocation = true;
        /// <summary>Reputation at which provocation timer is skipped (inclusive). Default 96 → scores 96..100 ignore provocation.</summary>
        public float ProvocationIgnoredReputationThreshold = 96f;
        public bool EnableSignBasedPlayerAggro = true;
        public float PlayerAggroPeaceThreshold = 65f;
        public float HSceneCompletedReputationDelta = 3f;
        public float HandoffReputationDelta = 5f;
        public float PlayerAttackReputationDelta = -1f;
        public float KillReputationDelta = -6f;
        public float KillReputationDeltaWhileRage = -8f;
        public float PositiveDeltaMindBrokenThreshold = 70f;
        public float PositiveDeltaMindBrokenMultiplier = 1.25f;
        public bool EnableRelationSpeedScaling = true;
        public float SpeedMultiplierAtMinus100 = 1.1f;
        public float SpeedMultiplierAtPlus100 = 0.9f;
        public float MinSpeedMultiplierClamp = 0.9f;
        public float MaxSpeedMultiplierClamp = 1.1f;
        public bool EnableRelationVisionOverride = true;
        public float VisionDistanceAtMinus100 = 10f;
        public float VisionDistanceAtPlus100 = 1f;
        public bool EnableDeescalationRollEvent = true;
        public float DeescalationRadius = 7.5f;
        public float DeescalationDurationSeconds = 7f;
        public float DeescalationLateAttackPenaltyStartSeconds = 4f;
        public float DeescalationRewardRelationDelta = 5f;
        public float DeescalationLateAttackPenaltyDelta = -5f;
        public float DeescalationTickSeconds = 0.2f;

        public string[] BanditTypes = new string[]
        {
            "TouzokuNormal", "TouzokuAxe", "Vagrant", "VagrantGuard", "VagrantThrow",
            "BossTouzoku"
        };
        public string[] DemonTypes = new string[]
        {
            "Mutude", "Bigoni", "BigoniBrother", "goblin", "Goblin", "GobBigAlter", "GobRider",
            "Gorotuki", "Sheepheaddemon", "Minotaurosu", "Slaughterer",
            "CrawlingDead", "CrawlingSisterKnight", "CrawlingCreatures", "Arulaune",
            "Kakash", "Kakasi", "DarkPixie", "Candore", "SuccubusSpine", "Tentacle",
            "DemonRequiemKnight", "IvyRoadStop", "OriginIbaranoMajyo", "LastIbaranoMajyo",
            "BossScapegoatentrance", "BOSS_Village", "BossInsomniaUnder",
            "BossLeftinsomniaUnder", "BossRightinsomniaUnder"
        };
        public string[] ChurchTypes = new string[]
        {
            "Inquisition", "InquisitionRED", "InquisitionWhite", "CrowInquisition",
            "HighInquisition_famale", "HighInquisitionFemale",
            "Pilgrim", "RequiemKnight", "Sisterknight",
            "PrisonOfficer", "SinnerslaveCrossbow", "Dorei",
            "SlaveBigAxe", "OtherSlavebigAxe", "Librarian", "AngelStatue", "Praymaiden"
        };
        public string[] MafiaTypes = new string[]
        {
            "Mafia", "Mafiamuscle", "MafiaBossCustom", "BlackMafia",
            "Tyoukyoushi", "Tyoukyousi", "TyoukyoushiRed", "TyoukyousiRed",
            "Boss_Ranch"
        };
        public string[] UndeadTypes = new string[]
        {
            "Undead", "MummyDog", "MummyMan", "Cocoonman", "Sisiruirui"
        };
        public string[] MonsterTypes = new string[]
        {
            "Kinoko", "Snailshell", "NormalSnailshell",
            "BlackOoze_Monster", "BlackOoze", "SkeltonOoze",
            "BigMerman", "DifferentBigMerman", "Coolmaiden", "Mimick"
        };
        public string[] NeutralTypes = new string[] { "DPScheckWood" };
        /// <summary>Extra EnemyDate types (auto-detected via vanilla BOSSflag field when possible).</summary>
        public string[] BossTypes = new string[0];
        /// <summary>Never treat as story boss (e.g. MafiaBossCustom).</summary>
        public string[] BossExcludeTypes = new string[]
        {
            "MafiaBossCustom"
        };
        public FactionRelationEntry[] FactionRelations = new FactionRelationEntry[]
        {
            new FactionRelationEntry { Left = "bandits_inquisition", Right = "church", Relation = "friendly" },
            new FactionRelationEntry { Left = "bandits_mafia", Right = "mafia", Relation = "friendly" },
            new FactionRelationEntry { Left = "bandits_demons", Right = "demons", Relation = "friendly" }
        };
        public FactionColorEntry[] FactionColors = new FactionColorEntry[]
        {
            new FactionColorEntry { Faction = "bandits", Color = "#FFFFFF" },
            new FactionColorEntry { Faction = "bandits_inquisition", Color = "#A7D3FF" },
            new FactionColorEntry { Faction = "bandits_mafia", Color = "#FFD27A" },
            new FactionColorEntry { Faction = "bandits_demons", Color = "#FF8A8A" }
        };

        public static EnemyFactionsSettings Default()
        {
            return new EnemyFactionsSettings();
        }
    }

    [Serializable]
    public class FactionRelationEntry
    {
        public string Left;
        public string Right;
        public string Relation;
    }

    [Serializable]
    public class FactionColorEntry
    {
        public string Faction;
        public string Color;
    }

    [Serializable]
    public class FactionMeleeReachEntry
    {
        public string EnemyType;
        public float Reach;
    }

    [Serializable]
    public class FactionTargetRangeEntry
    {
        public string EnemyType;
        public float Range;
    }
}
