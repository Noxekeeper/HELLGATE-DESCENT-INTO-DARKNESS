using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Rewired;
using UnityEngine;
using UnityEngine.SceneManagement;
using static ES2;
using Random = UnityEngine.Random;
using NoREroMod.Patches.UI.MindBroken;
using NoREroMod.Systems.UI;
using NoREroMod.Patches.Enemy;
using NoREroMod.Patches.Enemy.CrowInquisition;
using NoREroMod.Patches.Player;
using NoREroMod.Patches.Enemy.Six_hand;
using NoREroMod.Patches.Enemy.Kakash;
using NoREroMod.Patches.Base;
using NoREroMod.Systems.Dialogue;
using NoREroMod.Systems.Rage;
using NoREroMod.Systems.Cache;
using NoREroMod.Systems.Audio;

namespace NoREroMod;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInProcess("NightofRevenge.exe")]
public class Plugin : BaseUnityPlugin {

    public static ConfigEntry<float> enemyHPMultiMax;
    public static ConfigEntry<float> enemyHPMultiMin;
    public static ConfigEntry<float> enemySpeedMulti;
    public static ConfigEntry<float> enemyPoiseMulti;
    public static ConfigEntry<float> enemyEXPMulti; // v0.11.5

    public static ConfigEntry<float> bossHPMulti; // v0.11.5
    public static ConfigEntry<float> bossEXPMulti; // v0.11.5

    public static ConfigEntry<float> pleasureAfterOrgasm;
    public static ConfigEntry<float> pleasureEnemyAttackMax;
    public static ConfigEntry<float> pleasureEnemyAttackMin;
    public static ConfigEntry<float> pleasurePlayerAttackMax;
    public static ConfigEntry<float> pleasurePlayerAttackMin;
    public static ConfigEntry<float> pleasureAttackSpeedMax;
    public static ConfigEntry<float> pleasureAttackSpeedMin;
    public static ConfigEntry<float> pleasureGainOnEro;
    public static ConfigEntry<float> pleasureGainOnHit;
    public static ConfigEntry<float> pleasureLossOnHit; // v0.11.3 Edited
    public static ConfigEntry<float> pleasureGainOnBlock;
    public static ConfigEntry<float> pleasureGainOnDown;
    public static ConfigEntry<bool> enablePregnancy;
    public static ConfigEntry<bool> enableAnyPregnancy; // v0.12.0
    public static ConfigEntry<float> pregnancyChance; // v0.12.0
    public static ConfigEntry<float> extraBirthChance;
    public static ConfigEntry<bool> disablePleasureParalysis;
    public static ConfigEntry<float> orgasmFlashStrength; // v0.11.3 Edited


    public static ConfigEntry<float> hpLosePerSec;
    public static ConfigEntry<float> hpLoseOnCreampie;
    public static ConfigEntry<bool> enableDelevel;
    public static ConfigEntry<float> expLosePerSec;
    public static ConfigEntry<float> expLoseOnCreampie;
    public static ConfigEntry<float> animationExpLoseMulti;
    public static ConfigEntry<float> expDelevelRefundPercent;
    public static ConfigEntry<float> pleasureSPRegenMax;
    public static ConfigEntry<float> pleasureSPRegenMin;
    public static ConfigEntry<float> spLosePercentOnEroEvent;
    public static ConfigEntry<float> spPercentGainOnStruggleDown;
    public static ConfigEntry<float> spPercentGainOnStruggleEro;
    public static ConfigEntry<float> spPercentLoseOnBadStruggleEro;
    public static ConfigEntry<float> animationHPDamageMulti;
    public static ConfigEntry<float> animationPleasureDamageMulti;
    public static ConfigEntry<float> easyStruggleCount; // v0.11.3 Edited
    public static ConfigEntry<float> fatalityDifficulty; // v0.11.5 Rebalanced
    public static ConfigEntry<bool> fatalityEasyStruggles; // v0.11.5 Rebalanced
    public static ConfigEntry<bool> bossEasyStruggles; // v0.11.5 Rebalanced
    public static ConfigEntry<bool> bossStruggleFatigue; // v0.11.5 Rebalanced
    public static ConfigEntry<float> enemyHealthEffectiveness; // v0.11.5 Rebalanced
    public static ConfigEntry<float> playerHealthEffectiveness; // v0.11.5 Rebalanced
    public static ConfigEntry<float> spFactorEffectiveness; // v0.12.0
    public static ConfigEntry<float> playerMpEffectiveness; // v0.11.5 Rebalanced
    public static ConfigEntry<float> playerPleasureEffectiveness; // v0.11.5 Rebalanced
    public static ConfigEntry<float> struggleHpDifficultyPercent;
    public static ConfigEntry<float> strugglePleasureDifficultyPercent;
    public static ConfigEntry<bool> enableCriticalStruggle;
    public static ConfigEntry<bool> allowStrugglePotion;
    public static ConfigEntry<bool> enableImpossibleStruggles;

    public static ConfigEntry<float> mpGainPerHit;
    public static ConfigEntry<float> spCostPerGuard;
    public static ConfigEntry<float> spCostPerDash;
    public static ConfigEntry<float> spRegenIdle;
    public static ConfigEntry<float> spRegenGuard;
    public static ConfigEntry<bool> hiddenHPBars;

    // GrabSystem NG (GrabViaAttack)
    public static ConfigEntry<bool> enableGrabViaAttack;
    public static ConfigEntry<bool> disableOriginalEliteGrab;
    public static ConfigEntry<bool> grabViaAttackEliteOnly;
    public static ConfigEntry<float> grabChanceMelee;
    public static ConfigEntry<float> grabChancePowerAttack;
    public static ConfigEntry<float> grabChanceThroughBlock;
    public static ConfigEntry<float> grabChancePowerThroughBlock;
    public static ConfigEntry<float> grabChanceMindBrokenBonusPer10Percent;
    public static ConfigEntry<float> grabChanceRageReductionPerPercent;
    public static ConfigEntry<float> grabChancePleasureBonusMax;
    public static ConfigEntry<bool> grabViaAttackSlowmo;
    public static ConfigEntry<float> grabViaAttackSlowmoTimeScale;
    public static ConfigEntry<float> grabViaAttackSlowmoDuration;

    public static ConfigEntry<bool> enableFoV;
    public static ConfigEntry<float> frontViewDistance;
    public static ConfigEntry<float> backViewDistance;

    public static ConfigEntry<bool> isHardcoreMode;

    public static ConfigEntry<bool> trappedSavePoints;
    public static ConfigEntry<bool> shrinesRetoreVirginity; // v0.11.5 Rebalanced

    // Wolf Mod - path to Wolf Mod Spine assets
    public static ConfigEntry<string> wolfModAssetsPath;

    // Dorei Mod - path to DoreiFapping (idle during H-scene spectator)
    public static ConfigEntry<string> doreiFappingAssetsPath;
    public static ConfigEntry<float> doreiSpectatorScaleMultiplier;

    // New handoff system configs
    public static ConfigEntry<bool> enableEnemyHandoff;
    public static ConfigEntry<float> handoffCooldownTime;
    public static ConfigEntry<bool> enableHandoffVisualEffects;
    
    // Enemy Pass Mechanic (Cloud Solution)
    public static ConfigEntry<bool> enableEnemyPass;
    public static ConfigEntry<int> cyclesBeforePass;
    public static ConfigEntry<float> pushDistance;
    public static ConfigEntry<float> minCycleInterval;
    public static ConfigEntry<float> handoffDelay;
    public static ConfigEntry<bool> enableDirtyTalkMessages;
    public static ConfigEntry<bool> enableHandoffMessages;
    
    // Mind Broken system configs
    public static ConfigEntry<bool> enableMindBroken;
    public static ConfigEntry<float> mindBrokenPercentPerPass;
    public static ConfigEntry<float> mindBrokenMaxPercent;
    public static ConfigEntry<float> mindBrokenStruggleBonusPerStep;
    public static ConfigEntry<float> mindBrokenBadEndCountdownDuration;
    public static ConfigEntry<float> mindBrokenBadEndResetThreshold;
    public static ConfigEntry<bool> mindBrokenHighRagePassiveEnable;
    public static ConfigEntry<float> mindBrokenHighRageThresholdPercent;
    public static ConfigEntry<float> mindBrokenHighRagePassivePercentPerSecond;
    public static ConfigEntry<bool> mindBrokenHighRagePassiveOnlyWhenRageInactive;

    // MindBroken gain during special H-scene states
    public static ConfigEntry<float> hsceneBlackBackgroundMindBrokenPerSecondPercent;
    public static ConfigEntry<float> mutudeMindBrokenPerSecondPercent;

    // Corruption Captions system configs
    public static ConfigEntry<bool> enableCorruptionCaptions;
    public static ConfigEntry<float> corruptionCaptionCooldown;
    
    // MindBroken Recovery system configs
    public static ConfigEntry<bool> enableMindBrokenRecovery;
    public static ConfigEntry<float> recoveryPercentPerKill;
    public static ConfigEntry<float> recoveryPercentPerBossKill;
    public static ConfigEntry<string> recoveryBossNames;
    public static ConfigEntry<float> recoveryCaptionCooldown;

    // MindBroken Visual Effects system configs
    public static ConfigEntry<float> mbFogAppearanceThreshold;
    public static ConfigEntry<float> mbFogColorR;
    public static ConfigEntry<float> mbFogColorG;
    public static ConfigEntry<float> mbFogColorB;
    public static ConfigEntry<float> mbFogMaxAlpha;
    public static ConfigEntry<float> mbFogPulseSpeed;
    public static ConfigEntry<float> mbFogCenterRadiusMin;
    public static ConfigEntry<float> mbFogCenterRadiusMax;
    public static ConfigEntry<float> mbNegativeEffectDuration;
    public static ConfigEntry<float> mbNegativeActivationThreshold;
    public static ConfigEntry<float> mbNegativeActivationStep;
    public static ConfigEntry<float> mbDreamEffectSpeed;
    public static ConfigEntry<float> mbDreamEffectDistortion;
    public static ConfigEntry<float> mbFlashStartThreshold;
    public static ConfigEntry<float> mbFlashDuration;
    public static ConfigEntry<int> mbFlashPulseCycles;
    public static ConfigEntry<float> mbFlashMinAlpha;
    public static ConfigEntry<float> mbFlashMaxAlpha;
    public static ConfigEntry<float> mbFlashColorR;
    public static ConfigEntry<float> mbFlashColorG;
    public static ConfigEntry<float> mbFlashColorB;
    public static ConfigEntry<float> mbFlashFadeOutTime;
    public static ConfigEntry<float> mbDreamDuration;
    public static ConfigEntry<float> mbDreamFadeInTime;
    public static ConfigEntry<float> mbDreamFadeOutTime;

    // InquisitionWhite MindBroken configs
    public static ConfigEntry<bool> inquisitionWhiteEnableWaveEffect;
    public static ConfigEntry<float> inquisitionWhiteMindBrokenPerSecond;

    // CrowInquisition MindBroken configs
    public static ConfigEntry<float> crowInquisitionMindBrokenPerSecondIKI;
    public static ConfigEntry<float> crowInquisitionMindBrokenPerSecondIKI2;

    // Pilgrim MindBroken configs
    public static ConfigEntry<float> pilgrimMindBrokenPerSecondBell;

    // Rage Mode system configs
    public static ConfigEntry<bool> enableRageMode;
    public static ConfigEntry<float> rageCritMultiplier;
    public static ConfigEntry<float> rageBaseMindBrokenGainPerSecondPercent;
    public static ConfigEntry<int> rageHandsParticleMaxParticles;
    public static ConfigEntry<bool> ragePerformanceMode;
    public static ConfigEntry<float> rageGainPerKill;
    public static ConfigEntry<float> rageGainPerBossKill;
    public static ConfigEntry<float> ragePassiveTickAmount;
    public static ConfigEntry<float> ragePassiveTickInterval;
    public static ConfigEntry<float> rageActivationCost;
    public static ConfigEntry<float> rageActivationDuration;
    public static ConfigEntry<float> rageCooldownDuration;
    public static ConfigEntry<float> timeSlowMoTimeScale;
    public static ConfigEntry<float> timeSlowMoRageDrainPerSecond;
    
    // Rage Mode - Advanced Settings
    public static ConfigEntry<float> rageMinActivationPercent;
    public static ConfigEntry<float> rageCostDuringQTE;
    public static ConfigEntry<float> rageTier1Threshold;
    public static ConfigEntry<float> rageTier2Threshold;
    public static ConfigEntry<float> rageTier3OverflowThreshold;
    public static ConfigEntry<float> rageTier1Duration;
    public static ConfigEntry<float> rageTier2Duration;
    public static ConfigEntry<float> rageTier3Duration;
    public static ConfigEntry<float> rageDamageMultiplier;
    public static ConfigEntry<float> rageSPGainPercent;
    public static ConfigEntry<float> rageEnemyPushbackForceMultiplier;
    public static ConfigEntry<float> ragePushbackMinDistance;
    public static ConfigEntry<float> ragePushbackMaxDistance;
    public static ConfigEntry<float> ragePushbackVerticalBoost;
    public static ConfigEntry<bool> rageActivationCameraShake;
    public static ConfigEntry<bool> ragePushbackApplyFalter;
    public static ConfigEntry<float> ragePushbackFalterDuration;
    public static ConfigEntry<float> rageGrabDrainMin;
    public static ConfigEntry<float> rageGrabDrainMax;
    public static ConfigEntry<float> rageSlowMoDrainMultiplier;
    public static ConfigEntry<float> rageSlowMoMBGainMultiplier;
    public static ConfigEntry<float> rageUIPositionX;
    public static ConfigEntry<float> rageUIPositionY;
    public static ConfigEntry<float> rageBloodEffectDuration;
    public static ConfigEntry<int> ragePushbackMaxEnemies;
    public static ConfigEntry<float> rageOutburstFuryDrainPerSecond;
    public static ConfigEntry<float> rageKillTimeoutSeconds;
    public static ConfigEntry<float> rageComboTimeout;
    public static ConfigEntry<float> rageComboBaseGain;
    public static ConfigEntry<float> rageComboGainMultiplier;
    public static ConfigEntry<float> rageResetHCPenaltyGrab;
    public static ConfigEntry<float> rageResetHCPenaltyKnockdown;
    public static ConfigEntry<float> rageKeyPressCooldown;

    // Rage Visual Effects (edge glow bars, hands glow)
    public static ConfigEntry<float> rageGlowColorR;
    public static ConfigEntry<float> rageGlowColorG;
    public static ConfigEntry<float> rageGlowColorB;
    public static ConfigEntry<float> rageGlowMaxAlpha;
    public static ConfigEntry<bool> rageHandsGlowEnable;
    public static ConfigEntry<float> rageHandsGlowColorR;
    public static ConfigEntry<float> rageHandsGlowColorG;
    public static ConfigEntry<float> rageHandsGlowColorB;
    public static ConfigEntry<float> rageHandsGlowAlpha;
    public static ConfigEntry<float> rageHandsGlowSizePx;

