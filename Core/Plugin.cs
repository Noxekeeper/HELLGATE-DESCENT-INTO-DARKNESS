using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
using NoREroMod.Systems.EventCore.Core;
using NoREroMod.Systems.Pregnancy;
using NoREroMod.Systems.Pregnancy.Patches;

namespace NoREroMod;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInProcess("NightofRevenge.exe")]
public class Plugin : BaseUnityPlugin {

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

    public static ConfigEntry<bool> eventCoreEnable;
    public static ConfigEntry<KeyCode> eventCoreDevHotkey;
    public static ConfigEntry<string> eventCoreDevEventId;
    /// <summary>Dimming strength over the decorative EventCore frame (0 = frame-only tuning; ~0.88 ≈ legacy full-panel feel).</summary>
    public static ConfigEntry<float> eventCoreModalDimAlpha;

    /// <summary>When true, hides the vanilla gameplay HUD (root <c>Canvas</c>) while the EventCore modal is open.</summary>
    public static ConfigEntry<bool> eventCoreHideVanillaHud;

    /// <summary>Extra scale for Aradia bust PNGs in broker modal (left). Lower if face art is cropped tight.</summary>
    public static ConfigEntry<float> eventCoreBrokerPortraitAradiaScale;

    /// <summary>Extra scale for Touzoku bust PNGs in broker modal (right). Raise if hood sprites look too small.</summary>
    public static ConfigEntry<float> eventCoreBrokerPortraitTouzokuScale;

    /// <summary>Non-modal EventTrap encounters (coordinate-zone suspicion, knockdown ambush); JSON under HellGateJson/EventCore.</summary>
    public static ConfigEntry<bool> eventTrapEncountersEnable;

    /// <summary>Knockdown reinforcement spawns (anchor + radius); JSON: reinforcement_registry.json and EventCore/_shared/&lt;folder&gt;/config.json. Optional suspicion lines via phrasesFromEventFolder.</summary>
    public static ConfigEntry<bool> reinforcementEncountersEnable;

    /// <summary>Deprecated config key; kept so old cfg lines still apply when seeding <see cref="eventTrapEncountersEnable"/>.</summary>
    public static ConfigEntry<bool> ambientSpikeEncountersEnable;

    // GrabSystem NG (GrabViaAttack)
    public static ConfigEntry<bool> enableGrabViaAttack;
    public static ConfigEntry<bool> disableOriginalEliteGrab;
    public static ConfigEntry<bool> grabViaAttackEliteOnly;
    public static ConfigEntry<bool> grabBlockImmunity;
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

    /// <summary>Vengeance Strike (parry follow-up stab): assets, VFX, slow-mo, rage cost, grab denial.</summary>
    public static ConfigEntry<bool> enableVengeanceStrikeAssets;
    public static ConfigEntry<string> vengeanceStrikeSoundFile;
    public static ConfigEntry<bool> enableVengeanceStrikePlayOnStab;
    public static ConfigEntry<bool> enableVengeanceStrikeHandGlow;
    public static ConfigEntry<float> vengeanceStrikeHandsParticleSizeMult;
    public static ConfigEntry<float> vengeanceStrikeHandsEmitterAreaMult;
    public static ConfigEntry<float> vengeanceStrikeHandsEmissionMult;
    public static ConfigEntry<int> vengeanceStrikeHandsMaxParticles;
    public static ConfigEntry<float> vengeanceStrikeHandsParticleLifetimeMin;
    public static ConfigEntry<float> vengeanceStrikeHandsParticleLifetimeMax;
    public static ConfigEntry<float> vengeanceStrikeHandsParticleSpeedMin;
    public static ConfigEntry<float> vengeanceStrikeHandsParticleSpeedMax;
    public static ConfigEntry<float> vengeanceStrikeHandsLeftColorR;
    public static ConfigEntry<float> vengeanceStrikeHandsLeftColorG;
    public static ConfigEntry<float> vengeanceStrikeHandsLeftColorB;
    public static ConfigEntry<float> vengeanceStrikeHandsRightColorR;
    public static ConfigEntry<float> vengeanceStrikeHandsRightColorG;
    public static ConfigEntry<float> vengeanceStrikeHandsRightColorB;
    public static ConfigEntry<bool> vengeanceStrikeHandsCoreEnable;
    public static ConfigEntry<float> vengeanceStrikeHandsCoreScaleMult;

    public static ConfigEntry<bool> enableVengeanceStrikeSlowMo;
    public static ConfigEntry<float> vengeanceStrikeSlowMoTimeScale;
    /// <summary>Real-time seconds Vengeance Strike slow-mo lasts (not tied to stab animation).</summary>
    public static ConfigEntry<float> vengeanceStrikeSlowMoDurationSeconds;
    public static ConfigEntry<bool> enableVengeanceStrikeSpineBoost;
    public static ConfigEntry<float> vengeanceStrikeSpineMultiplier;
    public static ConfigEntry<bool> vengeanceStrikeSpineCompensateSlowMo;
    /// <summary>While parry stab active, block grab (collision + grab-via-attack).</summary>
    public static ConfigEntry<bool> enableVengeanceStrikeBlockGrabDuringStab;
    public static ConfigEntry<bool> enableVengeanceStrikeRageCost;
    public static ConfigEntry<float> vengeanceStrikeRageCostPercent;

    public static ConfigEntry<bool> enableAirGuard;
    public static ConfigEntry<bool> enableHitBloodParticleCleanup;
    public static ConfigEntry<float> hitBloodParticleCleanupDelaySeconds;

    public static ConfigEntry<int> witchGreatswordDuplicateLastTwoRounds;
    /// <summary>bigwitch ground hits 5–8 (atk_fun) only while Rage is active.</summary>
    public static ConfigEntry<bool> witchExtendedGroundComboRequiresRage;

    public static ConfigEntry<bool> enableFoV;
    public static ConfigEntry<float> frontViewDistance;
    public static ConfigEntry<float> backViewDistance;

    public static ConfigEntry<bool> isHardcoreMode;

    public static ConfigEntry<bool> trappedSavePoints;
    public static ConfigEntry<bool> shrinesRetoreVirginity; // v0.11.5 Rebalanced

    // Wolf Mod - path to Wolf Mod Spine assets
    public static ConfigEntry<string> wolfModAssetsPath;
    // RickEnemyMod - shared Rick fatality assets (Fatality Logo + per-enemy folders like Butcher)
    public static ConfigEntry<string> rickEnemyModAssetsPath;
    // Deprecated alias; falls back when RickEnemyMod AssetsPath is empty
    public static ConfigEntry<string> butcherModAssetsPath;
    // Hellish Touzoku - path to Hellish Touzoku Spine assets (Boss / Axe / Sword)
    public static ConfigEntry<string> hellishTouzokuAssetsPath;
    public static ConfigEntry<float> hellishTouzokuScaleMultiplier;

    // Dorei Mod - path to DoreiFapping (idle during H-scene spectator)
    public static ConfigEntry<string> doreiFappingAssetsPath;
    public static ConfigEntry<float> doreiSpectatorScaleMultiplier;

    /// <summary>PNG portrait overlay replacing vanilla <c>UIface</c> Spine content; assets under <c>sources/HellGate_sources/Portrait_mod</c>.</summary>
    public static ConfigEntry<bool> enablePortraitMod;
    public static ConfigEntry<string> portraitModAssetsPath;
    public static ConfigEntry<float> portraitModFrameSeconds;
    public static ConfigEntry<float> portraitModBrainwashThreshold;
    /// <summary>Uniform <see cref="UnityEngine.RectTransform.localScale"/> on the overlay after native sizing (1 = default).</summary>
    public static ConfigEntry<float> portraitModDisplayScale;
    /// <summary>After <c>SetNativeSize</c>: maximum width in layout units before <see cref="portraitModDisplayScale"/>; 0 disables clamping.</summary>
    public static ConfigEntry<float> portraitModMaxNativeWidth;

    // HellTraps — lethal magic trap variant + custom death PNG clip
    public static ConfigEntry<bool> enableLethalMagicTrap;
    public static ConfigEntry<float> lethalMagicTrapDamageMultiplier;
    public static ConfigEntry<string> lethalMagicTrapDeathClipPath;
    public static ConfigEntry<float> lethalMagicTrapDeathClipDisplayScale;
    public static ConfigEntry<float> lethalMagicTrapActTimeMultiplier;
    public static ConfigEntry<float> lethalMagicTrapBulletSpeedMultiplier;
    public static ConfigEntry<float> lethalMagicTrapSpawnScale;
    public static ConfigEntry<bool> enableLethalCocoonTrap;
    public static ConfigEntry<string> lethalCocoonTrapDeathClipPath;
    public static ConfigEntry<float> lethalCocoonTrapDeathClipDisplayScale;

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
    public static ConfigEntry<float> mindBrokenHScenePercentPerSecond;
    public static ConfigEntry<float> mindBrokenMaxPercent;
    public static ConfigEntry<float> mindBrokenStruggleBonusPerStep;
    public static ConfigEntry<float> mindBrokenBadEndCountdownDuration;
    public static ConfigEntry<float> mindBrokenBadEndResetThreshold;
    public static ConfigEntry<bool> mindBrokenHighRagePassiveEnable;
    public static ConfigEntry<float> mindBrokenHighRageThresholdPercent;
    public static ConfigEntry<float> mindBrokenHighRagePassivePercentPerSecond;
    public static ConfigEntry<bool> mindBrokenHighRagePassiveOnlyWhenRageInactive;
    public static ConfigEntry<bool> mindBrokenDebugLogAddPercent;

    // MindBroken gain during special H-scene states
    public static ConfigEntry<bool> enableHSceneBlackBackground;
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
    /// <summary>While Rage burst is active: block grab and knockdown from power melee (see RageActiveImmunityPatch).</summary>
    public static ConfigEntry<bool> rageActiveImmuneGrabAndKnockdown;
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
    public static ConfigEntry<bool> rageActivationCameraShake;
    public static ConfigEntry<float> rageGrabDrainMin;
    public static ConfigEntry<float> rageGrabDrainMax;
    public static ConfigEntry<float> rageSlowMoDrainMultiplier;
    public static ConfigEntry<float> rageSlowMoMBGainMultiplier;
    public static ConfigEntry<float> rageUIPositionX;
    public static ConfigEntry<float> rageUIPositionY;
    public static ConfigEntry<float> rageBloodEffectDuration;
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
    public static ConfigEntry<float> qteButtonPositionX;
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
    public static ConfigEntry<bool> startZoomSkipEnemyFatality;
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
    public static ConfigEntry<bool> lethalTrapVengeanceShockSoundEnable;
    public static ConfigEntry<float> lethalTrapVengeanceShockMindShockVolume;
    public static ConfigEntry<float> lethalTrapVengeanceShockHeartBeatVolume;
    
