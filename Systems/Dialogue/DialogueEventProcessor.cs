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
            // For goblins обрабатываем ВСЕ events, not only SE
            return;
        }

        // Processing H-сценных phrases for TouzokuAxe - все events (включая ARADIA_RESPONSE, ARADIA_THOUGHT)
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

        // Processing мыслей Арадии for InquisitionBlack - все events
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

        // Код for other enemies начинается здесь
        // Onomatopoeia should be driven only by SE* events (sound ticks), like in the old stable version.
        // Processing non-SE events here will update cooldown timestamps and can suppress real SE events.
        if (!eventUpper.StartsWith("SE"))
        {
            return;
        }
        
        DialogueEventType eventType = GetEventType(animationName, seCount);
        
        // (отключено) ранее вызывали активацию черного background for FIN/FIN2
        
        if (!ShouldProcessEvent(enemyInstance, eventType, animationName))
        {
            return;
        }

        // Отслеживаем время начала sounds for сегментации
        string soundKey = $"{animUpper}_{seCount}";
        float currentTime = Time.time;
        
        // If this новый sound (нет записи or прошло больше 10 seconds), сбрасываем время
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
    /// Определение типа events by animation и se_count
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
    /// Получение позиции кости for animation
    /// Determines правильную кость depending on enemy type и animation
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
                BoneName = "head",  // Кость ГГ for TouzokuNormal
                UseScreenCenter = false
            };
        }
        
        // TouzokuAxe (EroTouzokuAXE) - кости ГГ: bone82 (Start-Start5), bone25 (остальные)
        if (enemyName == "TouzokuAxe")
        {
            // Начальные animation: Start, Start1, Start2, Start3, Start4, Start5
            if (animUpper == "START" || animUpper == "START1" || animUpper == "START2" ||
                animUpper == "START3" || animUpper == "START4" || animUpper == "START5")
            {
                return new BonePosition
                {
                    BoneName = "bone82",
                    UseScreenCenter = false
                };
            }
            // Все остальные animation
            return new BonePosition
            {
                BoneName = "bone25",
                UseScreenCenter = false
            };
        }
        
        // Dorei (SinnerslaveCrossbow) - кости ГГ: bone17 (Start1-3, JIGO, JIGO2, ERO, ERO0, ERO1), bone30 (остальные)
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
            // Все остальные animation
            return new BonePosition 
            { 
                BoneName = "bone30",
                UseScreenCenter = false
            };
        }
        
        // Mutude - кость Mutude: bone37 (рабочая кость)
        if (enemyName == "Mutude")
        {
            return new BonePosition 
            { 
                BoneName = "bone37",  // Рабочая кость for Mutude
                UseScreenCenter = false
            };
        }
        
        // InquisitionBlack (InquiBlackEro) - GG bone: bone32 (for onomatopoeia)
        if (enemyName == "BlackInquisitor")
        {
            return new BonePosition 
            { 
                BoneName = "bone32",  // Кость ГГ for onomatopoeia InquisitionBlack (пробуем bone32)
                UseScreenCenter = false
            };
        }
        
        // Kakasi (kakashi_ero2 и EroAnimation) - GG bone: hair_front for креста, face for земли
        if (enemyName == "Kakasi")
        {
            // Определяем, крест this or земля by типу instanceа
            string typeName = enemyInstance?.GetType().Name ?? "";
            if (typeName == "EroAnimation")
            {
                // Крест - GG bone: hair_front
                return new BonePosition 
                { 
                    BoneName = "hair_front",  // Кость ГГ for onomatopoeia Kakasi on кресте
                    UseScreenCenter = false
                };
            }
            else
            {
                // Земля - GG bone: face
                return new BonePosition 
                { 
                    BoneName = "face",  // Кость ГГ for onomatopoeia Kakasi on земле
                    UseScreenCenter = false
                };
            }
        }
        
        // For other enemies используем bone13 by умолчанию
        return new BonePosition 
        { 
            BoneName = "bone13",
            UseScreenCenter = false
        };
    }

    /// <summary>
    /// Получение стиля диалога
    /// </summary>
    private DialogueStyle GetDialogueStyle(DialogueEventType eventType, string animationName)
    {
        return new DialogueStyle
        {
            // Ономатопеи: size 20, розовый цвет
            FontSize = Plugin.dialogueFontSize.Value,
            Color = new Color(1f, 0.4f, 0.8f), // розовый, as in DialogueFramework
            IsBold = (Plugin.GetFontStyle(Plugin.enemyFontStyle.Value) & FontStyle.Bold) != 0,
            IsItalic = (Plugin.GetFontStyle(Plugin.enemyFontStyle.Value) & FontStyle.Italic) != 0,
            UseOutline = true,
            OutlineColor = Color.black,
            OutlineDistance = new Vector2(1f, -1f)
        };
    }
    
    /// <summary>
    /// Get имя enemy from instanceа
    /// </summary>
    private string GetEnemyNameFromInstance(object enemyInstance)
    {
        if (enemyInstance == null)
        {
            return null;
        }
        
        string typeName = enemyInstance.GetType().Name;
        
        // Маппинг типоin enemies on имена
        // For Dorei возвращаем "dorei" as основное имя (используется in JSON), 
        // but система also поддерживает "SinnerslaveCrossbow" as альтернативное
        if (typeName == "EroTouzokuAXE" || typeName.Contains("TouzokuAXE"))
            return "TouzokuAxe";
        else if (typeName == "EroTouzoku" || typeName.Contains("EroTouzoku"))
            return "Touzoku";
        else if (typeName == "SinnerslaveCrossbowERO" || typeName.Contains("SinnerslaveCrossbow"))
            return "dorei"; // Основное имя for JSON данных, "SinnerslaveCrossbow" поддерживается as fallback
        else if (typeName == "EroAnimation" || typeName == "kakashi_ero2" || typeName.Contains("Kakasi") || typeName.Contains("Kakash"))
            return "Kakasi";  // FIXED: добавлен EroAnimation for креста
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
/// Тип events диалога
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
/// Позиция кости for позиционирования text
/// </summary>
public struct BonePosition
{
    public string BoneName;  // Может содержать путь к вложенным костям через "/" (e.g.: "bone37/E_face/E_face")
    public bool UseScreenCenter;
}

/// <summary>
/// Стиль отображения диалога
/// </summary>
public struct DialogueStyle
{
    public float FontSize;
    public Color Color;
    public bool IsBold;
    public bool IsItalic;
    public float VerticalOffset;
    public float HorizontalOffset;
    public bool FollowBone; // Следовать за костью or статичное позиционирование
    public bool UseOutline;
    public Color OutlineColor;
    public Vector2 OutlineDistance;
}