    // Rage Hands Particle Effects
    public static ConfigEntry<bool> rageHandsParticleEnable;
    public static ConfigEntry<float> rageHandsParticleEmissionRate;
    public static ConfigEntry<float> rageHandsParticleSize;
    public static ConfigEntry<float> rageHandsParticleColorR;
    public static ConfigEntry<float> rageHandsParticleColorG;
    public static ConfigEntry<float> rageHandsParticleColorB;

    // Tier 3 demon wings (kubi bone sprite loop)
    public static ConfigEntry<bool> rageWingsEnable;
    public static ConfigEntry<float> rageWingsDurationSeconds;
    public static ConfigEntry<float> rageWingsFps;
    public static ConfigEntry<float> rageWingsScale;
    public static ConfigEntry<float> rageWingsOffsetX;
    public static ConfigEntry<float> rageWingsOffsetY;

    // SlowMo visual effects (edge bars top/bottom, bone glow)
    public static ConfigEntry<float> slowMoEdgeBarsColorR;
    public static ConfigEntry<float> slowMoEdgeBarsColorG;
    public static ConfigEntry<float> slowMoEdgeBarsColorB;
    public static ConfigEntry<float> slowMoEdgeBarsMaxAlpha;
    public static ConfigEntry<bool> slowMoBoneGlowEnable;
    public static ConfigEntry<float> slowMoBoneGlowColorR;
    public static ConfigEntry<float> slowMoBoneGlowColorG;
    public static ConfigEntry<float> slowMoBoneGlowColorB;
    public static ConfigEntry<float> slowMoBoneGlowAlpha;
    public static ConfigEntry<float> slowMoBoneGlowSizePx;
    

    // MindBroken fog pulse amplitude (0.03 = barely visible)
    public static ConfigEntry<float> mbFogPulseAmount;

    public static ConfigEntry<float> qteSuccessVolumeMultiplier;
    public static ConfigEntry<float> qteFailureVolumeMultiplier;
    
    // QTE System 3.0 configs
    public static ConfigEntry<float> qteSPGainBase;
    public static ConfigEntry<float> qteSPGainMin;
    public static ConfigEntry<float> qteYellowButtonSPGainMin;
    public static ConfigEntry<float> qteYellowButtonSPGainMax;
    public static ConfigEntry<float> qteClickSPGainBase;
    public static ConfigEntry<float> qteClickSPGainMin;
    public static ConfigEntry<float> qteMPPenaltyPercent;
    public static ConfigEntry<float> qteMindBrokenPenaltyPercent;
    public static ConfigEntry<float> qteRedButtonMindBrokenPenalty;
    public static ConfigEntry<float> qteSPPenaltyMultiplier;
    public static ConfigEntry<float> qteWindowDurationMin;
    public static ConfigEntry<float> qteWindowDurationMax;
    public static ConfigEntry<float> qteCooldownDurationMin;
    public static ConfigEntry<float> qteCooldownDurationMax;
    public static ConfigEntry<float> qteButtonPositionY;
    public static ConfigEntry<float> qteButtonSpacing;
    public static ConfigEntry<float> qteColorChangeInterval;
    public static ConfigEntry<float> qtePressIndicatorDuration;
    public static ConfigEntry<float> qteMaxButtonTransparency;
    public static ConfigEntry<float> qteMaxPinkShadowIntensity;
    public static ConfigEntry<int> qteComboMilestone;
    public static ConfigEntry<bool> enableQTESystem;
    
    // H-Scene Effects configs
    public static ConfigEntry<bool> enableStartZoomEffect;
    public static ConfigEntry<float> startZoomAmount;
    public static ConfigEntry<float> startZoomDuration;
    public static ConfigEntry<float> startSlowmoDelay;
    public static ConfigEntry<float> startSlowmoTimeScale;
    public static ConfigEntry<float> startSlowmoDuration;
    public static ConfigEntry<bool> enableStartCenter;
    public static ConfigEntry<float> startCenterDuration;
    public static ConfigEntry<float> startCenterYOffset;
    
    // Splash Screen configs
    public static ConfigEntry<bool> showSplashScreenOnStartup;
    public static ConfigEntry<string> hellGateLanguage;
    public static ConfigEntry<bool> enableAttackSounds;
    public static ConfigEntry<float> attackSoundsVolume;
    public static ConfigEntry<bool> enableThreatSounds;
    public static ConfigEntry<bool> enableGrabThreats;
    public static ConfigEntry<bool> enableGrabThreatsText;
    public static ConfigEntry<float> threatSoundsVolume;
    public static ConfigEntry<bool> enableDeathSounds;
    public static ConfigEntry<float> deathSoundsVolume;
    public static ConfigEntry<float> threatSoundsGlobalCooldown;
    public static ConfigEntry<float> threatSoundsPerEnemyCooldown;
    public static ConfigEntry<float> attackSoundsGlobalInterval;
    public static ConfigEntry<float> attackSoundsPerAttackerInterval;

    // BadEnd Player (prototype) configs
    public static ConfigEntry<bool> enableBadEndPlayer;
    
    // Take Vengeance / PlayerRespawn configs
    public static ConfigEntry<float> vengeanceMindBrokenReduceFraction;
    public static ConfigEntry<float> vengeanceRageBonusPercent;
    public static ConfigEntry<float> vengeanceRageDrainFractionOfCurrent;
    public static ConfigEntry<float> vengeanceRageMaxPercentAfter;
    public static ConfigEntry<bool> badEndTakeVengeanceRespawnEnemies;
    public static ConfigEntry<float> badEndTakeVengeanceEnemyRespawnDelay;
    
    // Visual indicators configs
    public static ConfigEntry<bool> disableStruggleCameraShake;
    public static ConfigEntry<bool> enableStruggleVisualIndicators;
    public static ConfigEntry<bool> showDifficultyIndicator;
    public static ConfigEntry<bool> showProgressIndicator;
    public static ConfigEntry<bool> showCriticalChanceIndicator;

    // Dialogue font configs
    public static ConfigEntry<float> dialogueFontSize;
    public static ConfigEntry<int> enemyFontStyle;
    public static ConfigEntry<int> aradiaResponseFontStyle;
    public static ConfigEntry<int> aradiaThoughtFontStyle;
    public static ConfigEntry<int> spectatorFontStyle;
    public static ConfigEntry<int> threatFontStyle;
    public static ConfigEntry<string> enemyColor;
    public static ConfigEntry<string> enemyOutlineColor;
    public static ConfigEntry<string> aradiaResponseColor;
    public static ConfigEntry<string> aradiaResponseOutlineColor;
    public static ConfigEntry<string> aradiaThoughtColor;
    public static ConfigEntry<string> aradiaThoughtOutlineColor;
    public static ConfigEntry<string> spectatorColor;
    public static ConfigEntry<string> spectatorOutlineColor;
    public static ConfigEntry<string> threatColor;
    public static ConfigEntry<string> threatOutlineColor;
    
    // Touzoku aggression configs
    public static ConfigEntry<float> touzokuSpeedMultiplier;
    public static ConfigEntry<float> touzokuAttackRangeMultiplier;
    
    // Goblin hardcore features
    public static ConfigEntry<bool> enableGoblinStruggleSpawn;
    
    // BigoniBrother START2 animation configs
    public static ConfigEntry<int> bigoniBrotherStart2RepeatCount;
    public static ConfigEntry<float> bigoniBrotherStart2TimeScale;
    
    // CumDisplay configs
    public static ConfigEntry<float> cumDisplayFrameDuration;
    public static ConfigEntry<float> cumDisplayAnchoredOffsetX;
    public static ConfigEntry<float> cumDisplayAnchoredOffsetY;
    public static ConfigEntry<float> cumDisplayOralOffsetYDelta;
    public static ConfigEntry<float> cumDisplayPregnantOffsetX;
    public static ConfigEntry<float> cumDisplayPregnantOffsetY;
    public static ConfigEntry<float> cumDisplayWorldDepth;
    public static ConfigEntry<float> cumDisplaySizeMultiplier;
    
    // SoundOnomatopoeia configs
    public static ConfigEntry<float> soundOnomatopoeiaTimeout;
    
    // DialogueEventProcessor configs
    public static ConfigEntry<float> dialogueEventMinCooldown;
    
    // Combat Camera Preset configs (V key)
    public static ConfigEntry<bool> enableCombatCameraPresets;
    public static ConfigEntry<float> combatCameraFarZoom;
    public static ConfigEntry<float> combatCameraUltraFarZoom;

    // H-Scene Camera Zoom configs
    public static ConfigEntry<float> cameraZoomLevel10x;
    public static ConfigEntry<float> cameraZoomLevel8x;
    public static ConfigEntry<float> cameraZoomLevel5x;
    public static ConfigEntry<float> cameraZoomLevel4x;
    public static ConfigEntry<float> cameraZoomLevel3x;
    public static ConfigEntry<float> cameraZoomLevel2x;
    public static ConfigEntry<float> cameraZoomResetValue;

    public static float giveUpHoldTimer = 0f;

    public static float totalExpToLose = 0f;

    public static bool isSavePointTrapped = false;
    public static float savePointAwayTimer = 0f;

    public static GameObject lastBreedBy = null;

    public static float lastOrgasmTime = 0f; // v0.11.3 Edited
    public static float lastAnyClimaxTime = 0f; // v0.11.3 Edited
    public static Player player = null; // v0.11.3 Edited
    public static float eroSpeedWithoutOverride = 1f; // v0.11.3 Edited
    public static float eroSpeedActual = 1f; // v0.11.3 Edited

    public static bool isOrgasming = false;
    public static bool isBirthing = false;

    internal static ManualLogSource Log;
    internal static Plugin Instance { get; private set; }
    private Harmony harmony;
    private static Harmony _harmonyForLatePatches;

    private void Awake() {
        Instance = this;
        Log = Logger;
        harmony = new Harmony(PluginInfo.PLUGIN_GUID);
        SetUpConfigs();
        SetUpPatches();
        
        // Initialize visual indicators
        if (enableStruggleVisualIndicators.Value) {
            StruggleVisualIndicators.Initialize();
        }
        
        // Initialize dialogue system
        try {
            NoREroMod.Systems.Dialogue.DialogueFramework.Initialize();
            NoREroMod.Systems.Dialogue.QTEReactionFramework.Initialize();
        } catch { }
        
        // Initialize H-Scene camera system
        try {
            NoREroMod.Systems.Camera.HSceneCameraController.Initialize();
            NoREroMod.Systems.Camera.CameraCache.InitializeProCamera2DReflection();
            NoREroMod.Systems.Camera.CameraCache.InitializePlayerconReflection();
            NoREroMod.Systems.Camera.CameraCache.InitializeCameraTargetsReflection();
        } catch { }
        
        // Initialize H-Scene Effects
        try {
            NoREroMod.Systems.HSceneEffects.HSceneStartZoomEffect.Initialize();
        } catch { }
        
        // Initialize Corruption Captions system
        try {
            if (enableCorruptionCaptions?.Value ?? false) {
                NoREroMod.Patches.UI.MindBroken.CorruptionCaptionsSystem.Initialize();
            }
        } catch { }
        
        // Initialize MindBroken Recovery system
        try {
            if (enableMindBrokenRecovery?.Value ?? false) {
                NoREroMod.Patches.UI.MindBroken.MindBrokenRecoverySystem.Initialize();
            }
        } catch { }
        
        try {
            if (showSplashScreenOnStartup?.Value ?? true)
                StartCoroutine(ShowSplashScreenImmediately());
        } catch { }

        // Reset caches on scene change — prevents "click disable" during struggle due to stale player/camera refs
        SceneManager.sceneLoaded += OnSceneLoaded_ResetCaches;

        // BadEnd Player shows only when BadEnd triggers (MindBroken 100% + timer), not in main menu.
        
        // Initialize MindBroken Visual Effects system
        try {
            if (enableMindBroken?.Value ?? false) {
                NoREroMod.Patches.UI.MindBroken.MindBrokenVisualEffectsSystem.Initialize();
            }
        } catch { }
        
        // Initialize Rage Mode system
        try {
            if (enableRageMode?.Value ?? false) {
                NoREroMod.Systems.Rage.RageUISystem.InitializeFromPlugin();
                NoREroMod.Systems.Rage.RageInputHandler.EnsureCreated();
            }
        } catch { }

        // Initialize GrabChance UI (always on in gameplay)
        try {
            NoREroMod.Systems.GrabSystem.GrabChanceUISystem.InitializeFromPlugin();
        } catch { }
        
        // Edge bars disabled for performance (particle effects on hands are sufficient)
        try {
            NoREroMod.Systems.Rage.RageHandsGlowSystem.Initialize();
        } catch { }
        try {
            NoREroMod.Systems.Rage.RageHandsParticleSystem.Initialize();
        } catch { }
        try {
            NoREroMod.Systems.Rage.RageWingsSystem.Initialize();
        } catch { }
        // SlowMo edge bars disabled for performance
        try {
            NoREroMod.Systems.Rage.SlowMoBoneGlowSystem.Initialize();
        } catch { }

        // Startup compatibility probe for reflection-heavy integration points.
        RunNoREroModCompatibilityProbe();
    }

    private static void RunNoREroModCompatibilityProbe()
    {
        try
        {
            var checksPassed = 0;
            var checksTotal = 0;

            checksPassed += CheckTypeContract(
                "NoREroMod.PlayerConPatch",
                new[] { "inPraymaidenStruggle" },
                new[] { "UpdateStruggleHistory" },
                ref checksTotal
            );

            checksPassed += CheckTypeContract(
                "NoREroMod.EnemyDatePatch",
                null,
                new[] { "CanEliteGrabPlayer", "EliteGrabPlayer" },
                ref checksTotal
            );

            checksPassed += CheckTypeContract(
                "NoREroMod.UImngPatch",
                null,
                new[] { "WhiteFadeIn", "UpdateGrabStateWithColor" },
                ref checksTotal
            );

            Log?.LogInfo($"[Compat] NoREroMod probe: {checksPassed}/{checksTotal} checks passed");
        }
        catch (Exception ex)
        {
            Log?.LogWarning($"[Compat] Probe failed: {ex.Message}");
        }
    }

    private static int CheckTypeContract(string typeName, string[] requiredFields, string[] requiredMethods, ref int totalChecks)
    {
        var type = AccessTools.TypeByName(typeName);
        totalChecks++;
        if (type == null)
        {
            Log?.LogWarning($"[Compat] Missing type: {typeName}");
            return 0;
        }

        var passed = 1;

        if (requiredFields != null)
        {
            for (int i = 0; i < requiredFields.Length; i++)
            {
                totalChecks++;
                var field = AccessTools.Field(type, requiredFields[i]);
                if (field == null)
                {
                    Log?.LogWarning($"[Compat] Missing field: {typeName}.{requiredFields[i]}");
                }
                else
                {
                    passed++;
                }
            }
        }

        if (requiredMethods != null)
        {
            for (int i = 0; i < requiredMethods.Length; i++)
            {
                totalChecks++;
                var methods = AccessTools.GetDeclaredMethods(type);
                var exists = methods != null && methods.Any(m => m.Name == requiredMethods[i]);
                if (!exists)
                {
                    Log?.LogWarning($"[Compat] Missing method: {typeName}.{requiredMethods[i]}");
                }
                else
                {
                    passed++;
                }
            }
        }

        return passed;
    }