    // Visual indicators configs
    public static ConfigEntry<bool> disableStruggleCameraShake;
    public static ConfigEntry<bool> enableStruggleVisualIndicators;
    public static ConfigEntry<bool> showDifficultyIndicator;
    public static ConfigEntry<bool> showProgressIndicator;
    public static ConfigEntry<bool> showCriticalChanceIndicator;

    // Dialogue font configs
    public static ConfigEntry<float> dialogueFontSize;
    public static ConfigEntry<string> fontFileWestern;
    public static ConfigEntry<string> fontFamilyWestern;
    public static ConfigEntry<string> fontFileAsian;
    public static ConfigEntry<string> fontFamilyAsian;
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
        try { NoREroMod.Systems.Pregnancy.PregnancyConfig.Initialize(); }
        catch (Exception ex) { Log?.LogWarning($"[Pregnancy] Config init failed: {ex.Message}"); }
        NoREroMod.Systems.Compatibility.NoREroModScaffoldConfigPush.Apply();
        try {
            NoREroMod.Systems.Spawn.SpawnTemplateCatalog.Initialize(this);
        } catch (Exception ex) {
            Log?.LogWarning($"[SPAWN CATALOG] Initialization failed: {ex.Message}");
        }
        SetUpPatches();
        try {
            NoREroMod.Patches.HellTraps.LethalMagicTrapRuntime.TryEnsureTemplateRegistered();
            NoREroMod.Patches.HellTraps.LethalMagicTrapDeathDisplay.Preload();
            NoREroMod.Patches.HellTraps.LethalCocoonTrapRuntime.TryEnsureTemplateRegistered();
            NoREroMod.Patches.HellTraps.LethalCocoonTrapDeathDisplay.Preload();
            NoREroMod.Patches.HellTraps.LethalMagicTrapDeathAudio.Initialize(this);
            NoREroMod.Patches.HellTraps.LethalTrapVengeanceShockAudio.Initialize(this);
        } catch (Exception ex) {
            Log?.LogWarning("[LethalMagicTrap] Template bootstrap failed: " + ex.Message);
        }

        try
        {
            gameObject.AddComponent<NoREroMod.Systems.Spawn.LocationTransitionSpawnController>();
        }
        catch (Exception ex)
        {
            Log?.LogWarning($"[LOCATION SPAWN] Transition controller failed to start: {ex.Message}");
        }

        EventCoreBootstrap.Install(this);

        // Initialize visual indicators
        if (enableStruggleVisualIndicators.Value) {
            StruggleVisualIndicators.Initialize();
        }
        
        // Initialize dialogue system
        try {
            NoREroMod.Systems.Dialogue.DialogueFramework.Initialize();
            NoREroMod.Systems.Dialogue.QTEReactionFramework.Initialize();
        } catch { }

        try
        {
            NoREroMod.Systems.EventCore.EventTrap.EventTrapEncounterBootstrap.Install(this);
        }
        catch (Exception ex)
        {
            Log?.LogWarning($"[EventTrap] Bootstrap failed: {ex.Message}");
        }

        try
        {
            NoREroMod.Systems.EventCore.Reinforcement.ReinforcementEncounterBootstrap.Install(this);
        }
        catch (Exception ex)
        {
            Log?.LogWarning($"[Reinforcement] Bootstrap failed: {ex.Message}");
        }
        
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

        // Reset hideout spawn flags when leaving the church so children respawn on return.
        SceneManager.sceneUnloaded += OnSceneUnloaded_ResetHideoutSpawn;

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

        // GrabChance label lives on the Rage overlay canvas now; no separate init needed.

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
        try {
            NoREroMod.Systems.Rage.TimeSlowMoActivateClipSystem.Initialize();
        } catch { }

        try {
            NoREroMod.Systems.UI.Portrait.PortraitModSystem.Initialize();
        } catch { }

        // Initialize Tentacle H-scene diagnostics (off by default; toggle via JSON).
        try {
            NoREroMod.Systems.Diagnostics.Tentacle.TentacleDiagnostics.Initialize();
        } catch (Exception ex) {
            Log?.LogWarning($"[TentacleDiag] init failed: {ex.Message}");
        }

        // Trap H-scene player-body diagnostics (off by default; toggle via JSON).
        try {
            NoREroMod.Systems.Diagnostics.TrapBody.TrapPlayerBodyDiagnostics.Initialize();
        } catch (Exception ex) {
            Log?.LogWarning($"[TrapBodyDiag] init failed: {ex.Message}");
        }

        // Kinoko / MushroomERO H-scene event diagnostics (toggle via JSON).
        try {
            NoREroMod.Systems.Diagnostics.Kinoko.KinokoMushroomEroDiagnostics.Initialize();
        } catch (Exception ex) {
            Log?.LogWarning($"[KinokoEroDiag] init failed: {ex.Message}");
        }

        // Initialize Economic / Gold module (assets, wallet, lost-pile scene loader).
        try {
            NoREroMod.Systems.Economy.EconomicConfig.Initialize();
        } catch { }
        try {
            if (NoREroMod.Systems.Economy.EconomicConfig.Enable) {
                NoREroMod.Systems.Economy.GoldAssetLoader.Initialize();
                NoREroMod.Systems.Economy.GoldWallet.Initialize();
                NoREroMod.Systems.Economy.GoldLostPileSceneLoader.Initialize();
            }
        } catch (Exception ex) {
            Log?.LogWarning($"[Economic] Module init failed: {ex.Message}");
        }

        // Startup compatibility probe for reflection-heavy integration points.
        RunNoREroModCompatibilityProbe();

        // Publish the stable integration surface only after all subsystems initialized.
        NoREroMod.HellGate.Api.HellGateApi.Initialize(PluginInfo.PLUGIN_VERSION, Log);
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
        var type = HellGateTypeResolver.Resolve(typeName);
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
        PatchType(typeof(SoloPleasureAnimeFunPatch));
        PatchType(typeof(SoloPleasureSpRecoveryPatch));
        PatchType(typeof(SoloOrgasmStruggleSetupPatch));
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
        PatchType(typeof(MummyManHandoffGrabBlockPatch));
        PatchType(typeof(MummyManHandoffStatePatch));
        PatchType(typeof(UndeadPassPatch));
        PatchType(typeof(CrowInquisitionEROFix)); // Fix animation skipping from Hellachaz
        PatchType(typeof(CrowInquisitionPassLogic)); // Handoff in gangbang
        CrowInquisitionEROFix.UnpatchHellachaz(); // Remove Hellachaz patches immediately
        PatchType(typeof(SpawnPointAnalyzer));
        PatchType(typeof(SpawnRecorderAttackInputBlockPatch));
        // Zone packs: HellGateSpawnSceneHints registry + HellGateLocationSpawnRefresh (no per-map Spawnenemy.Update hooks).
        PatchType(typeof(NoREroMod.Systems.Spawn.Patches.WitchSlaveSlimeHellGateRewardPatch));
        PatchType(typeof(NoREroMod.Systems.Spawn.SpawnRespawnAfterAltarPatch));
        PatchType(typeof(NoREroMod.Systems.Spawn.SceneMoveTransitionSpawnPatch));
        PatchTypeWithLog(typeof(NoREroMod.Patches.Player.VanillaEvSceneExitPatch), "VanillaEvSceneExitPatch");
        PatchType(typeof(NoREroMod.Systems.Spawn.SceneLoadSpawnRefreshPatch));
        try
        {
            NoREroMod.Systems.Spawn.SpawnParentInitializeGate.Install(harmony);
        }
        catch (Exception ex)
        {
            Log?.LogWarning($"[LOCATION SPAWN] Initialize gate install failed: {ex.Message}");
        }
        try
        {
            harmony.PatchAll(typeof(NoREroMod.Patches.Enemy.HeckGateEnemy.HeckGateSlimeModule));
            PatchType(typeof(NoREroMod.Patches.Enemy.HeckGateEnemy.SuraimuStruggleContextPatch));
            PatchType(typeof(NoREroMod.Patches.Enemy.HeckGateEnemy.SuraimuStruggleMaxSpEscapePostfix));
            PatchType(typeof(NoREroMod.Patches.Enemy.HeckGateEnemy.SuraimuEroAnimationStrugglePatch));
            PatchType(typeof(NoREroMod.Patches.Enemy.HeckGateEnemy.SuraimuHannomiStrugglePatch));
            PatchType(typeof(NoREroMod.Patches.Enemy.HeckGateEnemy.SuraimuActiveHSceneStrugglePatch));
            PatchType(typeof(NoREroMod.Patches.Enemy.HeckGateEnemy.SuraimuHSceneEscapeFunNowDamagePatch));
            PatchType(typeof(NoREroMod.Patches.Enemy.HeckGateEnemy.SuraimuHSceneEscapeGiveUpPatch));
            Log?.LogInfo("[biscord] HeckGateSlimeModule Harmony patches applied (Start/Update/OnDestroy).");
        }
        catch (Exception ex)
        {
            Log?.LogError($"[biscord] CRITICAL: HeckGateSlimeModule PatchAll failed — biscord rewards/drops will not run: {ex}");
        }
        PatchType(typeof(SpawnResetPatch));
        PatchType(typeof(KakasiPassLogic));
        PatchType(typeof(KakasiCrossPatch));
        PatchType(typeof(KakasiHandoffGrabBlockPatch));
        PatchType(typeof(KakasiHandoffStatePatch));
        
        try {
            harmony.PatchAll(typeof(NoREroMod.Patches.Enemy.Kakash.KakashGrabPatch));
        } catch { }
        
