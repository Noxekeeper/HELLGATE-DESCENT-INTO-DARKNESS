using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using NoREroMod;
using NoREroMod.Systems.Effects;

namespace NoREroMod.Systems.Dialogue;

/// <summary>
/// Process Spine events and determine event type
/// </summary>
internal class DialogueEventProcessor
{
    private readonly Dictionary<object, float> _lastEventTime = new();
    private readonly Dictionary<string, float> _soundStartTime = new(); // Key: animationName_seCount

    /// <summary>
    /// Process animation event
    /// </summary>
    internal void ProcessEvent(object enemyInstance, string animationName, string eventName, int seCount,
        DialogueSelector selector, DialogueDisplay display)
    {
        if (enemyInstance == null || string.IsNullOrEmpty(animationName) || string.IsNullOrEmpty(eventName))
        {
            return;
        }

        string animUpper = animationName?.ToUpperInvariant() ?? string.Empty;
        string eventUpper = eventName?.ToUpperInvariant() ?? string.Empty;

        // Process H-scene phrases for Kakasi (cross and ground) - BEFORE SE check
        // For cross events can be: start, start2, ero1, ero2, ero3, finish1, finish2, finish_end, finish_end2
        // For ground events can be: SE, ERO2, ERO3, ERO4, ERO5, FIN, JIGO1, JIGO2, START2
        string enemyName = GetEnemyNameFromInstance(enemyInstance);
        if (enemyName == "Kakasi")
        {
            try
            {
                KakasiHSceneDialogues.ProcessHSceneEvent(enemyInstance, animationName, eventName, seCount);
            }
            catch (Exception)
            {
                // Ignore errors
            }
        }

        // Process H-scene phrases for Goblin - ALL events (including GG_RESPONSE, GG_THOUGHT)
        if (enemyName == "Goblin")
        {
            try
            {
                GoblinHSceneDialogues.ProcessEvent(enemyInstance, animationName, eventName, seCount);
            }
            catch (Exception ex)
            {
                // Goblin event processing failed silently
            }
            // For goblins process ALL events, not only SE
            return;
        }

        // Process TouzokuAxe H-scene phrases — all events (including ARADIA_RESPONSE, ARADIA_THOUGHT)
        if (enemyName == "TouzokuAxe")
        {
            try
            {
                TouzokuAxeHSceneDialogues.ProcessHSceneEvent(enemyInstance, animationName, eventName, seCount);
            }
            catch (Exception ex)
            {
                // TouzokuAxe event processing failed silently
            }
        }

        // Process Aradia thoughts for InquisitionBlack — all events
        if (enemyName == "BlackInquisitor")
        {
            try
            {
                AradiaInquisitionBlackDialogues.ProcessInquisitionBlackAradiaEvent(enemyInstance, animationName, eventName, seCount);
            }
            catch (Exception ex)
            {
                // InquisitionBlack event processing failed silently
            }
            return;
        }

                // TouzokuNormal Aradia processing now handled by TouzokuNormalPassPatch to avoid duplicates
                // if (enemyName == "Touzoku")
                // {
                //     AradiaTouzokuNormalDialogues.ProcessEnemyComment(enemyInstance, animationName, eventName, seCount);
                // }

        // Code for other enemies starts here
        // Onomatopoeia should be driven only by SE* events (sound ticks), like in the old stable version.
        // Processing non-SE events here will update cooldown timestamps and can suppress real SE events.
        if (!eventUpper.StartsWith("SE"))
        {
            return;
        }
        
        DialogueEventType eventType = GetEventType(animationName, seCount);
        
        // (disabled) previously activated black background for FIN/FIN2
        
        if (!ShouldProcessEvent(enemyInstance, eventType, animationName))
        {
            return;
        }

        // Track sound start time for segmentation
        string soundKey = $"{animUpper}_{seCount}";
        float currentTime = Time.time;
        
        // If this is a new sound (no record or more than 10 seconds elapsed), reset the timer
        if (!_soundStartTime.ContainsKey(soundKey) || 
            (currentTime - _soundStartTime[soundKey]) > 10f)
        {
            _soundStartTime[soundKey] = currentTime;
        }

        float timeSinceSoundStart = currentTime - _soundStartTime[soundKey];
        string onomatopoeia = selector.SelectOnomatopoeia(animationName, seCount, timeSinceSoundStart);
        
        if (string.IsNullOrEmpty(onomatopoeia))
        {
            return;
        }
        
        // Update event time only when we actually show something.
        _lastEventTime[enemyInstance] = currentTime;

        BonePosition bonePos = GetBonePosition(animationName, seCount, enemyInstance);
        DialogueStyle style = GetDialogueStyle(eventType, animationName);
        display.ShowOnomatopoeia(enemyInstance, onomatopoeia, bonePos, style);
    }

