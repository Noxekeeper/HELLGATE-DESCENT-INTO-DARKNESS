using System;
using System.Collections.Generic;
using UnityEngine;
using NoREroMod;

namespace NoREroMod.Systems.Dialogue;

/// <summary>
/// Main dialogue framework manager
/// Initializes system, registers enemies, manages lifecycle
/// </summary>
internal static class DialogueFramework
{
    private static bool _initialized = false;
    private static DialogueDatabase _database;
    private static DialoguePool _pool;
    private static DialogueEventProcessor _eventProcessor;
    private static DialogueSelector _selector;
    private static DialogueDisplay _display;

    /// <summary>
    /// Initialize framework
    /// </summary>
    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _database = new DialogueDatabase();
        _pool = new DialoguePool();
        _eventProcessor = new DialogueEventProcessor();
        
        _database.LoadAll();
        _pool.Initialize();
        SoundRegistry.Initialize();
        
        _selector = new DialogueSelector(_database);
        _display = new DialogueDisplay(_pool);

        // Initialize grab threats system
        GrabThreatDialogues.Initialize();
        
        // Initialize spectator comments system
        SpectatorCommentsSystem.Initialize();
        
        // Initialize TouzokuNormal custom phrases system during H-scenes
        TouzokuNormalHSceneDialogues.Initialize();
        TouzokuNormalHSceneDialogues.SetDisplay(_display);
        
        // Initialize TouzokuAxe custom phrases system during H-scenes
        TouzokuAxeHSceneDialogues.Initialize();
        TouzokuAxeHSceneDialogues.SetDisplay(_display);
        
        // Initialize InquisitionBlack custom phrases system during H-scenes
        InquisitionBlackHSceneDialogues.Initialize();
        InquisitionBlackHSceneDialogues.SetDisplay(_display);
        
        // Initialize Kakasi custom phrases system during H-scenes
        KakasiHSceneDialogues.Initialize();
        KakasiHSceneDialogues.SetDisplay(_display);

        // Initialize Goblin custom phrases system during H-scenes
        GoblinHSceneDialogues.Initialize();
        GoblinHSceneDialogues.SetDisplay(_display);

        // Initialize Goblin custom phrases system during H-scenes
        GoblinHSceneDialogues.Initialize();
        GoblinHSceneDialogues.SetDisplay(_display);

        // Initialize PC (Aradia) response system to TouzokuNormal lines
        AradiaTouzokuNormalDialogues.Initialize();
        AradiaTouzokuNormalDialogues.SetDisplay(_display);

        // Initialize PC (Aradia) thoughts system for InquisitionBlack
        AradiaInquisitionBlackDialogues.Initialize();
        AradiaInquisitionBlackDialogues.SetDisplay(_display);

        // Initialize PC (Aradia) thoughts system for TouzokuNormal
        AradiaTouzokuNormalDialogues.Initialize();
        AradiaTouzokuNormalDialogues.SetDisplay(_display);

        // Initialize PC (Aradia) response system to TouzokuAxe lines
        // TEMPORARILY DISABLED DUE TO COMPILATION ISSUES
        TouzokuAxeHSceneDialogues.Initialize();
        TouzokuAxeHSceneDialogues.SetDisplay(_display);