        PatchType(typeof(GoblinPassLogic));
        PatchType(typeof(GoblinStruggleSpawnPatch)); // HARDMODE: Spawn 2 goblins when escaping from START animation
        PatchTypeWithLog(typeof(BigoniBrotherPatch), "BigoniBrotherPatch");
        try
        {
            BigoniBrotherGameOverBypass.Apply(harmony);
            Log?.LogInfo("[PATCH] BigoniBrotherGameOverBypass nested patches applied.");
        }
        catch (Exception ex)
        {
            Log?.LogError("[PATCH] BigoniBrotherGameOverBypass FAILED: " + ex.Message);
        }
        PatchTypeWithLog(typeof(BigoniBrotherPassLogic), "BigoniBrotherPassLogic");
        PatchType(typeof(NoREroMod.Patches.Enemy.MafiaBossCustom.MafiaBossCustomStartPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.MafiaBossCustom.MafiaBossCustomPassLogic));
        PatchType(typeof(NoREroMod.Patches.Enemy.MafiaBossCustom.MafiaBossCustomEROPatches));
        PatchType(typeof(NoREroMod.Patches.Enemy.MafiaBossCustom.MafiaBossCustomGrabPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.BossTouzokuCustom.BossTouzokuCustomStartPrefixPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.BossTouzokuCustom.BossTouzokuCustomStartPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.BossTouzokuCustom.BossTouzokuCustomStartHpScalePatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.BossTouzokuCustom.BossTouzokuCustomUpdatePrefixPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.BossTouzokuCustom.BossTouzokuCustomUpdatePatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.BossTouzokuCustom.BossTouzokuCustomDistanceCapPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.BossTouzokuCustom.BossTouzokuCustomRestePatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.BossTouzokuCustom.BossTouzokuCustomDamagePatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.BossTouzokuCustom.BossTouzokuCustomMagicDamagePatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.BossTouzokuCustom.BossTouzokuCustomOnDestroyPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.BossTouzokuCustom.BossTouzokuCustomTreasurePatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.BossTouzokuCustom.BossTouzokuCustomWallPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.BossTouzokuCustom.BossTouzokuCustomIntroPatches.BlockFlagCallPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.BossTouzokuCustom.BossTouzokuCustomIntroPatches.BlockFlagCallDialogPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.BossTouzokuCustom.BossTouzokuCustomIntroPatches.BlockFlagBossBattleStartPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.BossTouzokuCustom.BossTouzokuCustomIntroPatches.BlockNextPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.BossTouzokuCustom.BossTouzokuCustomIntroPatches.CustomDeathEventPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.BossTouzokuCustom.BossTouzokuCustomIntroPatches.BlockNoREroModSuperBossSpawnPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.BossTouzokuCustom.BossTouzokuCustomIntroPatches.BlockNoREroModBossHpMultiPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.BossTouzokuCustom.BossTouzokuCustomIntroPatches.BlockNoREroModSuperBossSpeedPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.BossTouzokuCustom.BossTouzokuCustomIntroPatches.BlockNoREroModSuperEnemySpeedPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.BossTouzokuCustom.BossTouzokuCustomIntroPatches.BlockNoREroModSuperResteColorPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.BossTouzokuCustom.BossTouzokuCustomCombatPatches.ForceDamageOnHitPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.BossTouzokuCustom.BossTouzokuCustomCombatPatches.ForceMagicDamageOnHitPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.BossTouzokuCustom.BossTouzokuCustomCombatPatches.BlockIntroAnimationPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.BossTouzokuCustom.BossTouzokuCustomCombatPatches.BlockAnimeKindDuringEroPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.BossTouzokuCustom.BossTouzokuCustomCombatPatches.GuardFieldMobStatePatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.BossTouzokuCustom.BossTouzokuCustomCombatPatches.BlockBattleStartPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.BossTouzokuCustom.BossTouzokuCustomCombatPatches.BlockBossEnemyFovPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.BossTouzokuCustom.BossTouzokuCustomCombatPatches.BlockUpdateFovDirectPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.BossTouzokuCustom.BossTouzokuCustomCombatPatches.BlockDeathSlowMoPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.BossTouzokuCustom.BossTouzokuCustomEroStartSetPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.BossTouzokuCustom.BossTouzokuCustomEroAnimePatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.BossTouzokuCustom.BossTouzokuCustomHSceneEscapeStrugglePatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.BossTouzokuCustom.BossTouzokuCustomHSceneEscapeFunNowDamagePatch));
        PatchType(typeof(DoreiPassLogic));
        PatchType(typeof(MutudePassLogic));
        PatchType(typeof(NoREroMod.Systems.GrabSystem.Patches.RangedDamageFlagPatches));
        PatchType(typeof(NoREroMod.Systems.GrabSystem.Patches.MeleeAttackerContextPatches));
        PatchType(typeof(NoREroMod.Systems.Audio.AttackSoundPatch));
        PatchType(typeof(NoREroMod.Systems.Audio.DeathSoundPatch));
        PatchType(typeof(NoREroMod.Systems.GrabSystem.Patches.GrabViaAttackPatch));
        NoREroMod.Patches.Enemy.NoREroModEliteGrabDisablerPatch.Apply(harmony);
        NoREroMod.Systems.Gameplay.VengeanceStrikeNoGrabDuringStabPatch.Apply(harmony);
        NoREroMod.Systems.Rage.Patches.RageActiveImmunityPatch.ApplyCollisionGrabBlock(harmony);
        PatchType(typeof(NoREroMod.Patches.Enemy.WolfModCustom.WolfSkeletonDataAssetPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.RickEnemyModShared.RickEnemyModSkeletonDataAssetPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.RickEnemyModShared.RickEnemyModFatalityIconInstantiatePatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.RickEnemyModShared.RickEnemyModSlaughtererFatalityIconPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.HellishTouzokuModCustom.HellishTouzokuSkeletonDataAssetPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.HellishTouzokuModCustom.HellishTouzokuHSceneEscapeStrugglePatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.HellishTouzokuModCustom.HellishTouzokuHSceneEscapeFunNowDamagePatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.HellishTouzokuModCustom.HellishTouzokuHSceneEscapeGiveUpPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.DoreiModCustom.DoreiSkeletonDataAssetPatch));
        PatchType(typeof(NoREroMod.Patches.Enemy.DoreiModCustom.DoreiSpectatorIdlePatch));
        PatchType(typeof(TimeScaleResetOnEscapePatch));
        PatchType(typeof(PlayerHitBloodCleanupPatch));
        PatchType(typeof(StruggleInvulnPatch));
        PatchType(typeof(StruggleEscapeCombatRecoveryPatch));
        PatchType(typeof(EnemyLibraryEroStatusGuardPatches));
        PatchType(typeof(VanillaKnockdownRecoveryPatch));
        PatchType(typeof(PregnancyBirthGuardPatch.BirthPostfix));
        PatchType(typeof(PregnancyBirthGuardPatch.BirthstartPostfix));
        PatchType(typeof(PregnancyBirthGuardPatch.Birthstart2Postfix));
        PatchType(typeof(PregnancyBirthGuardPatch.EroresetPrefix));
        PatchType(typeof(PregnancyBirthGuardPatch.EroresetPostfix));
        PatchType(typeof(PregnancyBirthGuardPatch.Eroreset2Prefix));
        PatchType(typeof(PregnancyBirthGuardPatch.Eroreset2Postfix));
        PatchType(typeof(BirthRecoveryJigoPatch.BirthMonsterJigoPostfix));
        PatchType(typeof(BirthRecoveryJigoPatch.BirthMonsterSecondJigoPostfix));
        PatchType(typeof(BirthRecoveryStandGuardPatch));
        PatchType(typeof(NoREroMod.Patches.Trap.TrapdataHSceneEscapeFunNowDamagePatch));
        PatchType(typeof(NoREroMod.Patches.Trap.TrapdataHSceneEscapeGiveUpPatch));
        PatchType(typeof(GuardParryMindBrokenPatch));
        PatchType(typeof(NoREroMod.Systems.Gameplay.AirGuardGuardFunPatch));
        PatchType(typeof(NoREroMod.Systems.Gameplay.AirGuardAnimeFunPatch));
        PatchType(typeof(NoREroMod.Systems.Gameplay.EnemyConstantVisibilityPatch));
        PatchType(typeof(PlayerConQTE3RestartPatch));
        PatchType(typeof(PlayerConQTE3GiveUpPatch));
        PatchType(typeof(NoREroMod.Patches.Player.PlayerConUpdateDispatcher));
        PatchTypeWithLog(typeof(NoREroMod.Patches.Player.StrugglePotionPrepareFunNowdamagePatch), "StrugglePotionNorCompat");
        PatchTypeWithLog(typeof(NoREroMod.Patches.Player.StrugglePotionBlockVanillaItemUsePatch), "StrugglePotionBlockItemUse");
        PatchType(typeof(NoREroMod.Systems.Gameplay.VengeanceStrikeStabSoundPatch));
        PatchType(typeof(NoREroMod.Systems.Gameplay.VengeanceStrikeStabPresentationPatch));
        PatchType(typeof(NoREroMod.Systems.Gameplay.VengeanceStrikePlayerUpdatePatch));
        PatchType(typeof(NoREroMod.Systems.Gameplay.VengeanceStrikeHandsPatch));
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
        PatchType(typeof(NoREroMod.Systems.Rage.RageSystem));
        PatchType(typeof(NoREroMod.Systems.Rage.RageUISystem));
        // Use only the universal kill tracker to avoid duplicate kill registration.
        PatchType(typeof(NoREroMod.Systems.Rage.Patches.RageUniversalKillTrackerPatch));
        PatchType(typeof(NoREroMod.Systems.Rage.Patches.RageResetOnGrabDownPatch));
        PatchType(typeof(NoREroMod.Systems.Rage.Patches.RageHitTrackerPatch));
        PatchType(typeof(NoREroMod.Systems.Rage.Patches.RageActiveImmunityPatch));
        PatchType(typeof(NoREroMod.Systems.Gameplay.WeaponAnimations.WitchFineGreatswordPatch));
        PatchType(typeof(NoREroMod.Systems.Gameplay.WeaponAnimations.LightOneHand3HitExtendedComboEquipPatch));
        PatchType(typeof(NoREroMod.Systems.Gameplay.WeaponAnimations.WitchExtendedGroundSwordComboPatch));
        PatchType(typeof(NoREroMod.Systems.Rage.RageComboUISystemPatches));
        PatchType(typeof(NoREroMod.Systems.Effects.HSceneBlackBackgroundTriggerPatch));
        PatchType(typeof(NoREroMod.Patches.Effects.PregnancyClipTrigger));
        PatchType(typeof(NoREroMod.Systems.PlayerRespawn.VengeanceRespawnEffectPatch));
        PatchType(typeof(NoREroMod.Systems.PlayerRespawn.PlayerDeathSoulRestartMenuPatch));
        PatchType(typeof(NoREroMod.Patches.Trap.BlackOozeTrapDiagnosticsPatch));
        PatchType(typeof(NoREroMod.Patches.Trap.TrapHSceneMosaicDisablePatch));
        try
        {
            NoREroMod.Patches.HellTraps.LethalMagicTrapPatches.ApplyPatches(harmony);
            NoREroMod.Patches.HellTraps.LethalCocoonTrapPatches.ApplyPatches(harmony);
            if (enableLethalMagicTrap.Value || enableLethalCocoonTrap.Value)
                harmony.PatchAll(typeof(NoREroMod.Patches.HellTraps.LethalTrapDeathFlagCleanupPatch));
        }
        catch (Exception ex)
        {
            Log?.LogWarning("[LethalMagicTrap] Harmony patch setup failed: " + ex.Message);
        }
        PatchType(typeof(NoREroMod.Systems.CombatAi.Patches.EnemyDateDistanceFunPatch)); // Combat AI: react when player close (HellGateJson/CombatAi)
        PatchType(typeof(NoREroMod.Systems.CombatAi.Patches.EnemyDateOndamageSendPatch)); // Combat AI: react to combo — boost dodge chance (HellGateJson/CombatAi)
        PatchType(typeof(NoREroMod.Systems.CombatAi.Factions.Patches.EnemyDateFactionBootstrapPatch)); // Enemy Factions: isolated bootstrap (HellGateJson/CombatAi/Factions.json)
        PatchType(typeof(NoREroMod.Systems.CombatAi.Factions.Patches.EnemyDateFactionDistancePatch)); // Enemy Factions: retarget to hostile faction
        PatchType(typeof(NoREroMod.Systems.CombatAi.Factions.Patches.EnemyDateFactionUpdateSustainPatch)); // Enemy Factions: sustain approach after vanilla idle gate
        PatchType(typeof(NoREroMod.Systems.CombatAi.Factions.Patches.EnemyDateFactionVisionOverridePatch)); // Enemy Factions: relation-based player vision override
        PatchType(typeof(NoREroMod.Systems.CombatAi.Factions.Patches.EnemyDateFactionFovCompatPatch)); // Enemy Factions: keep NoREroMod FOV alpha off faked distance
        PatchType(typeof(NoREroMod.Systems.CombatAi.Factions.Patches.EnemyDateFactionIgnorePlayerDamageColPatch)); // Enemy Factions: bandits ignore player damage collider
        PatchType(typeof(NoREroMod.Systems.CombatAi.Factions.Patches.EnemyDateFactionPlayerProvocationPatch)); // Enemy Factions: become hostile to player after provocation
        PatchType(typeof(NoREroMod.Systems.CombatAi.Factions.Patches.PlayerAvoidedAttackTriggerImprovedPatch)); // Enemy Factions: trigger deescalation roll on dash-avoid
        PatchType(typeof(NoREroMod.Systems.CombatAi.Factions.Patches.PlayerAvoidedAttackTriggerLegacyPatch)); // Enemy Factions: trigger deescalation roll on legacy damage path
        PatchType(typeof(NoREroMod.Systems.CombatAi.Factions.Patches.FactionArrowOwnerPatch)); // Enemy Factions: tag Arrow projectiles with attacker EnemyDate
        PatchType(typeof(NoREroMod.Systems.CombatAi.Factions.Patches.FactionArrowHitPatch)); // Enemy Factions: Arrow damages hostile enemy hurtboxes
        PatchType(typeof(NoREroMod.Systems.CombatAi.Factions.Patches.FactionFallBulletOwnerPatch)); // Enemy Factions: tag fallBullet with attacker EnemyDate
        PatchType(typeof(NoREroMod.Systems.CombatAi.Factions.Patches.FactionFallBulletHitPatch)); // Enemy Factions: fallBullet damages hostile enemy hurtboxes
        PatchType(typeof(NoREroMod.Systems.CombatAi.Factions.Patches.FactionLightMagicOwnerPatch)); // Enemy Factions: tag LightMagic (Sister/CrawlingSister bolt) with attacker EnemyDate
        PatchType(typeof(NoREroMod.Systems.CombatAi.Factions.Patches.FactionLightMagicHitPatch)); // Enemy Factions: LightMagic damages hostile enemy hurtboxes
        PatchType(typeof(NoREroMod.Systems.CombatAi.Factions.Patches.EnemyDateFactionColorBootstrapPatch)); // Enemy Factions: apply faction marker visuals
        PatchType(typeof(NoREroMod.Systems.CombatAi.Factions.Patches.EnemyDateFactionColorAnimePatch)); // Enemy Factions: keep marker after animation state updates
        PatchType(typeof(NoREroMod.Systems.CombatAi.Factions.Patches.EnemyDateFactionColorResetPatch)); // Enemy Factions: keep marker after reset
        PatchType(typeof(NoREroMod.Systems.CombatAi.Factions.Patches.EnemyDateFactionCleanupPatch)); // Enemy Factions: runtime cleanup
        PatchType(typeof(NoREroMod.Systems.CombatAi.Factions.Patches.FactionReputationHudBootstrapPatch)); // Enemy Factions: create player reputation HUD on UI start
        PatchType(typeof(NoREroMod.Systems.CombatAi.Factions.Patches.FactionReputationHudBadstatusBootstrapPatch)); // Enemy Factions: keep reputation HUD across scenes
        PatchType(typeof(NoREroMod.Systems.CombatAi.Factions.Patches.PlayerFactionReputationSaveHookPatch)); // Enemy Factions: flush reputation JSON on save
        PatchType(typeof(NoREroMod.Systems.CombatAi.Factions.Patches.PlayerFactionReputationLoadHookPatch)); // Enemy Factions: reload reputation JSON on load
        PatchType(typeof(NoREroMod.Systems.EventCore.Core.EventCorePlayerAttackInputBlockPatch)); // EventCore: suppress left-click attack input during modal freeze
        PatchType(typeof(NoREroMod.Patches.Player.VanillaStoryEventCombatStatePrefixPatch));
        PatchType(typeof(NoREroMod.Patches.Player.VanillaStoryEventCombatInputBlockPatch));
        PatchType(typeof(NoREroMod.Patches.Player.VanillaStoryEventAtkFunBlockPatch));
        PatchType(typeof(NoREroMod.Patches.Player.VanillaStoryEventAirAtkFunBlockPatch));
        PatchType(typeof(NoREroMod.Patches.Player.VanillaStoryEventChargeAtkBlockPatch));
        PatchType(typeof(NoREroMod.Systems.Rage.Patches.RageMindBrokenSaveHookPatch)); // Rage + MindBroken JSON on save
        PatchType(typeof(NoREroMod.Systems.Rage.Patches.RageMindBrokenLoadHookPatch)); // Rage + MindBroken JSON on load