    /// <summary>
    /// Determine event type from animation and se_count
    /// </summary>
    private DialogueEventType GetEventType(string animationName, int seCount)
    {
        string animUpper = animationName?.ToUpperInvariant() ?? string.Empty;

        if (animUpper == "FIN" || animUpper == "FIN2")
        {
            return DialogueEventType.Climax;
        }

        if (animUpper == "START_JIGO")
        {
            return DialogueEventType.Transfer;
        }

        if (animUpper == "ERO3" || animUpper == "ERO4" || animUpper == "ERO5")
        {
            if (seCount == 2 || seCount == 4)
            {
                return DialogueEventType.SlimeWet;
            }
            return DialogueEventType.Thrust;
        }

        if (animUpper == "ERO2" || animUpper == "ERO2_2")
        {
            return DialogueEventType.Thrust;
        }

        if (animUpper == "ERO1" || animUpper == "ERO1_2")
        {
            return DialogueEventType.Thrust;
        }

        if (animUpper == "START")
        {
            return DialogueEventType.Stamina;
        }

        if (animUpper == "DRINK" || animUpper == "DRINK_END")
        {
            return DialogueEventType.SlimeWet;
        }

        return DialogueEventType.Thrust;
    }

    /// <summary>
    /// Check need to process event (minimum cooldown)
    /// </summary>
    private bool ShouldProcessEvent(object enemyInstance, DialogueEventType eventType, string animationName)
    {
        float currentTime = Time.time;
        
        if (_lastEventTime.ContainsKey(enemyInstance))
        {
            float timeSinceLastEvent = currentTime - _lastEventTime[enemyInstance];
            float minCooldown = Plugin.dialogueEventMinCooldown?.Value ?? 0.1f;
            if (timeSinceLastEvent < minCooldown)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Get bone position for animation
    /// Picks the correct bone based on enemy type and animation
    /// </summary>
    private BonePosition GetBonePosition(string animationName, int seCount, object enemyInstance)
    {
        string animUpper = animationName?.ToUpperInvariant() ?? string.Empty;
        string enemyName = GetEnemyNameFromInstance(enemyInstance);
        
        // TouzokuNormal (EroTouzoku) - GG bone: head
        if (enemyName == "Touzoku")
        {
            return new BonePosition 
            { 
                BoneName = "head",  // PC bone for TouzokuNormal
                UseScreenCenter = false
            };
        }
        
        // TouzokuAxe (EroTouzokuAXE) - PC bones: bone82 (Start-Start5), bone25 (others)
        if (enemyName == "TouzokuAxe")
        {
            // Opening animations: Start, Start1, Start2, Start3, Start4, Start5
            if (animUpper == "START" || animUpper == "START1" || animUpper == "START2" ||
                animUpper == "START3" || animUpper == "START4" || animUpper == "START5")
            {
                return new BonePosition
                {
                    BoneName = "bone82",
                    UseScreenCenter = false
                };
            }
            // All remaining animations
            return new BonePosition
            {
                BoneName = "bone25",
                UseScreenCenter = false
            };
        }
        
        // Dorei (SinnerslaveCrossbow) - PC bones: bone17 (Start1-3, JIGO, JIGO2, ERO, ERO0, ERO1), bone30 (others)
        if (enemyName == "dorei")
        {
            if (animUpper == "START1" || animUpper == "START2" || animUpper == "START3" ||
                animUpper == "JIGO" || animUpper == "JIGO2" ||
                animUpper == "ERO" || animUpper == "ERO0" || animUpper == "ERO1")
            {
                return new BonePosition 
                { 
                    BoneName = "bone17",
                    UseScreenCenter = false
                };
            }
            // All remaining animations
            return new BonePosition 
            { 
                BoneName = "bone30",
                UseScreenCenter = false
            };
        }
        
        // Mutude - Mutude bone: bone37 (working bone)
        if (enemyName == "Mutude")
        {
            return new BonePosition 
            { 
                BoneName = "bone37",  // Working bone for Mutude
                UseScreenCenter = false
            };
        }
        
        // InquisitionBlack (InquiBlackEro) - GG bone: bone32 (for onomatopoeia)
        if (enemyName == "BlackInquisitor")
        {
            return new BonePosition 
            { 
                BoneName = "bone32",  // PC bone for InquisitionBlack onomatopoeia (try bone32)
                UseScreenCenter = false
            };
        }
        
        // Kakasi (kakashi_ero2 and EroAnimation) — GG bone: hair_front for cross, face for ground
        if (enemyName == "Kakasi")
        {
            // Detect cross vs ground from instance type
            string typeName = enemyInstance?.GetType().Name ?? "";
            if (typeName == "EroAnimation")
            {
                // Cross — GG bone: hair_front
                return new BonePosition 
                { 
                    BoneName = "hair_front",  // PC bone for Kakasi onomatopoeia on cross
                    UseScreenCenter = false
                };
            }
            else
            {
                // Ground — GG bone: face
                return new BonePosition 
                { 
                    BoneName = "face",  // PC bone for Kakasi onomatopoeia on ground
                    UseScreenCenter = false
                };
            }
        }
        
        // For other enemies use bone13 by default
        return new BonePosition 
        { 
            BoneName = "bone13",
            UseScreenCenter = false
        };
    }

    /// <summary>
    /// Get dialogue style
    /// </summary>
    private DialogueStyle GetDialogueStyle(DialogueEventType eventType, string animationName)
    {
        return new DialogueStyle
        {
            // Onomatopoeia: size 20, pink color
            FontSize = Plugin.dialogueFontSize.Value,
            Color = new Color(1f, 0.4f, 0.8f), // pink, same as DialogueFramework
            IsBold = (Plugin.GetFontStyle(Plugin.enemyFontStyle.Value) & FontStyle.Bold) != 0,
            IsItalic = (Plugin.GetFontStyle(Plugin.enemyFontStyle.Value) & FontStyle.Italic) != 0,
            UseOutline = true,
            OutlineColor = Color.black,
            OutlineDistance = new Vector2(1f, -1f)
        };
    }
    
    /// <summary>
    /// Get enemy name from instance
    /// </summary>
    private string GetEnemyNameFromInstance(object enemyInstance)
    {
        if (enemyInstance == null)
        {
            return null;
        }
        
        string typeName = enemyInstance.GetType().Name;
        
        // Map enemy types to names
        // For Dorei return "dorei" as the primary name (used in JSON), 
        // but the system also supports "SinnerslaveCrossbow" as an alternative
        if (typeName == "EroTouzokuAXE" || typeName.Contains("TouzokuAXE"))
            return "TouzokuAxe";
        else if (typeName == "EroTouzoku" || typeName.Contains("EroTouzoku"))
            return "Touzoku";
        else if (typeName == "SinnerslaveCrossbowERO" || typeName.Contains("SinnerslaveCrossbow"))
            return "dorei"; // Primary JSON name; "SinnerslaveCrossbow" is supported as fallback
        else if (typeName == "EroAnimation" || typeName == "kakashi_ero2" || typeName.Contains("Kakasi") || typeName.Contains("Kakash"))
            return "Kakasi";  // FIXED: added EroAnimation for cross
        else if (typeName == "goblinero" || typeName.Contains("Goblin"))
            return "Goblin";
        else if (typeName == "InquiBlackEro" || typeName.Contains("InquisitionBlack"))
            return "BlackInquisitor";
        else if (typeName == "Mutudeero" || typeName == "Mutude" || typeName.Contains("Mutude"))
            return "Mutude";
        
        return null;
    }
}

/// <summary>
/// Dialogue event type
/// </summary>
internal enum DialogueEventType
{
    Stamina,
    Thrust,
    SlimeWet,
    Climax,
    Transfer
}

/// <summary>
/// Bone position for text placement
/// </summary>
public struct BonePosition
{
    public string BoneName;  // May contain a nested bone path via "/" (e.g. "bone37/E_face/E_face")
    public bool UseScreenCenter;

    /// <summary>
    /// If true, only <see cref="BoneName"/> is used (no alternate player bones or multi-skeleton name fallbacks).
    /// </summary>
    public bool DisableBoneFallbacks;

    /// <summary>Lift along skeleton Y before world transform (Spine bone WorldY + this).</summary>
    public float WorldOffsetY;
}

/// <summary>
/// Dialogue display style
/// </summary>
public struct DialogueStyle
{
    public float FontSize;
    public Color Color;
    public bool IsBold;
    public bool IsItalic;
    public float VerticalOffset;
    public float HorizontalOffset;
    public bool FollowBone; // Follow bone or use static positioning
    public bool UseOutline;
    public Color OutlineColor;
    public Vector2 OutlineDistance;
}