    private void SetUpPatches() {
        _harmonyForLatePatches = harmony;
        PatchType(typeof(PreventHarmonyUnpatch));
        
        PatchType(typeof(QTEStruggleSystemDisabler));
        PatchType(typeof(QTEStruggleHistoryDisabler));
        PatchType(typeof(StruggleCameraShakeDisabler));
        

        PatchType(typeof(StruggleVisualIndicators));
        PatchType(typeof(TouzokuNormalPassPatch));
        PatchType(typeof(TouzokuAxePassPatch));
        PatchType(typeof(InquisitionBlackPassPatch));
        PatchType(typeof(InquisitionWhitePassPatch));
        PatchType(typeof(InquisitionRedPassPatch));
        PatchType(typeof(VagrantPassPatch));
        PatchType(typeof(PrisonOfficerPassPatch));
        PatchType(typeof(LibrarianPassPatch));
        PatchType(typeof(MummyDogPassPatch));
        PatchType(typeof(PilgrimPassPatch));
        PatchType(typeof(MummyManPassPatch));
        PatchType(typeof(UndeadPassPatch));
        PatchType(typeof(CrowInquisitionEROFix)); // Fix animation skipping from Hellachaz
        PatchType(typeof(CrowInquisitionPassLogic)); // Handoff in gangbang
        CrowInquisitionEROFix.UnpatchHellachaz(); // Remove Hellachaz patches immediately
        PatchType(typeof(SpawnPointAnalyzer));
        PatchType(typeof(HellGateSpawn_FirstMap)); // FirstMap fixed position spawn system
        PatchType(typeof(HellGateSpawn_VillageMain));
        PatchType(typeof(HellGateSpawn_ScapegoatEntrance)); // VillageMain fixed position spawn system
        PatchType(typeof(HellGateSpawn_UndergroundChurch)); // UndergroundChurch fixed position spawn system
        PatchType(typeof(HellGateSpawn_InundergroundChurch)); // InundergroundChurch fixed position spawn system
        PatchType(typeof(HellGateSpawn_InsomniaTownC)); // Nightless City C (InsomniaTownC) spawn system
        PatchType(typeof(HellGateSpawn_ForestOfRequiem)); // Hidden Forest Area (ForestOfRequiem) spawn system
        PatchType(typeof(HellGateSpawn_UndergroundLaboratory)); // Underground Laboratory spawn system
        PatchType(typeof(HellGateSpawn_PilgrimageEntrance)); // Pilgrimage Entrance spawn system
        PatchType(typeof(HellGateSpawn_WhiteCathedral)); // White Cathedral spawn system
        PatchType(typeof(HellGateSpawn_WhiteCathedralGarden)); // White Cathedral Garden spawn system
        PatchType(typeof(HellGateSpawn_WhiteCathedralRooftop)); // White Cathedral Rooftop spawn system
        PatchType(typeof(SpawnResetPatch));
        PatchType(typeof(NoREroMod.Patches.Spawn.SpawnSceneTransitionFix)); // Ghost spawn fix for scene transitions
        PatchType(typeof(KakasiPassLogic));
        PatchType(typeof(KakasiCrossPatch));
        
        try {
            harmony.PatchAll(typeof(NoREroMod.Patches.Enemy.Kakash.KakashGrabPatch));
        } catch { }
        
        PatchType(typeof(GoblinPassLogic));
        PatchType(typeof(GoblinStruggleSpawnPatch)); // HARDMODE: Spawn 2 goblins when escaping from START animation
        PatchType(typeof(BigoniBrotherPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.MafiaBossCustom.MafiaBossCustomStartPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.MafiaBossCustom.MafiaBossCustomPassLogic));
        PatchType(typeof(NoREroMod.Patches.Enemy.MafiaBossCustom.MafiaBossCustomEROPatches));
        PatchType(typeof(NoREroMod.Patches.Enemy.MafiaBossCustom.MafiaBossCustomGrabPatch));
        PatchType(typeof(DoreiPassLogic));
        PatchType(typeof(MutudePassLogic));
        PatchType(typeof(NoREroMod.Systems.GrabSystem.Patches.RangedDamageFlagPatches));
        PatchType(typeof(NoREroMod.Systems.GrabSystem.Patches.MeleeAttackerContextPatches));
        PatchType(typeof(NoREroMod.Systems.Audio.AttackSoundPatch));
        PatchType(typeof(NoREroMod.Systems.Audio.DeathSoundPatch));
        PatchType(typeof(NoREroMod.Systems.GrabSystem.Patches.GrabViaAttackPatch));
        NoREroMod.Patches.Enemy.NoREroModEliteGrabDisablerPatch.Apply(harmony);
        PatchType(typeof(NoREroMod.Patches.Enemy.WolfModCustom.WolfSkeletonDataAssetPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.DoreiModCustom.DoreiSkeletonDataAssetPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.DoreiModCustom.DoreiSpectatorIdlePatch));
        PatchType(typeof(TimeScaleResetOnEscapePatch));
        PatchType(typeof(StruggleInvulnPatch));
        PatchType(typeof(GuardParryMindBrokenPatch));
        PatchType(typeof(PlayerConQTE3RestartPatch));
        PatchType(typeof(PlayerConQTE3GiveUpPatch));
        PatchType(typeof(NoREroMod.Patches.Player.PlayerConUpdateDispatcher));
        PatchType(typeof(BadstatusUiPatch));
        PatchType(typeof(MindBrokenSystem));
        PatchType(typeof(MindBrokenUIPatch));
        PatchType(typeof(NoREroMod.Patches.UI.MindBroken.CorruptionCaptionsSystem));
        PatchType(typeof(NoREroMod.Patches.UI.MindBroken.MindBrokenRecoverySystem));
        PatchType(typeof(NoREroMod.Patches.UI.MindBroken.MindBrokenVisualEffectsSystem));
        PatchType(typeof(NoREroMod.Patches.UI.MindBroken.MutudeMindbrokenControl));
        PatchType(typeof(NoREroMod.Patches.UI.MindBroken.InquisitionWhiteMindbrokenControl));
        PatchType(typeof(NoREroMod.Patches.UI.MindBroken.CrowInquisitionMindbrokenControl));
        PatchType(typeof(NoREroMod.Patches.UI.MindBroken.PilgrimMindbrokenControl));
        // H_scenesAllEnemiesCorruption is invoked from PlayerConUpdateDispatcher
        PatchType(typeof(NoREroMod.Patches.UI.MindBroken.EnemyKillRecoveryPatch)); // Legacy patch for specific enemies
        PatchType(typeof(NoREroMod.Patches.UI.MindBroken.MindBrokenUniversalKillRecoveryPatch)); // Universal patch for ALL enemies
        PatchType(typeof(NoREroMod.Systems.Dialogue.GrabThreatIdlePatch)); // Animation-based: threat on IDLE transition
        PatchType(typeof(NoREroMod.Systems.GrabSystem.GrabChanceUISystem));
        PatchType(typeof(NoREroMod.Systems.Rage.RageSystem));
        PatchType(typeof(NoREroMod.Systems.Rage.RageUISystem));
        // Use only the universal kill tracker to avoid duplicate kill registration.
        PatchType(typeof(NoREroMod.Systems.Rage.Patches.RageUniversalKillTrackerPatch));
        PatchType(typeof(NoREroMod.Systems.Rage.Patches.RageResetOnGrabDownPatch));
        PatchType(typeof(NoREroMod.Systems.Rage.Patches.RageHitTrackerPatch));
        PatchType(typeof(NoREroMod.Systems.Rage.Patches.WitchFineGreatswordPatch));
        PatchType(typeof(NoREroMod.Systems.Rage.RageComboUISystemPatches));
        PatchType(typeof(NoREroMod.Systems.Effects.HSceneBlackBackgroundTriggerPatch));
        PatchType(typeof(NoREroMod.Patches.Effects.PregnancyClipTrigger));
        PatchType(typeof(NoREroMod.Systems.PlayerRespawn.VengeanceRespawnEffectPatch));
        PatchType(typeof(NoREroMod.Systems.CombatAi.Patches.EnemyDateDistanceFunPatch)); // Combat AI: react when player close (HellGateJson/CombatAi)
        PatchType(typeof(NoREroMod.Systems.CombatAi.Patches.EnemyDateOndamageSendPatch)); // Combat AI: react to combo — boost dodge chance (HellGateJson/CombatAi)
        ApplyDoreiCombatAiPatch(harmony); // Combat AI: Dorei — no flee, prefer melee (CombatAi.json: DoreiEnable, DoreiDisableFlee)
        NoREroMod.Systems.Audio.AttackSoundSystem.Initialize(this);
        
        NoREroMod.Systems.Rage.RageSystem.Initialize();
        try {
            var patchType = typeof(NoREroMod.Systems.Rage.Patches.RageInputPatch);
            var criticalPostfix = AccessTools.Method(patchType, "Critical_Postfix");
            var magicDamagePrefix = AccessTools.Method(patchType, "MagicDamage_Prefix");
            var dpsMagicDamagePrefix = AccessTools.Method(patchType, "DPSMagicDamage_Prefix");
            var getinputPostfix = AccessTools.Method(patchType, "Getinput_Postfix");
            var gAmngUpdatePostfix = AccessTools.Method(patchType, "GAmngUpdate_Postfix");
            var criticalMethod = AccessTools.Method(typeof(PlayerStatus), "Critical");
            var magicDamageMethod = AccessTools.Method(typeof(EnemyDate), "MagicDamage");
            var dpsMagicDamageMethod = AccessTools.Method(typeof(EnemyDate), "DPSMagicDamage");
            var getinputMethod = AccessTools.Method(typeof(playercon), "Getinput");
            var gAmngUpdateMethod = AccessTools.Method(typeof(GAmng), "Update");
            if (criticalMethod != null && criticalPostfix != null)
            {
                harmony.Patch(criticalMethod, postfix: new HarmonyMethod(criticalPostfix));
            }
            if (magicDamageMethod != null && magicDamagePrefix != null)
            {
                harmony.Patch(magicDamageMethod, prefix: new HarmonyMethod(magicDamagePrefix));
            }
            if (dpsMagicDamageMethod != null && dpsMagicDamagePrefix != null)
            {
                harmony.Patch(dpsMagicDamageMethod, prefix: new HarmonyMethod(dpsMagicDamagePrefix));
            }
            if (getinputMethod != null && getinputPostfix != null)
            {
                harmony.Patch(getinputMethod, postfix: new HarmonyMethod(getinputPostfix));
            }

            if (gAmngUpdateMethod != null && gAmngUpdatePostfix != null)
            {
                harmony.Patch(gAmngUpdateMethod, postfix: new HarmonyMethod(gAmngUpdatePostfix));
            }
        } catch { }
        PatchType(typeof(NoREroMod.Systems.Camera.HSceneCameraDirectPanPatch));
        PatchType(typeof(NoREroMod.Systems.Camera.HSceneCameraPreventResetPatch));
        PatchType(typeof(NoREroMod.Systems.Camera.HSceneCameraCenterPreventPatch));
        PatchType(typeof(NoREroMod.Systems.Camera.HSceneCameraMoveOverridePatch));
        PatchType(typeof(NoREroMod.Systems.Camera.HSceneCameraGetTargetsMidPointPatch));
        PatchType(typeof(NoREroMod.Systems.Camera.HSceneCameraSmoothingDisablePatch));
        PatchType(typeof(NoREroMod.Systems.Camera.HSceneCameraSmoothApproachPatch));
        PatchType(typeof(NoREroMod.Systems.Camera.HSceneCameraArrowKeyBlockPatch1));
        PatchType(typeof(NoREroMod.Systems.Camera.HSceneCameraArrowKeyBlockPatch2));
        PatchType(typeof(NoREroMod.Systems.Camera.HSceneCameraZoomControlPatch));
        PatchType(typeof(NoREroMod.Systems.Camera.HSceneCameraResetPatch));
        PatchType(typeof(NoREroMod.Systems.Camera.CombatCameraPresetSystem));
        
        // Critical optimization: patches for camera_GetComponent() in EnemyDate/Trapdata/Slavehelp
        // Eliminates 2x FindWithTag("MainCamera") per grab (~5-10ms -> 0ms)
        PatchType(typeof(NoREroMod.Patches.Performance.CameraGetComponentPatch));
        PatchType(typeof(NoREroMod.Patches.Performance.TrapdataCameraGetComponentPatch));
        PatchType(typeof(NoREroMod.Patches.Performance.SlavehelpCameraGetComponentPatch));
        
        // Critical optimization: patch for EroMafiamuscle.Start()
        // Eliminates FindWithTag("Player") per Start/OnEnable (~3-5ms -> 0ms)
        PatchType(typeof(NoREroMod.Patches.Performance.EroMafiamuscleStartPatch));
        if (enableMindBroken.Value)
            MindBrokenUIPatch.InitializeFromPlugin();
        if (enableRageMode?.Value ?? false) {
            NoREroMod.Systems.Rage.RageUISystem.InitializeFromPlugin();
            NoREroMod.Systems.Rage.RageInputHandler.EnsureCreated();
        }
    }

    private void PatchType(Type type)
    {
        try { harmony.PatchAll(type); } catch { }
    }

    private void ApplyDoreiCombatAiPatch(Harmony h)
    {
        try
        {
            NoREroMod.Systems.CombatAi.Patches.SinnerslaveCrossbowCombatAiPatch.Apply(h);
        }
        catch (Exception ex)
        {
            Log?.LogWarning($"[CombatAi] Dorei patches failed: {ex.Message}. Dorei will use vanilla AI.");
        }
    }