        // Tentacle H-scene diagnostics (off by default; gated by JSON Enable inside each postfix).
        PatchType(typeof(NoREroMod.Systems.Diagnostics.Tentacle.TentacleDiagnosticsLifecyclePatches));
        // Trap player-body / camera diagnostics (off by default; JSON Enable).
        PatchType(typeof(NoREroMod.Systems.Diagnostics.TrapBody.TrapPlayerBodyLifecyclePatches));
        // Kinoko / MushroomERO OnEvent diagnostics (JSON Enable).
        PatchType(typeof(NoREroMod.Systems.Diagnostics.Kinoko.KinokoMushroomEroLifecyclePatches));

        // Economic / Gold module (HellGateJson/Economic/Economy.json + GoldDropTable.json)
        if (NoREroMod.Systems.Economy.EconomicConfig.Enable) {
            PatchType(typeof(NoREroMod.Systems.Economy.Patches.EnemyDeathGoldDropPatch));   // gold drop on enemy death
            PatchType(typeof(NoREroMod.Systems.Economy.Patches.PlayerDeathGoldDropPatch));  // souls-style on player death
            PatchType(typeof(NoREroMod.Systems.Economy.Patches.PlayerCombatGoldLossLegacyPatch));
            PatchType(typeof(NoREroMod.Systems.Economy.Patches.PlayerCombatGoldLossImprovedPatch));
            PatchType(typeof(NoREroMod.Systems.Economy.Patches.PlayerRespawnGoldArmPatch)); // re-arm death idempotency on respawn
            PatchType(typeof(NoREroMod.Systems.Economy.Patches.GoldWalletSaveHookPatch));   // flush wallet JSON on save
            PatchType(typeof(NoREroMod.Systems.Economy.Patches.GoldWalletLoadHookPatch));   // reload wallet JSON on load
            PatchType(typeof(NoREroMod.Systems.Economy.Patches.GoldHudBootstrapPatch));     // create HUD on UImng.Start
            PatchType(typeof(NoREroMod.Systems.Economy.Patches.GoldHudBadstatusBootstrapPatch)); // re-create HUD on bad-status canvas reload
        }
        // Pregnancy module (Phase 1, Milestone 1): milliliter womb meter on EnemyDate.Nakadasi + HUD.
        if (NoREroMod.Systems.Pregnancy.PregnancyConfig.Enable != null &&
            NoREroMod.Systems.Pregnancy.PregnancyConfig.Enable.Value) {
            try {
                PatchType(typeof(NoREroMod.Systems.Pregnancy.Patches.PregnancyPartnerTrackerPatch));
                Log?.LogInfo("[Pregnancy] Patched EnemyDate.Nakadasi (tracker)");
            } catch (Exception ex) { Log?.LogWarning($"[Pregnancy] Tracker patch failed: {ex.Message}"); }

            try {
                PatchType(typeof(NoREroMod.Systems.Pregnancy.Patches.EroTouzokuAxePregnancyPatch));
                Log?.LogInfo("[Pregnancy] Patched EroTouzokuAXE.OnEvent");
            } catch (Exception ex) { Log?.LogWarning($"[Pregnancy] EroTouzokuAXE patch failed: {ex.Message}"); }
            try {
                PatchType(typeof(NoREroMod.Systems.Pregnancy.Patches.MushroomEroPregnancyPatch));
                Log?.LogInfo("[Pregnancy] Patched MushroomERO.OnEvent (Kinoko FIN Nakadasi recover)");
            } catch (Exception ex) { Log?.LogWarning($"[Pregnancy] MushroomERO pregnancy patch failed: {ex.Message}"); }
            try {
                PatchType(typeof(NoREroMod.Systems.Pregnancy.Patches.WhiteFadeInNullSafePatch));
                Log?.LogInfo("[Pregnancy] Patched UImngPatch.WhiteFadeIn (null-safe under black BG)");
            } catch (Exception ex) { Log?.LogWarning($"[Pregnancy] WhiteFadeIn null-safe patch failed: {ex.Message}"); }

            try {
                PatchType(typeof(NoREroMod.Systems.Pregnancy.Patches.WombMeterHudBootstrapPatch));
                Log?.LogInfo("[Pregnancy] Patched WombMeterHudBootstrap");
            } catch (Exception ex) { Log?.LogWarning($"[Pregnancy] HUD bootstrap patch failed: {ex.Message}"); }

            try {
                PatchType(typeof(NoREroMod.Systems.Pregnancy.Patches.WombMeterHudBadstatusBootstrapPatch));
                Log?.LogInfo("[Pregnancy] Patched WombMeterHudBadstatusBootstrap");
            } catch (Exception ex) { Log?.LogWarning($"[Pregnancy] HUD badstatus patch failed: {ex.Message}"); }

            try {
                PatchType(typeof(NoREroMod.Systems.Pregnancy.Patches.SuppressVanillaCreampieValUiPatch));
                Log?.LogInfo("[Pregnancy] Patched SuppressVanillaCreampieValUi");
            } catch (Exception ex) { Log?.LogWarning($"[Pregnancy] Suppress creampie UI patch failed: {ex.Message}"); }

            try {
                PatchType(typeof(NoREroMod.Systems.Pregnancy.Patches.SuppressVanillaCreampieTimePatch));
                Log?.LogInfo("[Pregnancy] Patched SuppressVanillaCreampieTime");
            } catch (Exception ex) { Log?.LogWarning($"[Pregnancy] Suppress creampie time patch failed: {ex.Message}"); }

            try {
                PatchType(typeof(NoREroMod.Systems.Pregnancy.Patches.SuppressVanillaPregnancyTimePatch));
                Log?.LogInfo("[Pregnancy] Patched SuppressVanillaPregnancyTime (custom trimester timer)");
            } catch (Exception ex) { Log?.LogWarning($"[Pregnancy] Suppress pregnancy time patch failed: {ex.Message}"); }

            try {
                PatchType(typeof(NoREroMod.Systems.Pregnancy.Patches.TrimesterPhysicsPatch.BlockDashInThirdTrimesterPatch));
                PatchType(typeof(NoREroMod.Systems.Pregnancy.Patches.TrimesterPhysicsPatch.ThirdTrimesterJumpHeightPatch));
                PatchType(typeof(NoREroMod.Systems.Pregnancy.Patches.TrimesterPhysicsPatch.TrimesterMoveSpeedPatch));
                Log?.LogInfo("[Pregnancy] Patched trimester physics (dash / jump / movespeed)");
            } catch (Exception ex) { Log?.LogWarning($"[Pregnancy] Trimester physics patch failed: {ex.Message}"); }

            try {
                PatchType(typeof(NoREroMod.Systems.Pregnancy.Patches.FactionModifierPatches.PlayerStatusAllStrPatch));
                PatchType(typeof(NoREroMod.Systems.Pregnancy.Patches.FactionModifierPatches.PlayerStatusAllIntPatch));
                PatchType(typeof(NoREroMod.Systems.Pregnancy.Patches.FactionModifierPatches.PlayerStatusAllDexPatch));
                PatchType(typeof(NoREroMod.Systems.Pregnancy.Patches.FactionModifierPatches.PlayerStatusAllLuckPatch));
                PatchType(typeof(NoREroMod.Systems.Pregnancy.Patches.FactionModifierPatches.PlayerStatusAllToughPatch));
                Log?.LogInfo("[Pregnancy] Patched bloodline + trimester stat modifiers (STR/INT/DEX/LUCK/STA)");
            } catch (Exception ex) { Log?.LogWarning($"[Pregnancy] Faction stat modifier patch failed: {ex.Message}"); }

            try {
                PatchType(typeof(NoREroMod.Systems.Pregnancy.SemenValueMultiplier.EnemyDateNakadasiMultiplierPatch));
                PatchType(typeof(NoREroMod.Systems.Pregnancy.SemenValueMultiplier.TrapdataNakadasiMultiplierPatch));
                Log?.LogInfo("[Pregnancy] Patched Nakadasi semen value multiplier");
            } catch (Exception ex) { Log?.LogWarning($"[Pregnancy] Semen value multiplier patch failed: {ex.Message}"); }

            try {
                PatchType(typeof(NoREroMod.Systems.Pregnancy.Patches.PregnancySaveHookPatch));
                Log?.LogInfo("[Pregnancy] Patched save hook (hideout persistence)");
            } catch (Exception ex) { Log?.LogWarning($"[Pregnancy] Save hook patch failed: {ex.Message}"); }

            try {
                PatchType(typeof(NoREroMod.Systems.Pregnancy.Patches.PregnancyLoadHookPatch));
                Log?.LogInfo("[Pregnancy] Patched load hook (hideout persistence)");
            } catch (Exception ex) { Log?.LogWarning($"[Pregnancy] Load hook patch failed: {ex.Message}"); }

            try {
                PatchType(typeof(NoREroMod.Systems.Pregnancy.PregnancyAltarCleanupPatch));
                Log?.LogInfo("[Pregnancy] Patched altar reset (womb / gestation cleanup)");
            } catch (Exception ex) { Log?.LogWarning($"[Pregnancy] Altar cleanup patch failed: {ex.Message}"); }

            try {
                NoREroMod.Systems.Pregnancy.ShelterAttack.ShelterAttackHooks.Initialize();
                PatchType(typeof(NoREroMod.Systems.Pregnancy.ShelterAttack.ShelterAttackHooks.RefreshAfterAltarPostfix));
                PatchType(typeof(NoREroMod.Systems.Pregnancy.ShelterAttack.ShelterAttackHooks.NotifyCrossZoneWalkTransitionPostfix));
                PatchType(typeof(NoREroMod.Systems.Pregnancy.ShelterAttack.ShelterAttackHooks.SavepointMenuFastTravelPrefix));
                Log?.LogInfo("[Pregnancy] Patched shelter attack hooks (scene load + altar reset + walk + fast-travel prefix)");
            } catch (Exception ex) { Log?.LogWarning($"[Pregnancy] Shelter attack hook failed: {ex.Message}"); }

            try {
                // FIRST: Unpatch NoREroMod birth patches to prevent it from replacing the slime with the father.
                UnpatchNoREroModBirthPatches(harmony);

                PatchType(typeof(NoREroMod.Systems.Pregnancy.Patches.BirthSpawnOverridePatch));
                Log?.LogInfo("[Pregnancy] Patched BadstatusBirthMonster.OnEvent (birth coordinator)");

                PatchType(typeof(NoREroMod.Systems.Pregnancy.Patches.BirthSlimeCapturePatch));
                Log?.LogInfo("[Pregnancy] Patched suraimu.Start (slime capture)");

                PatchType(typeof(NoREroMod.Systems.Pregnancy.Patches.WitchOffspringFriendlyFirePatch));
                PatchType(typeof(NoREroMod.Systems.Pregnancy.Patches.WitchOffspringFriendlyFirePatch.BlockOffspringPlayerDamageTriggerPatch));
                PatchType(typeof(NoREroMod.Systems.Pregnancy.Patches.WitchOffspringFriendlyFirePatch.BlockOffspringSlashDamageTriggerPatch));
                PatchType(typeof(NoREroMod.Systems.Pregnancy.Patches.WitchOffspringFriendlyFirePatch.BlockOffspringImpactDamageTriggerPatch));
                PatchType(typeof(NoREroMod.Systems.Pregnancy.Patches.WitchOffspringFriendlyFirePatch.BlockOffspringFunDamagePatch));
                PatchType(typeof(NoREroMod.Systems.Pregnancy.Patches.WitchOffspringFriendlyFirePatch.BlockOffspringFunDamageImprovementPatch));
                PatchType(typeof(NoREroMod.Systems.Pregnancy.Patches.WitchOffspringFriendlyFirePatch.BlockOffspringOndamageSendPatch));
                PatchType(typeof(NoREroMod.Systems.Pregnancy.Patches.WitchOffspringFriendlyFirePatch.BlockOffspringMafiaMuscleDownedGrabPatch));
                PatchType(typeof(NoREroMod.Systems.Pregnancy.Patches.WitchOffspringFriendlyFirePatch.BlockPlayerMagicHitOffspringPatch));
                NoREroMod.Systems.Pregnancy.Patches.WitchOffspringFriendlyFirePatch.Apply(harmony);
                Log?.LogInfo("[Pregnancy] Patched offspring friendly-fire protection");
            } catch (Exception ex) { Log?.LogWarning($"[Pregnancy] Birth spawn patch failed: {ex.Message}"); }
        } else {
            Log?.LogInfo($"[Pregnancy] Module disabled or config null (Enable={NoREroMod.Systems.Pregnancy.PregnancyConfig.Enable?.Value})");
        }