        _initialized = true;
    }

    /// <summary>
    /// Process animation event from enemy
    /// </summary>
    internal static void ProcessAnimationEvent(object enemyInstance, string animationName, string eventName, int seCount)
    {
        if (!_initialized)
        {
            return;
        }

        _eventProcessor.ProcessEvent(enemyInstance, animationName, eventName, seCount, _selector, _display);
    }
    
    /// <summary>
    /// Process grab ready event
    /// </summary>
    internal static void ProcessGrabThreatEvent(object enemyInstance, string enemyType)
    {
        if (!_initialized)
        {
            return;
        }

        GrabThreatDialogues.ProcessGrabThreat(enemyInstance, enemyType);
    }
    
    /// <summary>
    /// Get DialogueDisplay for use in other systems
    /// </summary>
    internal static DialogueDisplay GetDialogueDisplay()
    {
        return _display;
    }

    /// <summary>
    /// Stop system and cleanup resources
    /// </summary>
    internal static void Shutdown()
    {
        if (!_initialized)
        {
            return;
        }

        // Cleanup all subsystems
        SoundRegistry.Clear();
        _pool?.ClearPool();
        
        // Reset initialization flags for all dialogue systems
        GoblinHSceneDialogues.Reset();
        TouzokuNormalHSceneDialogues.Reset();
        TouzokuAxeHSceneDialogues.Reset();
        InquisitionBlackHSceneDialogues.Reset();
        KakasiHSceneDialogues.Reset();
        AradiaTouzokuNormalDialogues.Reset();
        AradiaInquisitionBlackDialogues.Reset();
        GrabThreatDialogues.Reset();
        SpectatorCommentsSystem.Reset();
        
        _initialized = false;
    }

    /// <summary>
    /// Reload system with new language
    /// Called after language selection on splash screen
    /// </summary>
    internal static void Reload()
    {
        if (_initialized)
        {
            Shutdown();
        }
        Initialize();
    }

    /// <summary>
    /// Check initialization
    /// </summary>
    internal static bool IsInitialized => _initialized;

    /// <summary>
    /// Get DialogueDisplay for use in other systems
    /// </summary>
    internal static DialogueDisplay GetDisplay()
    {
        return _initialized ? _display : null;
    }

    /// <summary>
    /// Show onomatopoeia for sound (universal method for all enemies)
    /// </summary>
    internal static void ShowSoundOnomatopoeia(object enemyInstance, string soundName, string customOnomatopoeia = null)
    {
        if (!_initialized)
        {
            return;
        }

        if (string.IsNullOrEmpty(soundName))
        {
            return;
        }

        string onomatopoeia;
        if (!string.IsNullOrEmpty(customOnomatopoeia))
        {
            // Use provided onomatopoeia (e.g. "AGH!" for unknown sounds)
            onomatopoeia = customOnomatopoeia;
        }
        else
        {
            // Get from SoundRegistry
            if (string.IsNullOrEmpty(soundName))
            {
                return;
            }
            onomatopoeia = SoundRegistry.GetRandomOnomatopoeia(soundName);

            if (string.IsNullOrEmpty(onomatopoeia))
            {
                return;
            }
        }

        // Determine bone for onomatopoeia based on enemy type
        string boneName = GetOnomatopoeiaBoneForEnemy(enemyInstance);

        BonePosition bonePos = new BonePosition
        {
            BoneName = boneName,
            UseScreenCenter = false
        };

            DialogueStyle style = new DialogueStyle
            {
                FontSize = Plugin.dialogueFontSize.Value,
                IsBold = (Plugin.GetFontStyle(Plugin.enemyFontStyle.Value) & FontStyle.Bold) != 0,
                IsItalic = (Plugin.GetFontStyle(Plugin.enemyFontStyle.Value) & FontStyle.Italic) != 0,
                Color = new Color(1f, 0.4f, 0.8f), // Pink color (RGB: 255, 102, 204)
                VerticalOffset = -50f, // 50px below bone
                HorizontalOffset = 0f
            };

        _display.ShowOnomatopoeia(enemyInstance, onomatopoeia, bonePos, style);
    }

    /// <summary>
    /// Determines bone for onomatopoeia display based on enemy type
    /// </summary>
    private static string GetOnomatopoeiaBoneForEnemy(object enemyInstance)
    {
        // Use bone37 for goblins (same bone as dialogues)
        if (enemyInstance?.GetType().Name == "goblinero")
        {
            return "bone37";
        }

        // Use bone13 for other enemies
        return "bone13";
    }
}