    private void SetUpConfigs() {
        enemyHPMultiMax = Config.Bind(
            "Enemies",
            "HPMultiplierMax",
            0.8f,
            "Enemies have their HP multiplied by this value (max)"
        );
        enemyHPMultiMin = Config.Bind(
            "Enemies",
            "HPMultiplierMin",
            1.1f,
            "Enemies have their HP multiplied by this value (min)"
        );
        enemySpeedMulti = Config.Bind(
            "Enemies",
            "SpeedMultiplier",
            1.1f,
            "Enemies have their movement and animation speed multiplied by this value"
        );
        enemyPoiseMulti = Config.Bind(
            "Enemies",
            "PoiseMultiplier",
            1.5f,
            "Enemies have their poise value (damage they can take before flinching) multiplied by this value"
        );
        enemyEXPMulti = Config.Bind( // v0.11.5
            "Enemies",
            "EXPMultiplier",
            1f,
            "Enemies have their EXP multiplied by this value"
        );

        bossHPMulti = Config.Bind( // v0.11.5
            "Bosses",
            "HPMultiplier",
            1f,
            "Bosses have their HP multiplied by this value"
        );
        bossEXPMulti = Config.Bind( // v0.11.5
            "Bosses",
            "EXPMultiplier",
            1f,
            "Bosses have their EXP multiplied by this value"
        );

        pleasureAfterOrgasm = Config.Bind(
            "PleasureStatus",
            "PleasurePercentAfterOrgasm",
            0.75f,
            "After an orgasm cause by Pleasure Paralysis, Pleasure Paralysis will be set back to this percentage (0-1)"
        );
        pleasureEnemyAttackMax = Config.Bind(
            "PleasureStatus",
            "EnemyAttackMultiplierMax",
            2.5f,
            "Player takes this much more damage when at max pleasure"
        );
        pleasureEnemyAttackMin = Config.Bind(
            "PleasureStatus",
            "EnemyAttackMultiplierMin",
            1f,
            "Player takes this much more damage when at zero pleasure"
        );
        pleasurePlayerAttackMax = Config.Bind(
            "PleasureStatus",
            "PlayerAttackMultiplierMax",
            0.3f,
            "Player deals this much more damage when at max pleasure"
        );
        pleasurePlayerAttackMin = Config.Bind(
            "PleasureStatus",
            "PlayerAttackMultiplierMin",
            1f,
            "Player deals this much more damage when at zero pleasure"
        );
        pleasureAttackSpeedMax = Config.Bind(
            "PleasureStatus",
            "PlayerAttackSpeedMultiplierMax",
            0.7f,
            "Player attacks this much faster when at max pleasure"
        );
        pleasureAttackSpeedMin = Config.Bind(
            "PleasureStatus",
            "PlayerAttackSpeedMultiplierMin",
            1.3f,
            "Player attacks this much faster when at zero pleasure"
        );
        pleasureGainOnEro = Config.Bind(
            "PleasureStatus",
            "GainPerSecDuringEro",
            1f,
            "Amount pleasure bar fills per sec during ero (0-100)"
        );
        pleasureGainOnHit = Config.Bind(
            "PleasureStatus",
            "GainWhenHit",
            0f,
            "Amount pleasure bar fills when hit by an attack (0-100)"
        );
        pleasureLossOnHit = Config.Bind( // v0.11.3 Edited
            "PleasureStatus",
            "LossWhenHit",
            5f,
            "Amount pleasure bar reduces when player lands an attack (0-100)"
        );
        pleasureGainOnBlock = Config.Bind(
            "PleasureStatus",
            "GainWhenBlock",
            0f,
            "Amount pleasure bar fills when hit by chip damage from block (0-100)"
        );
        pleasureGainOnDown = Config.Bind(
            "PleasureStatus",
            "GainWhenDowned",
            5f,
            "Amount pleasure bar fills when downed by an attack (0-100)"
        );
        enablePregnancy = Config.Bind(
            "PleasureStatus",
            "EnablePregnancy",
            true,
            "Enables or disables additional pregnancy content such as multiple births and birthing based on sperm type (base game preg content will always be enabled)"
        );
        enableAnyPregnancy = Config.Bind( // v0.12.0
            "PleasureStatus",
            "EnableAnyPregnancy",
            true,
            "Allows aradia to give birth to any non-boss enemy (Aradia will give birth to a green slime everytime if disabled)"
        );
        pregnancyChance = Config.Bind(
            "PleasureStatus",
            "PregnancyChance",
            0.80f,
            "Chance to get pregnant after a creampie (0-1)"
        );
        extraBirthChance = Config.Bind(
            "PleasureStatus",
            "ExtraBirthChance",
            0.10f,
            "Chance to birth again after giving birth (0-1)"
        );
        disablePleasureParalysis = Config.Bind(
            "PleasureStatus",
            "DisableParalysis",
            false,
            "Set to true to disable the vanilla Pleasure Paralysis effect (flinch/stun effect that occurs randominly when at max pleasure)"
        );
        orgasmFlashStrength = Config.Bind( // v0.11.3 Edited
            "PleasureStatus",
            "OrgasmFlashStrength",
            0.25f,
            "Intensity of white flash of pleasure when Aradia experiences orgasm (0 = disabled, 1 = full intensity, 0.25 = default)"
        );

        hpLosePerSec = Config.Bind(
            "Ero",
            "HPLosePerSec",
            0f,
            "Amount HP lose per sec during ero"
        );
        hpLoseOnCreampie = Config.Bind(
            "Ero",
            "HPLosePerCreampie",
            5f,
            "Amount HP lose per creampie or other orgasm (most enemies creampie multiple times per animation)"
        );
        enableDelevel = Config.Bind(
            "Ero",
            "EnableDeleveling",
            true,
            "Enables or disables going down a level if exp would drain below zero"
        );
        expLosePerSec = Config.Bind(
            "Ero",
            "EXPLosePerSec",
            0.01f,
            "Percentage of exp to next level to lose per sec during ero (0-1)"
        );
        expLoseOnCreampie = Config.Bind(
            "Ero",
            "EXPLosePerCreampie",
            0.15f,
            "Percentage of exp to next level to lose per creampie or other orgasm (0-1) (most enemies creampie multiple times per animation)"
        );
        animationExpLoseMulti = Config.Bind(
            "Ero",
            "EXPLoseOnAnimationEventMultiplier",
            1f,
            "Exp lose caused by certain ero animations will be multiplied by this value"
        );
        expDelevelRefundPercent = Config.Bind(
            "Ero",
            "DelevelEXPRefundPercentage",
            1f,
            "Percentage of exp to refund back to the exp pool due to deleveling (0-1)"
        );
        pleasureSPRegenMax = Config.Bind(
            "Ero",
            "SPRegenMax",
            -30f,
            "Number of secs to go from 0% to 100% SP when downed at max pleasure"
        );
        pleasureSPRegenMin = Config.Bind(
            "Ero",
            "SPRegenMin",
            -60f,
            "Number of secs to go from 0% to 100% SP when downed at zero pleasure"
        );
        spLosePercentOnEroEvent = Config.Bind(
            "Ero",
            "SPLoseOnEroEvent",
            1f,
            "Current SP will be reduced by this percentage after penetration, player orgasm, or creampies (0-1)"
        );
        spPercentGainOnStruggleDown = Config.Bind(
            "Ero",
            "SPGainOnStruggleDowned",
            0.025f,
            "Percentage of max SP gained back on struggle while downed (downed but not yet in ero animation) (0-1)"
        );
        spPercentGainOnStruggleEro = Config.Bind(
            "Ero",
            "SPGainOnStruggleEro",
            0.025f,
            "Percentage of max SP gained back on struggle (during ero animation) (0-1)"
        );
        spPercentLoseOnBadStruggleEro = Config.Bind(
            "Ero",
            "SPLoseOnBadStruggleEro",
            0.12f,
            "Percentage of max SP lose when struggling outside of the allowed time (during ero animation) (0-1)"
        );
        animationHPDamageMulti = Config.Bind(
            "Ero",
            "AnimationHPDamageMultiplier",
            1f,
            "HP damage caused by certain ero animations will be multiplied by this value"
        );
        animationPleasureDamageMulti = Config.Bind(
            "Ero",
            "AnimationPleasureBuildupMultiplier",
            1f,
            "Pleasure buildup caused by certain ero animations will be multiplied by this value"
        );
        easyStruggleCount = Config.Bind( // v0.11.3 Edited
            "Ero",
            "easyStruggleCount",
            4f,
            "Enables easier struggles for a set number of struggles"
        );
        fatalityDifficulty = Config.Bind( // v0.11.5 Rebalanced
            "Ero",
            "fatalityDifficulty",
            0.4f,
            "How difficult it is to struggle out of a fatal animation (0-1)"
        );
        fatalityEasyStruggles = Config.Bind( // v0.11.5 Rebalanced
            "Ero",
            "fatalityEasyStruggles",
            false,
            "Enable easy struggles to work on fatality animations"
        );
        bossStruggleFatigue = Config.Bind( // v0.11.5 Rebalanced
            "Ero",
            "bossStruggleFatigue",
            true,
            "Enable struggling to get harder per escape during boss fights"
        );
        bossEasyStruggles = Config.Bind( // v0.11.5 Rebalanced
            "Ero",
            "bossEasyStruggles",
            false,
            "Enable easy struggles to work during boss fights"
        );
        enemyHealthEffectiveness = Config.Bind( // v0.11.5 Rebalanced
            "Ero",
            "enemyHealthEffectiveness",
            0.5f,
            "How much non-boss enemy max Hp effects struggle difficulty (0-1)"
        );
        playerHealthEffectiveness = Config.Bind( // v0.11.5 Rebalanced
            "Ero",
            "playerHealthEffectiveness",
            0.5f,
            "How strongly health effects struggle difficulty (0-1) (0=Disabled)"
        );
        spFactorEffectiveness = Config.Bind( // v0.12.0
            "Ero",
            "SpFactorEffectiveness",
            0.5f,
            "How strongly Max Sp eases struggle difficulty (0-1) (0=Disabled)"
        );

        playerMpEffectiveness = Config.Bind( // v0.11.5 Rebalanced
            "Ero",
            "playerMpEffectiveness",
            0f,
            "How strongly mp effects struggle difficulty (0-1) (0=Disabled)"
        );
        playerPleasureEffectiveness = Config.Bind( // v0.11.5 Rebalanced
            "Ero",
            "playerPleasureEffectiveness",
            1.5f,
            "How strongly pleasure effects struggle difficulty (0-1) (0=Disabled)"
        );
        struggleHpDifficultyPercent = Config.Bind(
            "StruggleDifficulty",
            "HpDifficultyPercent",
            100f,
            "Linear multiplier for HP deficit during struggles. Use 0-100 for percent scaling or 0-10 for short scale."
        );
        strugglePleasureDifficultyPercent = Config.Bind(
            "StruggleDifficulty",
            "PleasureDifficultyPercent",
            100f,
            "Linear multiplier for Pleasure contribution during struggles. Use 0-100 for percent scaling or 0-10 for short scale."
        );
        enableCriticalStruggle = Config.Bind(
            "Ero",
            "enableCriticalStruggle",
            false,
            "enables a certain chance to double your sp gain each time you struggle, but you could also lose that amount of progress (chances are based on your Aradia's Luck) Let's go gambling!"
        );
        allowStrugglePotion = Config.Bind(
            "Ero",
            "allowPotionEasyEscape",
            false,
            "Allows use of a potion to escape any struggle instantly"
        );
        enableImpossibleStruggles = Config.Bind(
            "Ero",
            "enableImpossibleStruggles",
            true,
            "Enable to make some struggles impossible based on the animation (When disabled, struggles will simply be harder instead of impossible)"
        );

        mpGainPerHit = Config.Bind(
            "Combat",
            "MPGainPerHit",
            3f,
            "Base amount of MP gained per attack with a INT scaling weapon"
        );
        spCostPerGuard = Config.Bind(
            "Combat",
            "SPGuardModifier",
            0.5f,
            "SP damage on guard is equal to the HP damage taken after guarding an attack multiplied by this value"
        );
        spCostPerDash = Config.Bind(
            "Combat",
            "DashSPCost",
            40f,
            "SP cost to dash/evade (base game = 20)"
        );
        spRegenIdle = Config.Bind(
            "Combat",
            "SPRegenWhenIdle",
            3f,
            "Number of secs to go from 0% to 100% SP when idle (base game = 2)"
        );
        spRegenGuard = Config.Bind(
            "Combat",
            "SPRegenWhenGuarding",
            10f,
            "Number of secs to go from 0% to 100% SP when guarding (base game = 7.5)"
        );
        hiddenHPBars = Config.Bind(
            "Combat",
            "HiddenEnemyHPBars",
            true,
            "Hides HP bars for non-boss enemies"
        );

        // GrabSystem NG
        enableGrabViaAttack = Config.Bind("GrabSystemNG", "EnableGrabViaAttack", true, "Enable grab on attack hit (melee only, 0% from ranged)");
        disableOriginalEliteGrab = Config.Bind("GrabSystemNG", "DisableOriginalEliteGrab", true, "Disable collision-based Elite Grab from NoREroMod");
        grabViaAttackEliteOnly = Config.Bind("GrabSystemNG", "GrabViaAttackEliteOnly", false, "Grab only from Elite (red) enemies. false = all enemies can grab");
        grabChanceMelee = Config.Bind("GrabSystemNG", "GrabChanceMelee", 0.10f, "Base chance of grab from normal melee attack when NOT blocking (0.10 = 10%). Affected by MindBroken (+), low HP (+), Pleasure (+) and Rage (-).");
        grabChancePowerAttack = Config.Bind("GrabSystemNG", "GrabChancePowerAttack", 0.15f, "Base chance of grab from knockdown/power attack when NOT blocking (0.15 = 15%). Affected by MindBroken (+), low HP (+), Pleasure (+) and Rage (-).");
        grabChanceThroughBlock = Config.Bind("GrabSystemNG", "GrabChanceThroughBlock", 0.05f, "Chance normal melee grabs through block (0.05 = 5%). Affected by MindBroken (+), low HP (+), Pleasure (+) and Rage (-).");
        grabChancePowerThroughBlock = Config.Bind("GrabSystemNG", "GrabChancePowerThroughBlock", 0.10f, "Chance knockdown attack grabs through block (0.10 = 10%). Affected by MindBroken (+), low HP (+), Pleasure (+) and Rage (-).");
        grabChanceMindBrokenBonusPer10Percent = Config.Bind("GrabSystemNG", "GrabChanceMindBrokenBonusPer10Percent", 0.02f, "Extra grab chance per 10% MindBroken in grab logic (0.02 = +2% per 10%). UI can use a different value.");
        grabChanceRageReductionPerPercent = Config.Bind("GrabSystemNG", "GrabChanceRageReductionPerPercent", 0.005f, "Grab chance reduction per 1% Rage (0.005 = 0.5% per 1% Rage). At 100% Rage grab chance is halved.");
        grabChancePleasureBonusMax = Config.Bind("GrabSystemNG", "GrabChancePleasureBonusMax", 0.20f, "Maximum additional grab chance from Pleasure gauge (BadstatusVal[0]). 0.20 = +20% at 100 pleasure, scaled linearly.");
        grabViaAttackSlowmo = Config.Bind("GrabSystemNG", "GrabViaAttackSlowmo", true, "Slow down time when grab via attack triggers (runs immediately, HScene zoom has no slowmo)");
        grabViaAttackSlowmoTimeScale = Config.Bind("GrabSystemNG", "GrabViaAttackSlowmoTimeScale", 0.3f, "Time scale during grab (0.3 = 30% speed, 2 sec)");
        grabViaAttackSlowmoDuration = Config.Bind("GrabSystemNG", "GrabViaAttackSlowmoDuration", 2f, "Duration of grab slowmo in real seconds");

        enableAttackSounds = Config.Bind(
            "AttackSounds",
            "Enable",
            true,
            "Enable custom attack sounds from sources/HellGate_sources/AttackSounds"
        );
        attackSoundsVolume = Config.Bind(
            "AttackSounds",
            "Volume",
            0.85f,
            "Global volume for custom attack sounds (0.0 - 1.0)"
        );
        enableThreatSounds = Config.Bind(
            "AttackSounds",
            "EnableThreatSounds",
            true,
            "Play threat sounds from AttackSounds/Human/Threats<LANG> (e.g. ThreatsEN) when human enemies are 4-6 units away (same flow as dialogue threats)"
        );

        // GrabThreats section: master switch + text/sound toggles
        enableGrabThreats = Config.Bind(
            "GrabThreats",
            "Enable",
            true,
            "Master switch: enable grab threat system (text phrases and/or sounds when enemies are about to grab). When false, disables both text and threat sounds from this system."
        );
        enableGrabThreatsText = Config.Bind(
            "GrabThreats",
            "EnableThreatText",
            true,
            "Show threat text phrases above enemies. Can be toggled separately from threat sounds (e.g. text only, sound only, or both)"
        );
        threatSoundsVolume = Config.Bind(
            "AttackSounds",
            "ThreatSoundsVolume",
            0.9f,
            "Volume for threat sounds (0.0 - 1.0)"
        );
        threatSoundsGlobalCooldown = Config.Bind(
            "AttackSounds",
            "ThreatSoundsGlobalCooldown",
            5f,
            "Minimum seconds between ANY threat sounds. Should match threatDisplayDuration for text/sound sync."
        );
        threatSoundsPerEnemyCooldown = Config.Bind(
            "AttackSounds",
            "ThreatSoundsPerEnemyCooldown",
            10f,
            "Seconds before the same enemy can play another threat sound."
        );
        enableDeathSounds = Config.Bind(
            "AttackSounds",
            "EnableDeathSounds",
            true,
            "Play death sounds from AttackSounds/Human/Death when human enemies die (DEATH animation)"
        );
        deathSoundsVolume = Config.Bind(
            "AttackSounds",
            "DeathSoundsVolume",
            1f,
            "Volume for death sounds (0.0 - 1.0)"
        );
        attackSoundsGlobalInterval = Config.Bind(
            "AttackSounds",
            "AttackSoundsGlobalInterval",
            0.12f,
            "Minimum seconds between attack sounds globally (reduces spam when fighting many enemies)."
        );
        attackSoundsPerAttackerInterval = Config.Bind(
            "AttackSounds",
            "AttackSoundsPerAttackerInterval",
            0.2f,
            "Minimum seconds before same attacker can play another attack sound."
        );

        enableFoV = Config.Bind(
            "FieldOfView",
            "EnableFieldOfView",
            true,
            "When enabled, enemies behind or too far away from the player will be hidden (recommend also enabling HiddenEnemyHPBars)"
        );
        frontViewDistance = Config.Bind(
            "FieldOfView",
            "FrontViewDistance",
            9f,
            "Vision radius for enemies in front of the player (10 ~= half screen distance)"
        );
        backViewDistance = Config.Bind(
            "FieldOfView",
            "BackViewDistance",
            2.5f,
            "Vision radius for enemies behind the player (2 ~= touching distance)"
        );

        isHardcoreMode = Config.Bind(
            "Hardcore",
            "IsHardcoreMode",
            false,
            "CAUTION!!! Deletes ALL save files upon death or bad end scene"
        );

        trappedSavePoints = Config.Bind(
            "SavePoints",
            "TrappedSavePoints",
            false,
            "Using the respawn save point after leaving will result in a gameover scene"
        );
        shrinesRetoreVirginity = Config.Bind( // v0.11.5 Rebalanced
            "SavePoints",
            "ShrinesRetoreVirginity",
            false,
            "Activating a shrine will restore virginity"
        );

        // New handoff system configs
        enableEnemyHandoff = Config.Bind(
            "HandoffSystem",
            "EnableEnemyHandoff",
            true,
            "Enables enemy handoff system - enemies will pass around the player after completing animation cycles"
        );
        handoffCooldownTime = Config.Bind(
            "HandoffSystem",
            "HandoffCooldownTime",
            2f,
            "Time in seconds between handoffs to prevent spam"
        );
        enableHandoffVisualEffects = Config.Bind(
            "HandoffSystem",
            "EnableHandoffVisualEffects",
            true,
            "Shows visual effects during handoffs"
        );

        // Visual indicators configs
        disableStruggleCameraShake = Config.Bind(
            "VisualIndicators",
            "DisableStruggleCameraShake",
            true,
            "Disable camera shake during struggle (Hellachaz/NoREroMod original)"
        );
        enableStruggleVisualIndicators = Config.Bind(
            "VisualIndicators",
            "EnableStruggleVisualIndicators",
            true,
            "Shows visual indicators during struggle"
        );
        showDifficultyIndicator = Config.Bind(
            "VisualIndicators",
            "ShowDifficultyIndicator",
            true,
            "Shows difficulty indicator bar"
        );
        showProgressIndicator = Config.Bind(
            "VisualIndicators",
            "ShowProgressIndicator",
            true,
            "Shows struggle progress bar"
        );
        showCriticalChanceIndicator = Config.Bind(
            "VisualIndicators",
            "ShowCriticalChanceIndicator",
            true,
            "Shows critical chance indicator"
        );

        // Dialogue font settings
        dialogueFontSize = Config.Bind(
            "DialogueFonts",
            "FontSize",
            22f,
            "Font size for all dialogue systems (22 = standard size)"
        );
        enemyFontStyle = Config.Bind(
            "DialogueFonts",
            "EnemyFontStyle",
            1,  // Changed from 0 to 1 (Bold) for consistent bold text across all enemies
            "Font style for enemy comments (0 = Normal, 1 = Bold, 2 = Italic, 3 = BoldAndItalic)"
        );
        aradiaResponseFontStyle = Config.Bind(
            "DialogueFonts",
            "AradiaResponseFontStyle",
            0,
            "Font style for Aradia responses (0 = Normal, 1 = Bold, 2 = Italic, 3 = BoldAndItalic)"
        );
        aradiaThoughtFontStyle = Config.Bind(
            "DialogueFonts",
            "AradiaThoughtFontStyle",
            0,
            "Font style for Aradia thoughts (0 = Normal, 1 = Bold, 2 = Italic, 3 = BoldAndItalic)"
        );
        spectatorFontStyle = Config.Bind(
            "DialogueFonts",
            "SpectatorFontStyle",
            0,
            "Font style for spectator comments (0 = Normal, 1 = Bold, 2 = Italic, 3 = BoldAndItalic)"
        );
        threatFontStyle = Config.Bind(
            "DialogueFonts",
            "ThreatFontStyle",
            1,
            "Font style for grab threats (0 = Normal, 1 = Bold, 2 = Italic, 3 = BoldAndItalic)"
        );
        enemyColor = Config.Bind(
            "DialogueFonts",
            "EnemyColor",
            "1.0,1.0,1.0,1.0",
            "Text color for enemies (R, G, B, A - values 0-1)"
        );
        enemyOutlineColor = Config.Bind(
            "DialogueFonts",
            "EnemyOutlineColor",
            "0.0,0.0,0.0,1.0",
            "Outline color for enemies (R, G, B, A - values 0-1)"
        );
        aradiaResponseColor = Config.Bind(
            "DialogueFonts",
            "AradiaResponseColor",
            "0.8,0.4,1.0,1.0",
            "Text color for Aradia responses (R, G, B, A - values 0-1)"
        );
        aradiaResponseOutlineColor = Config.Bind(
            "DialogueFonts",
            "AradiaResponseOutlineColor",
            "1.0,1.0,1.0,1.0",
            "Outline color for Aradia responses (R, G, B, A - values 0-1)"
        );
        aradiaThoughtColor = Config.Bind(
            "DialogueFonts",
            "AradiaThoughtColor",
            "1.0,0.6,0.8,1.0",
            "Text color for Aradia thoughts (R, G, B, A - values 0-1)"
        );
        aradiaThoughtOutlineColor = Config.Bind(
            "DialogueFonts",
            "AradiaThoughtOutlineColor",
            "1.0,1.0,1.0,1.0",
            "Outline color for Aradia thoughts (R, G, B, A - values 0-1)"
        );
        spectatorColor = Config.Bind(
            "DialogueFonts",
            "SpectatorColor",
            "1.0,1.0,1.0,1.0",
            "Text color for spectators (R, G, B, A - values 0-1)"
        );
        spectatorOutlineColor = Config.Bind(
            "DialogueFonts",
            "SpectatorOutlineColor",
            "0.0,0.0,0.0,1.0",
            "Outline color for spectators (R, G, B, A - values 0-1)"
        );
        threatColor = Config.Bind(
            "DialogueFonts",
            "ThreatColor",
            "1.0,0.0,0.0,1.0",
            "Text color for threats (R, G, B, A - values 0-1). Default: red"
        );
        threatOutlineColor = Config.Bind(
            "DialogueFonts",
            "ThreatOutlineColor",
            "0.0,0.0,0.0,1.0",
            "Outline color for threats (R, G, B, A - values 0-1)"
        );
        
        // Enemy Pass Mechanic (Cloud Solution)
        enableEnemyPass = Config.Bind(
            "EnemyPass",
            "EnableEnemyPassMechanic",
            true,
            "Enable enemy pass mechanic - player will be passed between enemies after several animation cycles"
        );
        
        cyclesBeforePass = Config.Bind(
            "EnemyPass",
            "CyclesBeforePass",
            2,
            "Number of animation cycles before pushback (1-5)"
        );
        
        pushDistance = Config.Bind(
            "EnemyPass",
            "PushDistance",
            2.0f,
            "Pushback distance for player to the side (1.0-5.0)"
        );
        
        minCycleInterval = Config.Bind(
            "EnemyPass",
            "MinCycleInterval",
            2.0f,
            "Minimum interval between cycles in seconds (0.5-5.0)"
        );
        
        handoffDelay = Config.Bind(
            "EnemyPass",
            "HandoffDelay",
            3.0f,
            "Delay before player handoff in seconds (1.0-10.0). Higher = slower handoff."
        );

        wolfModAssetsPath = Config.Bind(
            "WolfMod",
            "AssetsPath",
            "",
            "Path to Wolf Mod Spine folder (relative to game root). Empty = use default: sources/HellGate_sources/Wolf Mod Spine. MUST contain Enemy/WolfE.png and ERO/Wolf.png!"
        );

        doreiFappingAssetsPath = Config.Bind(
            "DoreiMod",
            "FappingAssetsPath",
            "",
            "Path to DoreiFapping folder (relative to game root). Empty = use default: sources/HellGate_sources/DoreiFapping. Dorei plays fapping IDLE while waiting in H-scene."
        );

        doreiSpectatorScaleMultiplier = Config.Bind(
            "DoreiMod",
            "SpectatorScaleMultiplier",
            1f,
            "Scale multiplier for Dorei fapping spectator skeleton. 1.0 = same as original. If fapping looks larger, try 0.85-0.95."
        );

        enableDirtyTalkMessages = Config.Bind(
            "EnemyPass",
            "EnableDirtyTalkMessages",
            true,
            "Enable dirty talk during H-scenes"
        );
        
        enableHandoffMessages = Config.Bind(
            "EnemyPass",
            "EnableHandoffMessages",
            true,
            "Enable messages when player is passed between enemies"
        );
        
        // Mind Broken system
        enableMindBroken = Config.Bind(
            "MindBroken",
            "Enable",
            true,
            "Enable Mind Broken system (increases struggle difficulty and pleasure gain when player is passed between enemies)"
        );

        mindBrokenPercentPerPass = Config.Bind(
            "MindBroken",
            "PercentPerPass",
            0.01f,
            "Mind Broken percentage added per handoff (0.01 = 1%)"
        );

        mindBrokenStruggleBonusPerStep = Config.Bind(
            "MindBroken",
            "StruggleBonusPerStep",
            0.30f,
            "Additional struggle difficulty per Mind Broken step (0.30 = +30%)"
        );

        mindBrokenMaxPercent = Config.Bind(
            "MindBroken",
            "MaxPercent",
            1.0f,
            "Maximum Mind Broken value (1.0 = 100%)"
        );
        
        mindBrokenBadEndCountdownDuration = Config.Bind(
            "MindBroken",
            "BadEndCountdownDuration",
            180.0f,
            "Countdown duration in seconds before Bad End triggers at 100% MindBroken (default: 180.0 = 3 minutes)"
        );
        
        mindBrokenBadEndResetThreshold = Config.Bind(
            "MindBroken",
            "BadEndResetThreshold",
            0.9f,
            "MindBroken percentage threshold for countdown reset (default: 0.9 = 90%, timer resets if MB drops below this)"
        );

        mindBrokenHighRagePassiveEnable = Config.Bind(
            "MindBroken",
            "HighRagePassiveEnable",
            true,
            "While Rage bar is above HighRageThresholdPercent, apply passive MindBroken gain (encourages spending Rage)."
        );
        mindBrokenHighRageThresholdPercent = Config.Bind(
            "MindBroken",
            "HighRageThresholdPercent",
            60f,
            "Rage percent (0-103) above which passive MindBroken applies (e.g. 60 = Tier-2 gate and above)."
        );
        mindBrokenHighRagePassivePercentPerSecond = Config.Bind(
            "MindBroken",
            "HighRagePassivePercentPerSecond",
            0.1f,
            "MindBroken gain per second while Rage is above threshold (0.1 = +0.1%/sec)."
        );
        mindBrokenHighRagePassiveOnlyWhenRageInactive = Config.Bind(
            "MindBroken",
            "HighRagePassiveOnlyWhenRageInactive",
            true,
            "If true, passive gain applies only while Rage mode is OFF (avoids stacking with rage_active / overdrive MB)."
        );

        // HSceneBlackBackground MindBroken tick
        hsceneBlackBackgroundMindBrokenPerSecondPercent = Config.Bind(
            "HSceneBlackBackground",
            "MindBrokenPerSecondPercent",
            0.2f,
            "MindBroken growth while H-scene black background is active (0.2 = +0.2% per second)"
        );

        // Corruption Captions system
        enableCorruptionCaptions = Config.Bind(
            "CorruptionCaptions",
            "Enable",
            true,
            "Enable corruption caption system - red text messages when MindBroken increases"
        );

        corruptionCaptionCooldown = Config.Bind(
            "CorruptionCaptions",
            "CaptionCooldown",
            1.5f,
            "Cooldown between captions in seconds (1.5 = 1.5 sec)"
        );

        // MindBroken Recovery system
        enableMindBrokenRecovery = Config.Bind(
            "MindBrokenRecovery",
            "Enable",
            true,
            "Enable MindBroken recovery system - recover MindBroken by killing enemies"
        );

        recoveryPercentPerKill = Config.Bind(
            "MindBrokenRecovery",
            "PercentPerKill",
            0.01f,
            "Recovery percentage per normal enemy kill (0.01 = 1%)"
        );

        recoveryPercentPerBossKill = Config.Bind(
            "MindBrokenRecovery",
            "PercentPerBossKill",
            0.05f,
            "Recovery percentage per boss kill (0.05 = 5%)"
        );

        recoveryBossNames = Config.Bind(
            "MindBrokenRecovery",
            "BossNames",
            "bigonibrother",
            "Boss names separated by commas (e.g., bigonibrother,boss1,boss2)"
        );

        recoveryCaptionCooldown = Config.Bind(
            "MindBrokenRecovery",
            "CaptionCooldown",
            1.5f,
            "Cooldown between recovery captions in seconds (1.5 = 1.5 sec)"
        );

        // MindBroken Visual Effects configs
        mbFogAppearanceThreshold = Config.Bind(
            "MindBrokenVisualEffects",
            "FogAppearanceThreshold",
            0.15f,
            "MindBroken percentage threshold for fog to appear (0.15 = 15%) - later appearance for performance"
        );
        
        mbFogColorR = Config.Bind(
            "MindBrokenVisualEffects",
            "FogColorR",
            1.0f,
            "Fog color red component (0.0-1.0)"
        );
        
        mbFogColorG = Config.Bind(
            "MindBrokenVisualEffects",
            "FogColorG",
            0.7f,
            "Fog color green component (0.0-1.0)"
        );
        
        mbFogColorB = Config.Bind(
            "MindBrokenVisualEffects",
            "FogColorB",
            0.95f,
            "Fog color blue component (0.0-1.0)"
        );
        
        mbFogMaxAlpha = Config.Bind(
            "MindBrokenVisualEffects",
            "FogMaxAlpha",
            0.3f,
            "Maximum fog alpha intensity (0.0-1.0, 0.3 = 30% opacity) - reduced for performance"
        );
        
        mbFogPulseSpeed = Config.Bind(
            "MindBrokenVisualEffects",
            "FogPulseSpeed",
            1.0f,
            "Fog pulse animation speed (higher = faster pulse, 1.0 = gentle pulse)"
        );
        
        mbFogCenterRadiusMin = Config.Bind(
            "MindBrokenVisualEffects",
            "FogCenterRadiusMin",
            0.35f,
            "Legacy parameter - not used with horizontal bars effect"
        );

        mbFogCenterRadiusMax = Config.Bind(
            "MindBrokenVisualEffects",
            "FogCenterRadiusMax",
            0.20f,
            "Legacy parameter - not used with horizontal bars effect"
        );
        
        mbNegativeEffectDuration = Config.Bind(
            "MindBrokenVisualEffects",
            "NegativeEffectDuration",
            1.5f,
            "Negative effect duration in seconds when triggered - reduced for performance"
        );
        
        mbNegativeActivationThreshold = Config.Bind(
            "MindBrokenVisualEffects",
            "NegativeActivationThreshold",
            0.5f,
            "MindBroken percentage threshold for negative effect to start (0.5 = 50%)"
        );
        
        mbNegativeActivationStep = Config.Bind(
            "MindBrokenVisualEffects",
            "NegativeActivationStep",
            0.15f,
            "MindBroken percentage step for negative effect triggers (0.15 = every 15% after threshold) - less frequent"
        );
        
        mbDreamEffectSpeed = Config.Bind(
            "MindBrokenVisualEffects",
            "DreamEffectSpeed",
            3f,
            "Dream distortion effect animation speed (0-32, default: 3 = slow waves)"
        );
        
        mbDreamEffectDistortion = Config.Bind(
            "MindBrokenVisualEffects",
            "DreamEffectDistortion",
            4f,
            "Dream distortion effect intensity (0-100, default: 4 = subtle distortion)"
        );
        
        mbFlashStartThreshold = Config.Bind(
            "MindBrokenVisualEffects",
            "FlashStartThreshold",
            0.2f,
            "MindBroken percentage to start flash effect (0.2 = 20%, then every 10%)"
        );
        
        mbFlashDuration = Config.Bind(
            "MindBrokenVisualEffects",
            "FlashDuration",
            3f,
            "Flash effect total duration in seconds (default: 3)"
        );
        
        mbFlashPulseCycles = Config.Bind(
            "MindBrokenVisualEffects",
            "FlashPulseCycles",
            3,
            "Number of pulse cycles during flash (default: 3)"
        );
        
        mbFlashMinAlpha = Config.Bind(
            "MindBrokenVisualEffects",
            "FlashMinAlpha",
            0.08f,
            "Flash minimum transparency (0.0-1.0, default: 0.08 = very subtle)"
        );
        
        mbFlashMaxAlpha = Config.Bind(
            "MindBrokenVisualEffects",
            "FlashMaxAlpha",
            0.22f,
            "Flash maximum transparency (0.0-1.0, default: 0.22 = gentle)"
        );
        
        mbFlashColorR = Config.Bind(
            "MindBrokenVisualEffects",
            "FlashColorR",
            1.0f,
            "Flash color red component (0.0-1.0, default: 1.0)"
        );
        
        mbFlashColorG = Config.Bind(
            "MindBrokenVisualEffects",
            "FlashColorG",
            0.75f,
            "Flash color green component (0.0-1.0, default: 0.75 = soft pink)"
        );
        
        mbFlashColorB = Config.Bind(
            "MindBrokenVisualEffects",
            "FlashColorB",
            0.88f,
            "Flash color blue component (0.0-1.0, default: 0.88 = soft pink)"
        );
        
        mbFlashFadeOutTime = Config.Bind(
            "MindBrokenVisualEffects",
            "FlashFadeOutTime",
            0.8f,
            "Flash fade out duration in seconds (default: 0.8 = smooth end)"
        );
        
        mbDreamDuration = Config.Bind(
            "MindBrokenVisualEffects",
            "DreamDuration",
            5f,
            "Dream effect total duration in seconds at 100% MindBroken (default: 5)"
        );
        
        mbDreamFadeInTime = Config.Bind(
            "MindBrokenVisualEffects",
            "DreamFadeInTime",
            1.2f,
            "Dream effect fade in duration in seconds (default: 1.2 = smooth start)"
        );
        
        mbDreamFadeOutTime = Config.Bind(
            "MindBrokenVisualEffects",
            "DreamFadeOutTime",
            1.5f,
            "Dream effect fade out duration in seconds (default: 1.5 = very smooth end)"
        );

        // InquisitionWhite MindBroken configs
        inquisitionWhiteEnableWaveEffect = Config.Bind(
            "InquisitionWhiteMindBroken",
            "EnableWaveEffect",
            true,
            "Enable visual wave effect during InquisitionWhite ERO_START3 animation and at 100% MindBroken"
        );

        inquisitionWhiteMindBrokenPerSecond = Config.Bind(
            "InquisitionWhiteMindBroken",
            "MindBrokenPerSecond",
            3f,
            "MindBroken percentage added per second during syringe injection (ERO_START2) (default: 3 = 3%/sec)"
        );

        // CrowInquisition MindBroken configs
        crowInquisitionMindBrokenPerSecondIKI = Config.Bind(
            "CrowInquisitionMindBroken",
            "MindBrokenPerSecondIKI",
            6f,
            "MindBroken percentage added per second during IKI animation (time-stop orgasm sequence) (default: 6 = 6%/sec)"
        );

        crowInquisitionMindBrokenPerSecondIKI2 = Config.Bind(
            "CrowInquisitionMindBroken",
            "MindBrokenPerSecondIKI2",
            3f,
            "MindBroken percentage added per second during IKI2 animation (time-stop orgasm sequence) (default: 3 = 3%/sec)"
        );

        // Pilgrim MindBroken configs
        pilgrimMindBrokenPerSecondBell = Config.Bind(
            "PilgrimMindBroken",
            "MindBrokenPerSecondBell",
            2f,
            "MindBroken percentage added per second during bell-ringing hypnosis phases (START2, FERA1, EROSTART, 2ERO) (default: 2 = 2%/sec)"
        );

        // Mutude MindBroken tick
        mutudeMindBrokenPerSecondPercent = Config.Bind(
            "MutudeMindBroken",
            "MindBrokenPerSecondPercent",
            1f,
            "MindBroken growth while Mutude DRINK/ERO3/ERO4/ERO5 animations are active (1 = +1% per second)"
        );

        // Rage Mode configs
        enableRageMode = Config.Bind(
            "RageMode",
            "Enable",
            true,
            "Enable Rage Mode system (counter-mechanic to MindBroken)"
        );

        rageCritMultiplier = Config.Bind(
            "RageMode",
            "CritMultiplier",
            1.5f,
            "Critical damage multiplier during Rage (1.5 = 50% bonus, 2.0 = 100% bonus)"
        );

        // Base MindBroken gain while Rage is active
        rageBaseMindBrokenGainPerSecondPercent = Config.Bind(
            "RageMode",
            "MindBrokenBaseGainPerSecondPercent",
            0.5f,
            "Base MindBroken gain during active Rage (0.5 = +0.5% per second)"
        );

        rageHandsParticleMaxParticles = Config.Bind(
            "RageMode",
            "HandsParticleMaxParticles",
            15,
            "Maximum particles per hand for fire effects (lower = better performance)"
        );

        ragePerformanceMode = Config.Bind(
            "RageMode",
            "PerformanceMode",
            false,
            "Enable performance mode: reduces particles and effects for better FPS"
        );

        rageHandsGlowSizePx = Config.Bind(
            "RageMode",
            "HandsGlowSizePx",
            96f,
            "Size of the glow effect around hands during Rage (in pixels)"
        );

        rageGainPerKill = Config.Bind(
            "RageMode",
            "GainPerKill",
            3.0f,
            "Rage percent per normal enemy kill on death (3 = +3%). Bosses use GainPerBossKill."
        );

        rageGainPerBossKill = Config.Bind(
            "RageMode",
            "GainPerBossKill",
            12.0f,
            "Rage Energy percentage per boss kill (12.0 = 12%)"
        );

        ragePassiveTickAmount = Config.Bind(
            "RageMode",
            "PassiveTickAmount",
            0.3f,
            "Rage Energy percentage per passive tick (only if MB >70%, 0.3 = 0.3%)"
        );

        ragePassiveTickInterval = Config.Bind(
            "RageMode",
            "PassiveTickInterval",
            3.0f,
            "Passive tick interval in seconds (3.0 = 3 sec)"
        );

        rageActivationCost = Config.Bind(
            "RageMode",
            "ActivationCost",
            50.0f,
            "LEGACY: single-mode activation cost. Tiered system uses fixed per-tier costs (T1=30, T2=60, T3=100)."
        );

        rageActivationDuration = Config.Bind(
            "RageMode",
            "ActivationDuration",
            8.0f,
            "LEGACY: single-mode activation duration. Tiered system uses RageTier1/2/3Duration."
        );

        rageCooldownDuration = Config.Bind(
            "RageMode",
            "CooldownDuration",
            10.0f,
            "Cooldown duration after activation in seconds (10.0 = 10 sec)"
        );

        timeSlowMoTimeScale = Config.Bind(
            "RageMode",
            "TimeSlowMoTimeScale",
            0.4f,
            "Time slow-mo time scale (T key) (0.4 = 60% slowdown, 0.5 = 50%, 1.0 = no slowdown)"
        );

        timeSlowMoRageDrainPerSecond = Config.Bind(
            "RageMode",
            "TimeSlowMoRageDrainPerSecond",
            5.0f,
            "Rage Energy drain per second when using Time Slow-Mo (T) (5.0 = 5% per second)"
        );
        
        // Rage Mode - Advanced Settings
        rageMinActivationPercent = Config.Bind(
            "RageMode",
            "MinActivationPercent",
            50.0f,
            "LEGACY: previous single-threshold activation. Tiered system uses RageTier1/2/3 thresholds below."
        );
        
        rageCostDuringQTE = Config.Bind(
            "RageMode",
            "CostDuringQTE",
            50.0f,
            "LEGACY: QTE now uses tier-based activation costs. Kept for backward compatibility with old configs/log paths."
        );

        rageTier1Threshold = Config.Bind(
            "RageMode",
            "RageTier1Threshold",
            30.0f,
            "Tier1 threshold (outside H-scene only)."
        );

        rageTier2Threshold = Config.Bind(
            "RageMode",
            "RageTier2Threshold",
            60.0f,
            "Tier2 threshold (outside and inside H-scene; minimum for Rage-based H escape)."
        );

        rageTier3OverflowThreshold = Config.Bind(
            "RageMode",
            "RageTier3OverflowThreshold",
            103.0f,
            "Tier3 threshold using overflow (internal cap above 100; UI still shows max 100)."
        );

        rageTier1Duration = Config.Bind(
            "RageMode",
            "RageTier1Duration",
            5.0f,
            "Tier1 activation duration in seconds."
        );

        rageTier2Duration = Config.Bind(
            "RageMode",
            "RageTier2Duration",
            10.0f,
            "Tier2 activation duration in seconds."
        );

        rageTier3Duration = Config.Bind(
            "RageMode",
            "RageTier3Duration",
            15.0f,
            "Tier3 activation duration in seconds."
        );
        
        // REMOVED: Stats boost config - using critical hits now
        
        rageSPGainPercent = Config.Bind(
            "QTE",
            "RageAttackClickSPGainPercent",
            0.5f,
            "SP gain per attack click during Rage as percentage of max SP (0.5 = 50%). In QTE section for consistency."
        );
        
        rageEnemyPushbackForceMultiplier = Config.Bind(
            "RageMode",
            "EnemyPushbackForceMultiplier",
            100.0f,
            "Force multiplier for enemy pushback on Rage activation"
        );
        
        ragePushbackMinDistance = Config.Bind(
            "RageMode",
            "PushbackMinDistance",
            4.0f,
            "Minimum push distance (units, 1 unit ≈ 100px). 4.0 = 400px"
        );
        
        ragePushbackMaxDistance = Config.Bind(
            "RageMode",
            "PushbackMaxDistance",
            6.0f,
            "Maximum push distance (units). 6.0 = 600px"
        );
        
        ragePushbackVerticalBoost = Config.Bind(
            "RageMode",
            "PushbackVerticalBoost",
            6.0f,
            "Vertical force added to pushback (enemies fly up slightly). 0 = horizontal only"
        );
        
        rageActivationCameraShake = Config.Bind(
            "RageMode",
            "ActivationCameraShake",
            true,
            "Camera shake effect when Rage activates"
        );
        
        ragePushbackApplyFalter = Config.Bind(
            "RageMode",
            "PushbackApplyFalter",
            true,
            "Apply stagger (Falter) to all enemies on pushback"
        );
        
        ragePushbackFalterDuration = Config.Bind(
            "RageMode",
            "PushbackFalterDuration",
            1.5f,
            "Stagger duration in seconds for pushed enemies"
        );
        
        rageGrabDrainMin = Config.Bind(
            "RageMode",
            "GrabDrainMin",
            1.0f,
            "Rage drain per second when grabbed at 0% MindBroken (default: 1.0 = 1%/sec)"
        );
        
        rageGrabDrainMax = Config.Bind(
            "RageMode",
            "GrabDrainMax",
            10.0f,
            "Rage drain per second when grabbed at 100% MindBroken (default: 10.0 = 10%/sec, linear interpolation)"
        );
        
        rageSlowMoDrainMultiplier = Config.Bind(
            "RageMode",
            "SlowMoDrainMultiplier",
            2.0f,
            "Multiplier for SlowMo rage drain (default: 2.0 = base drain * 2.0)"
        );
        
        rageSlowMoMBGainMultiplier = Config.Bind(
            "RageMode",
            "SlowMoMBGainMultiplier",
            2.0f,
            "Multiplier for SlowMo MindBroken gain (default: 2.0 = base gain * 2.0)"
        );
        
        rageUIPositionX = Config.Bind(
            "RageMode",
            "UIPositionX",
            360.0f,
            "Rage UI X position from left edge (default: 360.0 = 360px)"
        );
        
        rageUIPositionY = Config.Bind(
            "RageMode",
            "UIPositionY",
            -25.0f,
            "Rage UI Y position from top edge (default: -25.0 = 25px down from top, negative = down from top)"
        );
        
        rageBloodEffectDuration = Config.Bind(
            "RageMode",
            "BloodEffectDuration",
            0.5f,
            "Duration of Vision_Blood_Fast effect on activation in seconds (0.5 = 0.5 sec)"
        );
        
        ragePushbackMaxEnemies = Config.Bind(
            "RageMode",
            "PushbackMaxEnemies",
            50,
            "Max enemies to push on activation (prevents lag)"
        );
        
        rageOutburstFuryDrainPerSecond = Config.Bind(
            "RageMode",
            "OutburstFuryDrainPerSecond",
            10.0f,
            "LEGACY: old auto-Outburst drain value. Tiered mode uses timer windows and does not rely on legacy auto-Outburst."
        );
        
        rageKillTimeoutSeconds = Config.Bind(
            "RageMode",
            "KillTimeoutSeconds",
            5.0f,
            "Seconds without kill to refresh overdrive timeout"
        );
        
        rageComboTimeout = Config.Bind(
            "RageMode",
            "ComboTimeout",
            2.0f,
            "Seconds without attack to reset combo (2.0 = 2 sec)"
        );

        rageComboBaseGain = Config.Bind(
            "RageMode",
            "ComboBaseGain",
            3.0f,
            "Base rage per hit before ComboGainMultiplier and global hit scale (1/3). With ComboGainMultiplier=0.5 and global 1/3 => +0.5% rage per hit. Every 10th hit adds flat +1%, +2%, +3%... on the bar."
        );

        rageComboGainMultiplier = Config.Bind(
            "RageMode",
            "ComboGainMultiplier",
            0.5f,
            "Multiplier for base per-hit combo rage only. Does not affect x10 flat milestones (+1/+2/...), kills, parry, block, vengeance."
        );
        
        rageResetHCPenaltyGrab = Config.Bind(
            "RageMode",
            "ResetHCPenaltyGrab",
            0.05f,
            "MindBroken penalty when Rage is interrupted by grab / H activation (0.05 = +5% MB). Applies to normal Rage and Outburst Fury."
        );
        
        rageResetHCPenaltyKnockdown = Config.Bind(
            "RageMode",
            "ResetHCPenaltyKnockdown",
            0.02f,
            "MindBroken penalty when Rage is interrupted by knockdown only (0.02 = +2% MB). Grab uses ResetHCPenaltyGrab."
        );
        
        rageKeyPressCooldown = Config.Bind(
            "RageMode",
            "KeyPressCooldown",
            0.2f,
            "Cooldown between Rage key presses in seconds (0.2 = 200ms)"
        );

        rageGlowColorR = Config.Bind("RageVisualEffects", "GlowColorR", 1.0f, "Rage edge glow red (0-1)");
        rageGlowColorG = Config.Bind("RageVisualEffects", "GlowColorG", 0.0f, "Rage edge glow green (0-1)");
        rageGlowColorB = Config.Bind("RageVisualEffects", "GlowColorB", 0.15f, "Rage edge glow blue (0-1)");
        rageGlowMaxAlpha = Config.Bind("RageVisualEffects", "GlowMaxAlpha", 0.55f, "Rage edge glow max alpha (0-1)");

        rageHandsGlowEnable = Config.Bind("RageVisualEffects", "HandsGlowEnable", true, "Enable red glow on Aradia hands during Rage");
        rageHandsGlowColorR = Config.Bind("RageVisualEffects", "HandsGlowColorR", 1.0f, "Hands glow red (0-1)");
        rageHandsGlowColorG = Config.Bind("RageVisualEffects", "HandsGlowColorG", 0.0f, "Hands glow green (0-1)");
        rageHandsGlowColorB = Config.Bind("RageVisualEffects", "HandsGlowColorB", 0.15f, "Hands glow blue (0-1)");
        rageHandsGlowAlpha = Config.Bind("RageVisualEffects", "HandsGlowAlpha", 0.85f, "Hands glow alpha (0-1)");
        rageHandsGlowSizePx = Config.Bind("RageVisualEffects", "HandsGlowSizePx", 96f, "Hands glow size in pixels");
        
        rageHandsParticleEnable = Config.Bind("RageVisualEffects", "HandsParticleEnable", true, "Enable red fire particle effects on hands during Rage (like Mafia Muscle)");
        rageHandsParticleEmissionRate = Config.Bind("RageVisualEffects", "HandsParticleEmissionRate", 20.0f, "Particle emission rate (particles per second)");
        rageHandsParticleSize = Config.Bind("RageVisualEffects", "HandsParticleSize", 4.0f, "Particle size multiplier");
        rageHandsParticleColorR = Config.Bind("RageVisualEffects", "HandsParticleColorR", 1.0f, "Particle color Red (0-1)");
        rageHandsParticleColorG = Config.Bind("RageVisualEffects", "HandsParticleColorG", 0.0f, "Particle color Green (0-1)");
        rageHandsParticleColorB = Config.Bind("RageVisualEffects", "HandsParticleColorB", 0.15f, "Particle color Blue (0-1)");

        rageWingsEnable = Config.Bind(
            "RageVisualEffects",
            "WingsEnable",
            true,
            "Tier 3 Rage: enable demon wings sprite loop on kubi bone"
        );
        rageWingsDurationSeconds = Config.Bind(
            "RageVisualEffects",
            "WingsDurationSeconds",
            13f,
            "Tier 3 wings: loop duration in seconds"
        );
        rageWingsFps = Config.Bind(
            "RageVisualEffects",
            "WingsFps",
            24f,
            "Tier 3 wings: animation speed (frames per second)"
        );
        rageWingsScale = Config.Bind(
            "RageVisualEffects",
            "WingsScale",
            1f,
            "Tier 3 wings: local scale multiplier"
        );
        rageWingsOffsetX = Config.Bind(
            "RageVisualEffects",
            "WingsOffsetX",
            -0.05f,
            "Tier 3 wings: local X offset from kubi bone (bone space)"
        );
        rageWingsOffsetY = Config.Bind(
            "RageVisualEffects",
            "WingsOffsetY",
            0f,
            "Tier 3 wings: local Y offset from kubi bone (bone space)"
        );

        slowMoEdgeBarsColorR = Config.Bind("SlowMoVisualEffects", "EdgeBarsColorR", 0.3f, "SlowMo edge bars (top/bottom) red (0-1)");
        slowMoEdgeBarsColorG = Config.Bind("SlowMoVisualEffects", "EdgeBarsColorG", 0.6f, "SlowMo edge bars green (0-1)");
        slowMoEdgeBarsColorB = Config.Bind("SlowMoVisualEffects", "EdgeBarsColorB", 1.0f, "SlowMo edge bars blue (0-1)");
        slowMoEdgeBarsMaxAlpha = Config.Bind("SlowMoVisualEffects", "EdgeBarsMaxAlpha", 0.5f, "SlowMo edge bars max alpha (0-1)");
        slowMoBoneGlowEnable = Config.Bind("SlowMoVisualEffects", "BoneGlowEnable", true, "Enable blue glow on bones (bone3, bone8) during TimeSlowMo");
        slowMoBoneGlowColorR = Config.Bind("SlowMoVisualEffects", "BoneGlowColorR", 0.3f, "SlowMo bone glow red (0-1)");
        slowMoBoneGlowColorG = Config.Bind("SlowMoVisualEffects", "BoneGlowColorG", 0.6f, "SlowMo bone glow green (0-1)");
        slowMoBoneGlowColorB = Config.Bind("SlowMoVisualEffects", "BoneGlowColorB", 1.0f, "SlowMo bone glow blue (0-1)");
        slowMoBoneGlowAlpha = Config.Bind("SlowMoVisualEffects", "BoneGlowAlpha", 0.85f, "SlowMo bone glow alpha (0-1)");
        slowMoBoneGlowSizePx = Config.Bind("SlowMoVisualEffects", "BoneGlowSizePx", 48f, "SlowMo bone glow size in pixels");
        

        mbFogPulseAmount = Config.Bind("MindBrokenVisualEffects", "FogPulseAmount", 0.03f, "Fog pulse amplitude (0.03 = barely visible)");

        qteSuccessVolumeMultiplier = Config.Bind(
            "QTE",
            "SuccessVolumeMultiplier",
            1.5f,
            "Volume multiplier for successful QTE button press sound (1.0 = 100%)"
        );

        qteFailureVolumeMultiplier = Config.Bind(
            "QTE",
            "FailureVolumeMultiplier",
            1.5f,
            "Volume multiplier for QTE error sound (1.0 = 100%)"
        );
        
        // QTE System 3.0 - SP Gain
        qteSPGainBase = Config.Bind(
            "QTE",
            "SPGainBase",
            0.05f,
            "SP gain for A/D buttons at 0% MindBroken (0.05 = 5% of MaxSP)"
        );
        
        qteSPGainMin = Config.Bind(
            "QTE",
            "SPGainMin",
            0.02f,
            "SP gain for A/D buttons at 100% MindBroken (0.02 = 2% of MaxSP)"
        );
        
        qteYellowButtonSPGainMin = Config.Bind(
            "QTE",
            "YellowButtonSPGainMin",
            0.15f,
            "Minimum SP gain for yellow W/S buttons (0.15 = 15% of MaxSP)"
        );
        
        qteYellowButtonSPGainMax = Config.Bind(
            "QTE",
            "YellowButtonSPGainMax",
            0.3f,
            "Maximum SP gain for yellow W/S buttons (0.3 = 30% of MaxSP)"
        );
        
        qteClickSPGainBase = Config.Bind(
            "QTE",
            "ClickSPGainBase",
            0.015f,
            "SP gain for mouse/E click during struggle at 0% MindBroken (0.015 = 1.5% of MaxSP)"
        );
        qteClickSPGainMin = Config.Bind(
            "QTE",
            "ClickSPGainMin",
            0.005f,
            "SP gain for mouse/E click during struggle at 100% MindBroken (0.005 = 0.5% of MaxSP)"
        );
        
        // QTE System 3.0 - Penalties
        qteMPPenaltyPercent = Config.Bind(
            "QTE",
            "MPPenaltyPercent",
            0.3f,
            "MP penalty for wrong button press (0.3 = 30% of MaxMP)"
        );
        
        qteMindBrokenPenaltyPercent = Config.Bind(
            "QTE",
            "MindBrokenPenaltyPercent",
            0.002f,
            "MindBroken penalty for wrong W/S press during cooldown (0.002 = 0.2%)"
        );
        
        qteRedButtonMindBrokenPenalty = Config.Bind(
            "QTE",
            "RedButtonMindBrokenPenalty",
            0.002f,
            "MindBroken penalty for pressing red W/S button (0.002 = 0.2%)"
        );
        
        qteSPPenaltyMultiplier = Config.Bind(
            "QTE",
            "SPPenaltyMultiplier",
            2.0f,
            "SP penalty multiplier for wrong A/D press during cooldown (2.0 = 2x the correct press gain)"
        );
        
        // QTE System 3.0 - Timers
        qteWindowDurationMin = Config.Bind(
            "QTE",
            "WindowDurationMin",
            2f,
            "Minimum QTE window duration in seconds"
        );
        
        qteWindowDurationMax = Config.Bind(
            "QTE",
            "WindowDurationMax",
            3.5f,
            "Maximum QTE window duration in seconds"
        );
        
        qteCooldownDurationMin = Config.Bind(
            "QTE",
            "CooldownDurationMin",
            2f,
            "Minimum cooldown between windows in seconds"
        );
        
        qteCooldownDurationMax = Config.Bind(
            "QTE",
            "CooldownDurationMax",
            4f,
            "Maximum cooldown between windows in seconds"
        );
        
        // QTE System 3.0 - Button Positioning
        qteButtonPositionY = Config.Bind(
            "QTE",
            "ButtonPositionY",
            200f,
            "Y position from top in pixels (200 = 200px from top)"
        );
        
        qteButtonSpacing = Config.Bind(
            "QTE",
            "ButtonSpacing",
            100f,
            "Spacing between buttons in horizontal row (100px)"
        );
        
        
        qteColorChangeInterval = Config.Bind(
            "QTE",
            "ColorChangeInterval",
            1f,
            "Color change interval for W/S buttons in seconds"
        );
        
        qtePressIndicatorDuration = Config.Bind(
            "QTE",
            "PressIndicatorDuration",
            0.15f,
            "Visual press indicator duration (green/red flash) in seconds"
        );
        
        qteMaxButtonTransparency = Config.Bind(
            "QTE",
            "MaxButtonTransparency",
            0.5f,
            "Maximum button transparency at 100% MindBroken (0.5 = 50%, 0.0 = opaque, 1.0 = fully transparent)"
        );
        
        qteMaxPinkShadowIntensity = Config.Bind(
            "QTE",
            "MaxPinkShadowIntensity",
            1f,
            "Maximum pink neon shadow brightness at 100% MindBroken (1.0 = 100%, 0.0 = no shadow)"
        );
        
        // QTE System 3.0 - Combo
        qteComboMilestone = Config.Bind(
            "QTE",
            "ComboMilestone",
            10,
            "Combo threshold for bonus activation (counter of correct yellow button presses)"
        );
        
        // QTE System 3.0 - Enable/Disable
        enableQTESystem = Config.Bind(
            "QTE",
            "EnableQTESystem",
            true,
            "Enable or disable QTE System 3.0 (struggle system)"
        );
        
        // H-Scene Effects
        enableStartZoomEffect = Config.Bind(
            "HSceneEffects",
            "StartZoom.Enable",
            true,
            "Enable zoom and slowmo effect when H-scene starts"
        );
        
        startZoomAmount = Config.Bind(
            "HSceneEffects",
            "StartZoom.Amount",
            3.0f,
            "Zoom level when H-scene starts (3.0 = 3.0x zoom)"
        );
        
        startZoomDuration = Config.Bind(
            "HSceneEffects",
            "StartZoom.Duration",
            4.0f,
            "Duration of zoom animation in seconds (4.0 = smooth 2.0 second zoom)"
        );
        
        startSlowmoDelay = Config.Bind(
            "HSceneEffects",
            "StartZoom.SlowmoDelay",
            0.5f,
            "Delay after zoom before slowmo starts in seconds (0.5 = half second pause)"
        );
        
        startSlowmoTimeScale = Config.Bind(
            "HSceneEffects",
            "StartZoom.SlowmoTimeScale",
            0.2f,
            "Time scale during slowmo (0.2 = 80% slowdown)"
        );
        
        startSlowmoDuration = Config.Bind(
            "HSceneEffects",
            "StartZoom.SlowmoDuration",
            2.0f,
            "Duration of slowmo effect in seconds"
        );
        
        enableStartCenter = Config.Bind(
            "HSceneEffects",
            "StartCenter.Enable",
            true,
            "Enable camera centering on animation center when H-scene starts"
        );
        
        startCenterDuration = Config.Bind(
            "HSceneEffects",
            "StartCenter.Duration",
            0.5f,
            "Duration of camera centering animation in seconds (0.5 = faster, more aggressive)"
        );
        
        startCenterYOffset = Config.Bind(
            "HSceneEffects",
            "StartCenter.YOffset",
            0.0f,
            "Y offset for camera centering (positive = up, negative = down)"
        );
 
        // Touzoku aggression settings
        touzokuSpeedMultiplier = Config.Bind(
            "TouzokuAggression",
            "SpeedMultiplier",
            1.5f,
            "Touzoku speed multiplier (1.0-3.0). Affects movement and attack speed. 1.5 = +50% speed."
        );
        
        touzokuAttackRangeMultiplier = Config.Bind(
            "TouzokuAggression",
            "AttackRangeMultiplier",
            1.4f,
            "Touzoku attack range multiplier (1.0-2.5). Affects attack distance. 1.4 = +40% range."
        );

        
        // Goblin hardcore features
        enableGoblinStruggleSpawn = Config.Bind(
            "GoblinHardcore",
            "EnableStruggleSpawn",
            true,
            "HARDMODE: When player breaks free from goblin START animation (where 3 goblins appear), spawn 2 additional goblins to maintain consistency. Disable if causing issues."
        );
        
        // BigoniBrother START2 animation configs
        bigoniBrotherStart2RepeatCount = Config.Bind(
            "BigoniBrother",
            "Start2RepeatCount",
            3,
            "Number of times START2 animation should play before transitioning to START3 (default: 3)"
        );
        
        bigoniBrotherStart2TimeScale = Config.Bind(
            "BigoniBrother",
            "Start2TimeScale",
            1.0f,
            "Time scale for START2 animation (1.0 = normal speed, 2.0 = 2x speed, default: 1.0)"
        );
        
        // CumDisplay configs
        cumDisplayFrameDuration = Config.Bind(
            "CumDisplay",
            "FrameDuration",
            1f / 25f,
            "X-ray banner frame duration in seconds (1/25 = ~25 FPS)"
        );
        
        cumDisplayAnchoredOffsetX = Config.Bind(
            "CumDisplay",
            "AnchoredOffsetX",
            450f,
            "X-ray banner X offset from screen center in pixels (right)"
        );
        
        cumDisplayAnchoredOffsetY = Config.Bind(
            "CumDisplay",
            "AnchoredOffsetY",
            100f,
            "X-ray banner Y offset from screen center in pixels (up)"
        );
        
        cumDisplayOralOffsetYDelta = Config.Bind(
            "CumDisplay",
            "OralOffsetYDelta",
            -140f,
            "Additional Y offset for oral clips (negative = down)"
        );
        
        cumDisplayPregnantOffsetX = Config.Bind(
            "CumDisplay",
            "PregnantOffsetX",
            0.25f,
            "Pregnancy banner X offset in normalized viewport coordinates (0.25 = right from center)"
        );
        
        cumDisplayPregnantOffsetY = Config.Bind(
            "CumDisplay",
            "PregnantOffsetY",
            0f,
            "Pregnancy banner Y offset in normalized viewport coordinates"
        );
        
        cumDisplayWorldDepth = Config.Bind(
            "CumDisplay",
            "WorldDepth",
            3f,
            "Distance from camera for WorldSpace banner rendering"
        );
        
        cumDisplaySizeMultiplier = Config.Bind(
            "CumDisplay",
            "SizeMultiplier",
            2.5f,
            "Banner size multiplier (2.5 = 2.5x increase)"
        );
        
        // SoundOnomatopoeia configs
        soundOnomatopoeiaTimeout = Config.Bind(
            "SoundOnomatopoeia",
            "SoundTimeout",
            10f,
            "Timeout in seconds between onomatopoeia displays for one sound"
        );
        
        // DialogueEventProcessor configs
        dialogueEventMinCooldown = Config.Bind(
            "DialogueEventProcessor",
            "MinCooldown",
            0.1f,
            "Minimum cooldown in seconds between dialogue event processing"
        );
        
        // Combat Camera Preset (V key)
        enableCombatCameraPresets = Config.Bind(
            "CombatCamera",
            "EnableCombatCameraPresets",
            true,
            "Enable V key to toggle between standard and far zoom during combat (outside H-scenes)"
        );
        
        combatCameraFarZoom = Config.Bind(
            "CombatCamera",
            "FarZoom",
            1.4f,
            "Far zoom multiplier (1st V press). Camera zooms out by this factor. Values <= 1.1 are clamped to 1.4."
        );
        
        combatCameraUltraFarZoom = Config.Bind(
            "CombatCamera",
            "UltraFarZoom",
            1.8f,
            "Ultra-far zoom multiplier (2nd V press). Camera zooms out even further. Values <= 1.1 are clamped to 1.8."
        );
        
        // H-Scene Camera Zoom configs
        cameraZoomLevel10x = Config.Bind(
            "HSceneCameraZoom",
            "ZoomLevel10x",
            10f,
            "[UNUSED] Zoom level for 10x magnification (not in current cycle)"
        );
        
        cameraZoomLevel8x = Config.Bind(
            "HSceneCameraZoom",
            "ZoomLevel8x",
            8f,
            "[UNUSED] Zoom level for 8x magnification (not in current cycle)"
        );
        
        cameraZoomLevel5x = Config.Bind(
            "HSceneCameraZoom",
            "ZoomLevel5x",
            5f,
            "[UNUSED] Zoom level for 5x magnification (not in current cycle)"
        );
        
        cameraZoomLevel4x = Config.Bind(
            "HSceneCameraZoom",
            "ZoomLevel4x",
            4f,
            "[UNUSED] Zoom level for 4x magnification (not in current cycle)"
        );
        
        cameraZoomLevel3x = Config.Bind(
            "HSceneCameraZoom",
            "ZoomLevel3x",
            3f,
            "[UNUSED] Zoom level for 3x magnification (not in current cycle)"
        );
        
        cameraZoomLevel2x = Config.Bind(
            "HSceneCameraZoom",
            "ZoomLevel2x",
            2f,
            "[UNUSED] Zoom level for 2x magnification (hardcoded cycle: 1.5x → 3x → 6x → 1.5x)"
        );
        
        cameraZoomResetValue = Config.Bind(
            "HSceneCameraZoom",
            "ResetZoomValue",
            1.5f,
            "Default zoom level when H-scene ends (matches game default)"
        );
        
        
        // Splash Screen configs
        showSplashScreenOnStartup = Config.Bind(
            "General",
            "ShowSplashScreenOnStartup",
            true,
            "Show HELLGATE splash screen on game startup. Set to false to skip splash screen."
        );
        
        hellGateLanguage = Config.Bind(
            "General",
            "HellGateLanguage",
            "",
            "Selected language for HELLGATE mod. Available: RU, EN, JP, CN, KR, FR, DE, PT, BR, ES. Set automatically on first language selection."
        );

        enableBadEndPlayer = Config.Bind(
            "BadEndPlayer",
            "Enable",
            true,
            "Enable BadEnd Player module. When true, BadEnd (MindBroken 100% + timer) shows the image player instead of YOU LOSE + epilogue. Content from sources/HellGate_sources/BadEndPlayer_Proto."
        );
        
        vengeanceMindBrokenReduceFraction = Config.Bind(
            "TakeVengeance",
            "MindBrokenReduceFraction",
            0.9f,
            "On Take Vengeance (death/BadEnd respawn): reduce MindBroken by this fraction of current value (0.9 = 90% reduction, e.g. 90% -> 9%)"
        );
        vengeanceRageBonusPercent = Config.Bind(
            "TakeVengeance",
            "RageBonusPercent",
            10f,
            "On Take Vengeance (respawn): flat Rage added after optional drain. Default 10 = +10% on the bar."
        );
        vengeanceRageDrainFractionOfCurrent = Config.Bind(
            "TakeVengeance",
            "RageDrainFractionOfCurrent",
            0f,
            "On Take Vengeance: remove this fraction of *current* Rage before RageBonusPercent is applied (0 = no drain, 0.5 = lose half of current Rage, 1 = reset Rage to 0 before bonus)."
        );
        vengeanceRageMaxPercentAfter = Config.Bind(
            "TakeVengeance",
            "RageMaxPercentAfter",
            10f,
            "After Take Vengeance (after drain + bonus): clamp Rage to at most this value (10 = keep 10% or less). Use -1 to disable the cap."
        );
        badEndTakeVengeanceRespawnEnemies = Config.Bind(
            "TakeVengeance",
            "BadEndRespawnEnemies",
            true,
            "On Take Vengeance from BadEnd: respawn enemies at spawn points"
        );
        badEndTakeVengeanceEnemyRespawnDelay = Config.Bind(
            "TakeVengeance",
            "BadEndEnemyRespawnDelay",
            1.2f,
            "Delay in seconds before enemy respawn after Take Vengeance from BadEnd (default 1.2)"
        );
    }