        ApplyDoreiCombatAiPatch(harmony); // Combat AI: Dorei — no flee, prefer melee (CombatAi.json: DoreiEnable, DoreiDisableFlee)
        NoREroMod.Systems.Audio.AttackSoundSystem.Initialize(this);
        NoREroMod.Systems.Gameplay.VengeanceStrikeContent.Initialize(this);
        
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
        try
        {
            harmony.PatchAll(type);
            // Nested [HarmonyPatch] types are not always picked up by PatchAll(outer) alone.
            Type[] nested = type.GetNestedTypes(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            for (int i = 0; i < nested.Length; i++)
            {
                try { harmony.PatchAll(nested[i]); }
                catch { }
            }
        }
        catch { }
    }

    private void PatchTypeWithLog(Type type, string label)
    {
        try
        {
            harmony.PatchAll(type);
            Log?.LogInfo($"[PATCH] {label} applied.");
        }
        catch (Exception ex)
        {
            Log?.LogError($"[PATCH] {label} FAILED: {ex.Message}");
        }
    }

    private void UnpatchNoREroModBirthPatches(Harmony harmony)
    {
        try
        {
            var methodsToUnpatch = new MethodBase[]
            {
                AccessTools.Method(typeof(BadstatusBirthMonster), "OnEvent"),
                AccessTools.Method(typeof(BadstatusBirthMonstersecond), "OnEvent"),
                AccessTools.Method(typeof(MonsterChild), "FixedUpdate")
            };

            foreach (var method in methodsToUnpatch)
            {
                if (method == null) continue;

                var patches = Harmony.GetPatchInfo(method);
                if (patches == null) continue;

                foreach (var prefix in patches.Prefixes)
                {
                    if (IsNoREroModBirthPatch(prefix))
                    {
                        harmony.Unpatch(method, prefix.PatchMethod);
                        Log?.LogInfo($"[Pregnancy] Unpatched NoREroMod birth prefix: {prefix.PatchMethod.DeclaringType?.Name}.{prefix.PatchMethod.Name}");
                    }
                }

                foreach (var postfix in patches.Postfixes)
                {
                    if (IsNoREroModBirthPatch(postfix))
                    {
                        harmony.Unpatch(method, postfix.PatchMethod);
                        Log?.LogInfo($"[Pregnancy] Unpatched NoREroMod birth postfix: {postfix.PatchMethod.DeclaringType?.Name}.{postfix.PatchMethod.Name}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log?.LogWarning($"[Pregnancy] Failed to unpatch NoREroMod birth patches: {ex.Message}");
        }
    }

    private static bool IsNoREroModBirthPatch(HarmonyLib.Patch patch)
    {
        if (patch == null) return false;
        var asmName = patch.PatchMethod.DeclaringType?.Assembly.GetName().Name;
        if (string.IsNullOrEmpty(asmName)) return false;

        // The original NoREroMod plugin assembly is named NoREroMod (or nor-ero-mod-rebalance in some forks).
        // The HellGate overlay lives in a different assembly, so we must not match it here.
        if (asmName.Equals("NoREroMod", StringComparison.OrdinalIgnoreCase)) return true;
        if (asmName.StartsWith("nor-ero-mod", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
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
        // Enemy/boss HP-speed-poise scaling lives in NoREroMod.dll → edit BepInEx/config/NoREroMod.cfg ([Enemies], [Elites], [Bosses]).

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
            0.5f,
            "Current SP is multiplied by this value after penetration, player orgasm, or creampies. 1 = no loss, 0.5 = lose half, 0 = full reset (0-1)"
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
        grabBlockImmunity = Config.Bind("GrabSystemNG", "GrabBlockImmunity", true, "When true, guarding fully blocks grab-via-attack. When false, use GrabChanceThroughBlock / GrabChancePowerThroughBlock.");
        grabChanceMelee = Config.Bind("GrabSystemNG", "GrabChanceMelee", 0.10f, "Base chance of grab from normal melee attack when NOT blocking (0.10 = 10%). Affected by MindBroken (+), low HP (+), Pleasure (+) and Rage (-) only when base chance > 0.");
        grabChancePowerAttack = Config.Bind("GrabSystemNG", "GrabChancePowerAttack", 0.15f, "Base chance of grab from knockdown/power attack when NOT blocking (0.15 = 15%). Affected by MindBroken (+), low HP (+), Pleasure (+) and Rage (-) only when base chance > 0.");
        grabChanceThroughBlock = Config.Bind("GrabSystemNG", "GrabChanceThroughBlock", 0.05f, "When GrabBlockImmunity is false: chance normal melee grabs through block (0.05 = 5%). Modifiers apply only when this base chance > 0.");
        grabChancePowerThroughBlock = Config.Bind("GrabSystemNG", "GrabChancePowerThroughBlock", 0.10f, "When GrabBlockImmunity is false: chance knockdown attack grabs through block (0.10 = 10%). Modifiers apply only when this base chance > 0.");
        grabChanceMindBrokenBonusPer10Percent = Config.Bind("GrabSystemNG", "GrabChanceMindBrokenBonusPer10Percent", 0.02f, "Extra grab chance per 10% MindBroken in grab logic (0.02 = +2% per 10%). UI can use a different value.");
        grabChanceRageReductionPerPercent = Config.Bind("GrabSystemNG", "GrabChanceRageReductionPerPercent", 0.005f, "Grab chance reduction per 1% Rage (0.005 = 0.5% per 1% Rage). At 100% Rage grab chance is halved.");
        grabChancePleasureBonusMax = Config.Bind("GrabSystemNG", "GrabChancePleasureBonusMax", 0.20f, "Maximum additional grab chance from Pleasure gauge (BadstatusVal[0]). 0.20 = +20% at 100 pleasure, scaled linearly.");
        grabViaAttackSlowmo = Config.Bind("GrabSystemNG", "GrabViaAttackSlowmo", true, "Slow down time when grab via attack triggers (runs immediately, HScene zoom has no slowmo)");
        grabViaAttackSlowmoTimeScale = Config.Bind("GrabSystemNG", "GrabViaAttackSlowmoTimeScale", 0.3f, "Time scale during grab (0.3 = 30% speed, 2 sec)");
        grabViaAttackSlowmoDuration = Config.Bind("GrabSystemNG", "GrabViaAttackSlowmoDuration", 2f, "Duration of grab slowmo in real seconds");

        enableVengeanceStrikeAssets = Config.Bind(
            "VengeanceStrike",
            "EnableAssets",
            true,
            "Load optional strike presentation assets from [Game]/sources/HellGate_sources/VengeanceStrike/ (portable path, same as other HellGate_sources content)."
        );
        vengeanceStrikeSoundFile = Config.Bind(
            "VengeanceStrike",
            "SoundFile",
            "fatality.wav",
            "WAV filename inside the VengeanceStrike folder (empty = skip loading)."
        );
        enableVengeanceStrikePlayOnStab = Config.Bind(
            "VengeanceStrike",
            "PlayOnStab",
            true,
            "When true and WAV loaded, play once at the start of Stab_fun (parry follow-up stab)."
        );
        enableVengeanceStrikeHandGlow = Config.Bind(
            "VengeanceStrike",
            "HandGlowLikeRage",
            true,
            "Fire particles on hands during parry stab (bone3 = left color, bone8 = right color; see Hands* settings below)."
        );
        vengeanceStrikeHandsParticleSizeMult = Config.Bind(
            "VengeanceStrike",
            "HandsParticleSizeMult",
            7f,
            "Multiplier for particle size during Vengeance hands (1 = same base as Rage fire). Default 7 ≈ 4× prior 1.75."
        );
        vengeanceStrikeHandsEmitterAreaMult = Config.Bind(
            "VengeanceStrike",
            "HandsEmitterAreaMult",
            12f,
            "Multiplies spawn circle radius (base 0.06 world units). Bigger = fire fills a larger area around the hand bone; try 8–20."
        );
        vengeanceStrikeHandsEmissionMult = Config.Bind(
            "VengeanceStrike",
            "HandsEmissionMult",
            2.25f,
            "Multiplier for particles/sec during Vengeance hands."
        );
        vengeanceStrikeHandsMaxParticles = Config.Bind(
            "VengeanceStrike",
            "HandsMaxParticles",
            48,
            "Max simultaneous particles per hand during Vengeance (higher = denser fire)."
        );
        vengeanceStrikeHandsParticleLifetimeMin = Config.Bind(
            "VengeanceStrike",
            "HandsParticleLifetimeMin",
            0.14f,
            "Vengeance hand particles: min lifetime (seconds). Shorter = briefer trails."
        );
        vengeanceStrikeHandsParticleLifetimeMax = Config.Bind(
            "VengeanceStrike",
            "HandsParticleLifetimeMax",
            0.36f,
            "Vengeance hand particles: max lifetime (seconds). If max < min, values are swapped."
        );
        vengeanceStrikeHandsParticleSpeedMin = Config.Bind(
            "VengeanceStrike",
            "HandsParticleSpeedMin",
            0.22f,
            "Vengeance hand particles: min outward speed (lower = shorter reach)."
        );
        vengeanceStrikeHandsParticleSpeedMax = Config.Bind(
            "VengeanceStrike",
            "HandsParticleSpeedMax",
            0.62f,
            "Vengeance hand particles: max outward speed. If max < min, values are swapped."
        );
        vengeanceStrikeHandsLeftColorR = Config.Bind("VengeanceStrike", "HandsLeftColorR", 1f, "bone3 hand red 0–1 (default same red fire as right).");
        vengeanceStrikeHandsLeftColorG = Config.Bind("VengeanceStrike", "HandsLeftColorG", 0.15f, "bone3 hand green 0–1.");
        vengeanceStrikeHandsLeftColorB = Config.Bind("VengeanceStrike", "HandsLeftColorB", 0.12f, "bone3 hand blue 0–1.");
        vengeanceStrikeHandsRightColorR = Config.Bind("VengeanceStrike", "HandsRightColorR", 1f, "bone8 hand red 0–1 (default red fire).");
        vengeanceStrikeHandsRightColorG = Config.Bind("VengeanceStrike", "HandsRightColorG", 0.15f, "bone8 hand green 0–1.");
        vengeanceStrikeHandsRightColorB = Config.Bind("VengeanceStrike", "HandsRightColorB", 0.12f, "bone8 hand blue 0–1.");
        vengeanceStrikeHandsCoreEnable = Config.Bind(
            "VengeanceStrike",
            "HandsCoreEnable",
            true,
            "Visible additive orb (nucleus) under the hand particle cloud; tinted HandsLeft/Right colors."
        );
        vengeanceStrikeHandsCoreScaleMult = Config.Bind(
            "VengeanceStrike",
            "HandsCoreScaleMult",
            1f,
            "Multiplier for orb diameter vs emitter radius (HandsEmitterAreaMult). 1 ≈ ~1.8× spawn circle radius."
        );
        enableVengeanceStrikeSlowMo = Config.Bind(
            "VengeanceStrike",
            "SlowMoDuringStab",
            true,
            "On parry stab start: apply slow-mo for SlowMoDurationSeconds (real time), then restore. New stabs during that window do not extend it."
        );
        vengeanceStrikeSlowMoTimeScale = Config.Bind(
            "VengeanceStrike",
            "SlowMoTimeScale",
            0.1f,
            "World Time.timeScale during Vengeance window (clamped 0.01–1). Values below 0.01 are raised to 0.01: true 0 freezes Spine animation (no deltaTime) and can softlock the stab combo. 0.1 = strong slow-mo, 1 = no change."
        );
        vengeanceStrikeSlowMoDurationSeconds = Config.Bind(
            "VengeanceStrike",
            "SlowMoDurationSeconds",
            2f,
            "How long slow-mo lasts in real seconds (not tied to stab animation length). Another stab starting during this time does not get a new slow-mo window."
        );
        enableVengeanceStrikeSpineBoost = Config.Bind(
            "VengeanceStrike",
            "SpineBoostDuringStab",
            true,
            "Multiply player Spine timeScale during stab so the strike anim stays snappy while the world is slowed."
        );
        vengeanceStrikeSpineMultiplier = Config.Bind(
            "VengeanceStrike",
            "SpineMultiplier",
            2f,
            "Multiplier on SkeletonAnimation.timeScale during stab (after vanilla Update). Default 2."
        );
        vengeanceStrikeSpineCompensateSlowMo = Config.Bind(
            "VengeanceStrike",
            "SpineCompensateTimeScale",
            false,
            "When true, multiply further by 1/Time.timeScale while world is slowed. Default false (calmer anim than compensated mode)."
        );
        enableVengeanceStrikeBlockGrabDuringStab = Config.Bind(
            "VengeanceStrike",
            "BlockGrabDuringStab",
            true,
            "While parry stab (Vengeance) is active (_stabnow), enemy cannot grab Aradia (collision elite grab + grab-via-attack)."
        );
        enableVengeanceStrikeRageCost = Config.Bind(
            "VengeanceStrike",
            "RageCostEnable",
            true,
            "Require Rage (see RageCostPercent) to perform parry follow-up stab (Stab_fun). If Rage mode is off, cost is skipped."
        );
        vengeanceStrikeRageCostPercent = Config.Bind(
            "VengeanceStrike",
            "RageCostPercent",
            15f,
            "Rage consumed when Vengeance stab executes (0–100). If current Rage is below this, Stab_fun is blocked."
        );
        enableAirGuard = Config.Bind("AirGuard", "Enable", true, "Block (Guard) while airborne.");
        enableHitBloodParticleCleanup = Config.Bind(
            "PlayerVisualFixes",
            "EnableHitBloodParticleCleanup",
            true,
            "After player takes damage, clear lingering vanilla blood particles (Blood7 / playercon.blood) that otherwise follow the player when HellGate is loaded."
        );
        hitBloodParticleCleanupDelaySeconds = Config.Bind(
            "PlayerVisualFixes",
            "HitBloodParticleCleanupDelaySeconds",
            1.25f,
            "Real-time seconds after a hit before blood sub-emitters (Head/Right/Main/Left) are stopped and cleared."
        );
        witchGreatswordDuplicateLastTwoRounds = Config.Bind(
            "WeaponAnimations",
            "WitchGreatsword.DuplicateLastTwoRounds",
            0,
            "Append duplicate ground strike pairs (WeaponKind 1 / wp_bigwitch). 0 = auto until AtkMotion has 9 rows; 1-16 = fixed rounds. Re-equip after change."
        );
        witchExtendedGroundComboRequiresRage = Config.Bind(
            "WeaponAnimations",
            "WitchExtendedGroundComboRequiresRage",
            true,
            "Require Rage (IsActive) for extended ground hits 5-8. False allows full list without Rage (vanilla atk_fun covers 0-4 only)."
        );

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
            false,
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
            false,
            "When enabled, enemies behind or too far away from the player fade out (NoREroMod FoV). Default off — all enemies stay fully visible."
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
        fontFileWestern = Config.Bind(
            "Fonts",
            "FontFileWestern",
            "",
            "Legacy/reserved. External font files are not loaded by Unity 5.6 at runtime; leave empty and use FontFamilyWestern."
        );
        fontFamilyWestern = Config.Bind(
            "Fonts",
            "FontFamilyWestern",
            "",
            "Windows-installed font family for En/Ru/De/Pt/Br/Es/Fr. Recommended: Georgia, Constantia, Cambria, Segoe UI. Empty = automatic fallback."
        );
        fontFileAsian = Config.Bind(
            "Fonts",
            "FontFileAsian",
            "",
            "Legacy/reserved. External font files are not loaded by Unity 5.6 at runtime; leave empty and use FontFamilyAsian or automatic fallbacks."
        );
        fontFamilyAsian = Config.Bind(
            "Fonts",
            "FontFamilyAsian",
            "",
            "Windows-installed font family override for Cn/Jp/Kr. Empty = automatic per-language fallback: Cn=Microsoft YaHei, Jp=Yu Gothic, Kr=Malgun Gothic."
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
            "0.9176,0.8902,0.8235,1.0",
            "Text color for Aradia thoughts — dusty white #EAE3D2 (R, G, B, A values 0-1)"
        );
        aradiaThoughtOutlineColor = Config.Bind(
            "DialogueFonts",
            "AradiaThoughtOutlineColor",
            "0.0,0.0,0.0,1.0",
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

        rickEnemyModAssetsPath = Config.Bind(
            "RickEnemyMod",
            "AssetsPath",
            "",
            "Path to RickEnemyMod folder (relative to game root). Empty = sources/HellGate_sources/RickEnemyMod. Shared Fatality Logo: Fatality Logo/FatalityDeath.png. Per-enemy fatality folders: Butcher/, etc."
        );

        butcherModAssetsPath = Config.Bind(
            "ButcherMod",
            "AssetsPath",
            "",
            "Deprecated — use [RickEnemyMod] AssetsPath. Kept for backward compatibility when RickEnemyMod path is empty."
        );

        hellishTouzokuAssetsPath = Config.Bind(
            "HellishTouzoku",
            "AssetsPath",
            "",
            "Path to Hellish Touzoku Spine folder (relative to game root). Empty = use default: sources/HellGate_sources/Hellish Touzoku Spine. Subfolders: HelllishTouzokuBoSS, HelllishTouzokuAxe, HelllishTouzokuSword."
        );

        hellishTouzokuScaleMultiplier = Config.Bind(
            "HellishTouzoku",
            "SpawnScaleMultiplier",
            0.8f,
            "Visual scale multiplier applied to Hellish Touzoku on spawn (Boss / Axe / Sword). 1.0 = prefab size, 0.8 = 80%."
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

        enablePortraitMod = Config.Bind(
            "PortraitMod",
            "Enable",
            true,
            "When true, replaces the vanilla UIface Spine portrait with looping PNGs from Portrait_mod (Normal, NakedNormal, Sex, Rage, NakedRage, Brainwash). When false, vanilla Spine is restored."
        );
        portraitModAssetsPath = Config.Bind(
            "PortraitMod",
            "AssetsPath",
            "",
            "Root folder for portrait assets, relative to the game install. Leave empty to use sources/HellGate_sources/Portrait_mod."
        );
        portraitModFrameSeconds = Config.Bind(
            "PortraitMod",
            "SecondsPerFrame",
            1f / 15f,
            "Display duration per frame in the PNG cycle (seconds). Default 1/15 s (~15 FPS); lower values advance frames faster."
        );
        portraitModBrainwashThreshold = Config.Bind(
            "PortraitMod",
            "BrainwashMindBrokenFraction",
            0.5f,
            "Minimum MindBroken normalized value [0,1] to select the Brainwash asset folder."
        );
        enableLethalMagicTrap = Config.Bind(
            "HellTraps",
            "EnableLethalMagicTrap",
            true,
            "Enable lethal magic trap spawn key 'lethal_magictrap' (legacy alias: letal_magictrap; 100x bullet damage by default) and custom PNG death clip on kill.");
        lethalMagicTrapDamageMultiplier = Config.Bind(
            "HellTraps",
            "LethalMagicTrapDamageMultiplier",
            100f,
            "Damage multiplier vs vanilla SetupFireball enmATK (vanilla ~70). Lethal default: 100 (= ~7000 per hit).");
        lethalMagicTrapDeathClipPath = Config.Bind(
            "HellTraps",
            "DeathClipAssetsPath",
            "",
            "Folder with numbered PNG frames (1.png..15.png), relative to game root. Empty = sources/HellGate_sources/CustomDeath/Exp_Death.");
        lethalMagicTrapDeathClipDisplayScale = Config.Bind(
            "HellTraps",
            "LethalMagicTrapDeathClipDisplayScale",
            1f,
            "Uniform world scale for lethal magic trap death PNG overlay (1 = native size at 100 pixels per unit; Exp_Death default frames are 1400x835 px).");
        lethalMagicTrapActTimeMultiplier = Config.Bind(
            "HellTraps",
            "LethalMagicTrapActTimeMultiplier",
            1f,
            "Delay before lethal trap fires (multiplier on vanilla acttime ~1.2s). Lower = faster shot, higher = longer warning icon.");
        lethalMagicTrapBulletSpeedMultiplier = Config.Bind(
            "HellTraps",
            "LethalMagicTrapBulletSpeedMultiplier",
            1f,
            "SetupFireball/Fireball Xspd/Yspd/startYspd multiplier for lethal_magictrap bullets.");
        lethalMagicTrapSpawnScale = Config.Bind(
            "HellTraps",
            "LethalMagicTrapSpawnScale",
            1f,
            "Uniform scale on spawned lethal trap instance (trigger collider + visuals). Use for wider/narrower activation area.");
        enableLethalCocoonTrap = Config.Bind(
            "HellTraps",
            "EnableLethalCocoonTrap",
            true,
            "Enable lethal cocoon trap spawn key 'lethal_cocoontrap' (alias: Lethal_cocoontrap). Based on cocoontrap; uses LethalMagicTrapDamageMultiplier vs vanilla 10 ATK; WebSpike_Death PNG clip at trap position.");
        lethalCocoonTrapDeathClipPath = Config.Bind(
            "HellTraps",
            "LethalCocoonTrapDeathClipPath",
            "",
            "Folder with numbered PNG frames for lethal cocoon death (PPU 100). Empty = sources/HellGate_sources/CustomDeath/WebSpike_Death.");
        lethalCocoonTrapDeathClipDisplayScale = Config.Bind(
            "HellTraps",
            "LethalCocoonTrapDeathClipDisplayScale",
            1f,
            "Uniform world scale for lethal cocoon death PNG overlay (same bone playback as magic trap; 1 = native at 100 PPU; WebSpike_Death ~823x984 px vs Exp_Death ~1400x835).");

        portraitModDisplayScale = Config.Bind(
            "PortraitMod",
            "DisplayScale",
            1f,
            "Uniform scale applied to the overlay RectTransform after native size and optional MaxNativeWidth clamp."
        );
        portraitModMaxNativeWidth = Config.Bind(
            "PortraitMod",
            "MaxNativeWidth",
            384f,
            "Maximum width in layout units after SetNativeSize (aspect preserved). Caps oversized textures; set 0 to disable."
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

        mindBrokenHScenePercentPerSecond = Config.Bind(
            "MindBroken",
            "HScenePercentPerSecond",
            0.1f,
            "Passive MindBroken gain per second while in H-scene (eroflag + erodown). 0.1 = +0.1%/sec. 0 = disable. Stacks with enemy-specific ticks (Mutude, Pilgrim, etc.)."
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

        mindBrokenDebugLogAddPercent = Config.Bind(
            "MindBroken",
            "DebugLogAddPercent",
            false,
            "Log every MindBroken AddPercent call with reason to BepInEx (for diagnosing runaway temptation)."
        );

        // HSceneBlackBackground MindBroken tick
        enableHSceneBlackBackground = Config.Bind(
            "HSceneBlackBackground",
            "Enable",
            true,
            "Black fullscreen background on H-scene climax (FIN / iki triggers). Set false to disable the effect."
        );

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
            "",
            "Optional extra boss type keys (lowercase class names, comma-separated). Story bosses use FactionBossDetection (vanilla BOSSflag) automatically."
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

        eventCoreEnable = Config.Bind(
            "EventCore",
            "Enable",
            true,
            "Enable EventCore (modal dialogues / branches; spawn lines use |ec_event=). HellGateJson/EventCore content is inactive when false."
        );
        eventCoreDevHotkey = Config.Bind(
            "EventCore",
            "DevHotkey",
            KeyCode.F9,
            "In-game: open DevEventId modal when EventCore is enabled"
        );
        eventCoreDevEventId = Config.Bind(
            "EventCore",
            "DevEventId",
            "eventcore_broker_gate",
            "Event id loaded from eventcore_manifest.json (e.g. eventcore_broker_gate, eventcore_smoke_test)"
        );
        eventCoreModalDimAlpha = Config.Bind(
            "EventCore",
            "ModalDimAlpha",
            0f,
            new ConfigDescription(
                "Darkening behind the text/button panel only (not across the full decorative PNG width). 0 = off; higher values add subtle dimming under the UI.",
                new AcceptableValueRange<float>(0f, 1f))
        );
        eventCoreHideVanillaHud = Config.Bind(
            "EventCore",
            "HideVanillaHudDuringModal",
            true,
            "While the EventCore modal is open, disable the vanilla gameplay HUD (root Canvas). When false, the HUD stays visible under the modal."
        );
        eventCoreBrokerPortraitAradiaScale = Config.Bind(
            "EventCore",
            "BrokerPortraitAradiaScale",
            1f,
            new ConfigDescription(
                "Display scale for Aradia (left) broker portraits. Lower if she looks larger than Touzoku despite smaller PNG width.",
                new AcceptableValueRange<float>(0.25f, 2f))
        );
        eventCoreBrokerPortraitTouzokuScale = Config.Bind(
            "EventCore",
            "BrokerPortraitTouzokuScale",
            1f,
            new ConfigDescription(
                "Display scale for Touzoku (right) broker portraits. Raise if hood/mask art looks too small in the frame.",
                new AcceptableValueRange<float>(0.25f, 2f))
        );

        ambientSpikeEncountersEnable = Config.Bind(
            "EventCore",
            "AmbientSpikeEncountersEnable",
            true,
            "Deprecated: use EventTrapEncountersEnable. This entry is read once to seed the new key if your cfg still only has the old name."
        );
        eventTrapEncountersEnable = Config.Bind(
            "EventCore",
            "EventTrapEncountersEnable",
            ambientSpikeEncountersEnable.Value,
            "EventTrap: non-modal coordinate-zone suspicion + knockdown ambush. Requires event_trap_registry.json (preferred) or legacy ambient_spike_registry.json under HellGateJson/EventCore/. System options: EventCore/_shared/<eventFolder>/config.json when present (otherwise per-language EventCore/<Lang>/<eventFolder>/config.json). Phrases: EventCore/<Lang>/<eventFolder>/phrases.json with fallback order En, Ru, Jp, Cn, Kr, Fr, De, Pt, Br, Es."
        );

        reinforcementEncountersEnable = Config.Bind(
            "EventCore",
            "ReinforcementEncountersEnable",
            true,
            "Reinforcement: knockdown-triggered extra spawns when the player is within triggerRadiusFromAnchor of a REINFORCEMENT,folder,x,y anchor. Optional suspicion lines (phrasesFromEventFolder). Requires HellGateJson/EventCore/reinforcement_registry.json and EventCore/_shared/<folder>/config.json."
        );

        // Rage Mode configs
        enableRageMode = Config.Bind(
            "RageMode",
            "Enable",
            true,
            "Enable Rage Mode system (counter-mechanic to MindBroken)"
        );

        rageActiveImmuneGrabAndKnockdown = Config.Bind(
            "RageMode",
            "ActiveImmuneGrabAndKnockdown",
            true,
            "While Rage is active: block elite grab (collision + grab-via-attack) and prevent knockdown from kickback types 3/4/6 (damage still applies)."
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
            30.0f,
            "Rage Energy percentage per boss kill (30.0 = 30%). Boss detection uses vanilla BOSSflag / FactionBossDetection."
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
        
        rageSPGainPercent = Config.Bind(
            "QTE",
            "RageAttackClickSPGainPercent",
            0.5f,
            "SP gain per attack click during Rage as percentage of max SP (0.5 = 50%). In QTE section for consistency."
        );
        
        rageActivationCameraShake = Config.Bind(
            "RageMode",
            "ActivationCameraShake",
            true,
            "Camera shake effect when Rage activates"
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
            0f,
            "Tier 3 wings: loop duration in seconds. 0 = until Rage ends (recommended). Positive = auto-destroy after N seconds."
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
            0.016f,
            "SP gain for A/D buttons at 0% MindBroken (0.05 = 5% of MaxSP)"
        );
        
        qteSPGainMin = Config.Bind(
            "QTE",
            "SPGainMin",
            0.002f,
            "SP gain for A/D buttons at 100% MindBroken (0.02 = 2% of MaxSP)"
        );
        
        qteYellowButtonSPGainMin = Config.Bind(
            "QTE",
            "YellowButtonSPGainMin",
            0.05f,
            "Minimum SP gain for yellow W/S buttons (0.15 = 15% of MaxSP)"
        );
        
        qteYellowButtonSPGainMax = Config.Bind(
            "QTE",
            "YellowButtonSPGainMax",
            0.2f,
            "Maximum SP gain for yellow W/S buttons (0.3 = 30% of MaxSP)"
        );
        
        qteClickSPGainBase = Config.Bind(
            "QTE",
            "ClickSPGainBase",
            0.01f,
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
        
        // QTE System 3.0 - Button Positioning (reference resolution 1920x1080 canvas space)
        qteButtonPositionX = Config.Bind(
            "QTE",
            "ButtonPositionX",
            0f,
            "Shift the whole QTE button row left/right from screen center (NOT spacing). Pixels at 1080p ref: negative = left, positive = right. Example: -150 left, +150 right. ButtonSpacing is separate."
        );

        qteButtonPositionY = Config.Bind(
            "QTE",
            "ButtonPositionY",
            70f,
            "Distance from top of screen to the button row center, in pixels (1080p reference). Default 70 matches pre-1.2.1 HUD height."
        );
        
        qteButtonSpacing = Config.Bind(
            "QTE",
            "ButtonSpacing",
            100f,
            "Gap between adjacent QTE buttons in the row (does NOT move the row left/right — use ButtonPositionX for that)"
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

        startZoomSkipEnemyFatality = Config.Bind(
            "HSceneEffects",
            "StartZoom.SkipEnemyFatality",
            true,
            "When true, HellGate start-zoom and spacebar zoom skip RequiemKnight death-fatality only (void camera risk). Other *Fatality grabs (Butcher/Slaughterer, BossScapegoatentrance, Candore, …) use HellGate camera like normal grabs."
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
            0f,
            "Seconds after zoom begins before slowmo starts (0 = together with zoom)"
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
            3.0f,
            "Duration of slowmo effect in seconds (real time, runs parallel with zoom when delay is 0)"
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
        
        // H-Scene Camera Zoom configs (spacebar cycle: ResetZoomValue → ZoomLevel3x → ZoomLevel5x → ResetZoomValue)
        cameraZoomResetValue = Config.Bind(
            "HSceneCameraZoom",
            "ResetZoomValue",
            1.5f,
            "H-scene spacebar zoom — step 1 (base). Also applied when H-scene ends. Cycle: 1.5x → 3x → 5x → 1.5x"
        );

        cameraZoomLevel3x = Config.Bind(
            "HSceneCameraZoom",
            "ZoomLevel3x",
            3f,
            "H-scene spacebar zoom — step 2 (medium). Cycle: 1.5x → 3x → 5x → 1.5x"
        );

        cameraZoomLevel5x = Config.Bind(
            "HSceneCameraZoom",
            "ZoomLevel5x",
            5f,
            "H-scene spacebar zoom — step 3 (max). Cycle: 1.5x → 3x → 5x → 1.5x"
        );

        cameraZoomLevel2x = Config.Bind(
            "HSceneCameraZoom",
            "ZoomLevel2x",
            2f,
            "[UNUSED] Reserved zoom preset (not in current spacebar cycle)"
        );

        cameraZoomLevel4x = Config.Bind(
            "HSceneCameraZoom",
            "ZoomLevel4x",
            4f,
            "[UNUSED] Reserved zoom preset (not in current spacebar cycle)"
        );

        cameraZoomLevel8x = Config.Bind(
            "HSceneCameraZoom",
            "ZoomLevel8x",
            8f,
            "[UNUSED] Reserved zoom preset (not in current spacebar cycle)"
        );

        cameraZoomLevel10x = Config.Bind(
            "HSceneCameraZoom",
            "ZoomLevel10x",
            10f,
            "[UNUSED] Reserved zoom preset (not in current spacebar cycle)"
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
        lethalTrapVengeanceShockSoundEnable = Config.Bind(
            "TakeVengeance",
            "LethalTrapShockSoundEnable",
            true,
            "After Take Vengeance from lethal trap death: play MindShock.wav + HeartBeat.wav from sources/HellGate_sources/CustomDeath."
        );
        lethalTrapVengeanceShockMindShockVolume = Config.Bind(
            "TakeVengeance",
            "LethalTrapShockMindShockVolume",
            1f,
            "Volume for MindShock.wav during lethal-trap vengeance shock (0 = mute, 1 = full)."
        );
        lethalTrapVengeanceShockHeartBeatVolume = Config.Bind(
            "TakeVengeance",
            "LethalTrapShockHeartBeatVolume",
            1f,
            "Volume for HeartBeat.wav (vengeance shock loop + lethal trap proximity thoughts; 0 = mute, 1 = full)."
        );
    }

    private void Update() {
        StruggleSystem.Update();
    }

    private void OnDestroy() {
        NoREroMod.HellGate.Api.HellGateApi.Shutdown();
        SceneManager.sceneLoaded -= OnSceneLoaded_ResetCaches;
        SceneManager.sceneUnloaded -= OnSceneUnloaded_ResetHideoutSpawn;
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
        try {
            NoREroMod.Systems.UI.Portrait.PortraitModSystem.Cleanup();
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
    /// Also resets caches when a gameplay scene loads.
    /// Hideout offspring respawn is handled by HellGateLocationSpawnRefresh (zone refresh + altar reset).
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

        // Hideout offspring spawn is driven by HellGateLocationSpawnRefresh (zone refresh + altar reset).
    }

    private static void OnSceneUnloaded_ResetHideoutSpawn(Scene scene)
    {
        try
        {
            string sceneName = scene.name;
            if (HideoutSceneUtility.IsParishHideoutActive())
            {
                foreach (var child in Systems.Pregnancy.PregnancySlotStore.GetAliveChildrenInHideout())
                {
                    child.IsSpawned = false;
                }
                if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
                    Log?.LogInfo("[Pregnancy.Hideout] Reset IsSpawned flags for hideout children (scene unloaded)");
            }
        }
        catch (Exception ex)
        {
            Log?.LogWarning($"[Pregnancy.Hideout] Failed to reset hideout spawn flags: {ex.Message}");
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