    private void Update() {
        StruggleSystem.Update();
    }

    private void OnDestroy() {
        SceneManager.sceneLoaded -= OnSceneLoaded_ResetCaches;
        // Cleanup visual indicators
        StruggleVisualIndicators.Cleanup();
        
        // Cleanup handoff system
        EnemyHandoffSystem.ResetAllData();
        
        // Cleanup MindBroken systems
        try {
            NoREroMod.Patches.UI.MindBroken.CorruptionCaptionsSystem.Cleanup();
            NoREroMod.Patches.UI.MindBroken.MindBrokenRecoverySystem.Cleanup();
            NoREroMod.Patches.UI.MindBroken.MindBrokenVisualEffectsSystem.Cleanup();
        } catch { }
        try {
            NoREroMod.Systems.Audio.AttackSoundSystem.Cleanup();
        } catch { }
        harmony?.UnpatchSelf();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Reset caches on scene change. Prevents "click disable" during struggle
    /// due to stale player/camera refs after optimization.
    /// </summary>
    private static void OnSceneLoaded_ResetCaches(Scene scene, LoadSceneMode mode)
    {
        try
        {
            UnifiedPlayerCacheManager.ResetCache();
            UnifiedCameraCacheManager.ResetCache();
            NoREroMod.Systems.Cache.UnifiedGameControllerCacheManager.ResetCache();
            if (Instance != null && _harmonyForLatePatches != null)
                Instance.ApplyDoreiCombatAiPatch(_harmonyForLatePatches);
        }
        catch (Exception ex)
        {
            Log?.LogWarning($"[HellGate] Cache reset on scene load failed: {ex.Message}");
        }
    }

    public static void QuitToTitleScreen() {
        SceneManager.LoadScene("Gametitle");
        var gc = GameObject.FindWithTag("GameController");
        if (gc != null) Destroy(gc);
        NoREroMod.Systems.Cache.UnifiedGameControllerCacheManager.ResetCache();
    }

    public static void DeleteAllSaveFiles() {
        string savePath = Application.dataPath + "/../SaveData/SaveData";
        ES2.Delete(savePath + "01.txt");
        ES2.Delete(savePath + "02.txt");
        ES2.Delete(savePath + "03.txt");
    }

    public static void ExpDrain(PlayerStatus ps, float percentage) {
        if (percentage == 0) { return; }
        int expToLevelUp = Mathf.FloorToInt((500f + (500f + (float)(ps.LV * ps.LV) * 0.7f * 10f)) * 1.2f / 2f + (float)(ps.LV * 15 + ps.LV * ps.LV));
        Plugin.totalExpToLose += expToLevelUp * percentage;
        int expToLose = Mathf.FloorToInt(Plugin.totalExpToLose);

        if (expToLose == 0) { return; }
        Plugin.totalExpToLose -= expToLose;
        if (ps.Exppoint - expToLose < 0) {
            ps.Exppoint = 0;
            Plugin.LevelDrain(ps);
        }
        else {
            ps.Exppoint -= expToLose;
        }
    }

    public static void LevelDrain(PlayerStatus ps) {
        if (!Plugin.enableDelevel.Value) { return; }
        if (ps.LV <= 1) { return; }

        List<string> statsToLower = new List<string>();
        if (ps.MaxHp > 200) {
            statsToLower.Add("HP");
        }
        if (ps.MaxMp > 100) {
            statsToLower.Add("MP");
        }
        if (ps.MaxSp > 100) {
            statsToLower.Add("SP");
        }
        if (ps.Str > 5) {
            statsToLower.Add("STR");
        }
        if (ps.Dex > 5) {
            statsToLower.Add("DEX");
        }
        if (ps.Int > 5) {
            statsToLower.Add("INT");
        }
        if (ps.LUCK > 1) {
            statsToLower.Add("LUK");
        }

        switch (statsToLower[Random.Range(0, statsToLower.Count)]) {
            case "HP":
                ps.MaxHp -= 10;
                break;
            case "MP":
                ps.MaxMp -= 10;
                break;
            case "SP":
                ps.MaxSp -= 5;
                break;
            case "STR":
                ps.Str -= 1;
                break;
            case "DEX":
                ps.Dex -= 1;
                break;
            case "INT":
                ps.Int -= 1;
                break;
            case "LUK":
                ps.LUCK -= 1;
                break;
            default:
                break;
        }

        int targetLevel = ps.LV - 1;
        int expToNextLevel = Mathf.FloorToInt((500f + (500f + (float)(targetLevel * targetLevel) * 0.7f * 10f)) * 1.2f / 2f + (float)(targetLevel * 15 + targetLevel * targetLevel));
        int expToRefund = Mathf.FloorToInt(expToNextLevel * Plugin.expDelevelRefundPercent.Value);
        ps.Exppoint += expToRefund;
        ps.LV -= 1;
    }
    
    private System.Collections.IEnumerator ShowSplashScreenImmediately()
    {
        yield return null;
        yield return null;
        yield return null;
        
        try {
            NoREroMod.Systems.UI.HellGateSplashScreen.Initialize();
        } catch { }
    }

    // Dialogue font helper methods
    public static FontStyle GetFontStyle(int styleValue) {
        switch (styleValue) {
            case 1: return FontStyle.Bold;
            case 2: return FontStyle.Italic;
            case 3: return FontStyle.BoldAndItalic;
            default: return FontStyle.Normal;
        }
    }

    public static Color ParseColor(string colorString) {
        try {
            string[] parts = colorString.Split(',');
            if (parts.Length >= 4) {
                return new Color(
                    float.Parse(parts[0]),
                    float.Parse(parts[1]),
                    float.Parse(parts[2]),
                    float.Parse(parts[3])
                );
            }
        } catch { }
        return Color.white;
    }

}