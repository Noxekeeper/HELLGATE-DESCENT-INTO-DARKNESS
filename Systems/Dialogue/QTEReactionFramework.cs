using System;
using UnityEngine;
using NoREroMod.Patches.UI.MindBroken;

namespace NoREroMod.Systems.Dialogue;

/// <summary>
/// Main QTE reactions system manager
/// Modular and independent system, similar to DialogueFramework
/// </summary>
internal static class QTEReactionFramework
{
    private static bool _initialized = false;
    private static QTEReactionDatabase _database;
    private static DialogueDisplay _display;
    
    private static float _lastReactionTime = 0f;
    private static int _lastComboMilestone = 0;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _database = new QTEReactionDatabase();
        _database.LoadAll();
        
// QTE reactions database loaded
        
        // Subscribe to QTE events (QTESystem is in global namespace)
        NoREroMod.QTESystem.OnQTEWrong += OnQTEWrong;
        NoREroMod.QTESystem.OnQTEComboMilestone += OnQTEComboMilestone;
        
        _initialized = true;
    }

    internal static void Shutdown()
    {
        if (!_initialized)
        {
            return;
        }

        // Unsubscribe from events
        NoREroMod.QTESystem.OnQTEWrong -= OnQTEWrong;
        NoREroMod.QTESystem.OnQTEComboMilestone -= OnQTEComboMilestone;
        
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
    /// Process correct press (called from QTESystem)
    /// </summary>
    internal static void OnQTECorrect(int currentCombo, string enemyName = null)
    {
        if (!_initialized)
        {
            return;
        }

        // Check minimum combo
        if (currentCombo < _database.MinComboForReaction)
        {
            return;
        }

        // Check cooldown
        float timeSinceLastReaction = Time.time - _lastReactionTime;
        if (timeSinceLastReaction < _database.CooldownSeconds)
        {
            return;
        }

        // Check probability
        float roll = UnityEngine.Random.Range(0f, 1f);
        if (roll > _database.CorrectPressChance)
        {
            return;
        }

        // If enemyName not provided, try to get it
        if (string.IsNullOrEmpty(enemyName))
        {
            enemyName = GetCurrentEnemyName();
        }

        // Выбираем категорию considering MindBroken уровня
        string category = GetCategoryByMindBroken(true); // true = correctPress (from enemy)
        
        string[] phrases = _database.GetCorrectPressPhrases(category, enemyName);
        
        if (phrases.Length > 0)
        {
            string phrase = phrases[UnityEngine.Random.Range(0, phrases.Length)];
            ShowEnemyReaction(phrase, enemyName);
            _lastReactionTime = Time.time;
        }
    }

    /// <summary>
    /// Processing неcorrect press
    /// </summary>
    private static void OnQTEWrong()
    {
        if (!_initialized)
        {
            return;
        }


        // Check cooldown
        float timeSinceLastReaction = Time.time - _lastReactionTime;
        if (timeSinceLastReaction < _database.CooldownSeconds)
        {
                return;
        }

        // Check probability
        float roll = UnityEngine.Random.Range(0f, 1f);
        if (roll > _database.WrongPressChance)
        {
            return;
        }

        // Get имя enemy (if доступно)
        string enemyName = GetCurrentEnemyName();

        // Выбираем категорию considering MindBroken уровня
        string category = GetCategoryByMindBroken(false); // false = wrongPress (from ГГ)
        
        string[] phrases = _database.GetWrongPressPhrases(category, enemyName);
        if (phrases.Length > 0)
        {
            string phrase = phrases[UnityEngine.Random.Range(0, phrases.Length)];
            ShowPlayerReaction(phrase, enemyName);
            _lastReactionTime = Time.time;
        }
    }

    /// <summary>
    /// Processing достижения комбо-вехи
    /// </summary>
    private static void OnQTEComboMilestone(int milestone)
    {
        if (!_initialized)
        {
            return;
        }

        // Check that this новая веха (not повтор)
        if (milestone == _lastComboMilestone)
        {
            return;
        }

        _lastComboMilestone = milestone;

        // Check probability (обычbut 100% for комбо)
        float roll = UnityEngine.Random.Range(0f, 1f);
        if (roll > _database.ComboMilestoneChance)
        {
            return;
        }

        string[] phrases = _database.GetComboMilestonePhrases(milestone);
        if (phrases.Length > 0)
        {
            string phrase = phrases[UnityEngine.Random.Range(0, phrases.Length)];
            ShowEnemyReaction(phrase, null); // Комбо-реакции always from enemy
            _lastReactionTime = Time.time;
        }
    }

    /// <summary>
    /// Display реакцию enemy (correctPress, comboMilestone) on костях enemy
    /// </summary>
    private static void ShowEnemyReaction(string text, string enemyName)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        DialogueDisplay display = DialogueFramework.GetDisplay();
        if (display == null)
        {
            return;
        }

        // Get enemyInstance for позиционирования
        object enemyInstance = GetCurrentEnemyInstance();
        if (enemyInstance == null)
        {
            // Fallback: use old method if enemyInstance недоступен
            ShowReactionFallback(text);
            return;
        }

        // Use ShowTouzokuHSceneComment for отображения on костях enemy
        // Длительность 3 seconds, size шрифта 30px (увеличен for QTE reactions)
        // Offset 95px (70 + 25) to avoid попадали on другие phrases
        // Красный цвет text with черной обводкой for ошибок QTE
        display.ShowTouzokuHSceneComment(enemyInstance, text, 3f, 30f, 95f, 0f, new Color(1f, 0f, 0f, 1f), new Color(0f, 0f, 0f, 1f));
    }

    /// <summary>
    /// Display реакцию ГГ (wrongPress) on костях ГГ
    /// </summary>
    private static void ShowPlayerReaction(string text, string enemyName)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        DialogueDisplay display = DialogueFramework.GetDisplay();
        if (display == null)
        {
            return;
        }

        // Get enemyInstance for позиционирования (нужен for search костей ГГ)
        object enemyInstance = GetCurrentEnemyInstance();
        if (enemyInstance == null)
        {
            // Fallback: use old method if enemyInstance недоступен
            ShowReactionFallback(text);
            return;
        }

        // Use ShowAradiaHSceneComment for отображения on костях ГГ
        // Длительность 3 seconds, size шрифта 30px (увеличен for QTE reactions), offset 75px (50 + 25), кость "face"
        // Offset увеличен on 25px to avoid попадали on другие phrases
        // Красный цвет text with черной обводкой for ошибок QTE
        display.ShowAradiaHSceneComment(enemyInstance, text, 3f, 30f, 75f, 0f, "face", new Color(1f, 0f, 0f, 1f), new Color(0f, 0f, 0f, 1f));
    }

    /// <summary>
    /// Fallback метод for отображения реакции (if enemyInstance недоступен)
    /// </summary>
    private static void ShowReactionFallback(string text)
    {
        DialogueDisplay display = DialogueFramework.GetDisplay();
        if (display == null)
        {
            return;
        }

        GameObject centerObj = new GameObject("QTEReactionCenter");
        centerObj.transform.position = Vector3.zero;

        BonePosition bonePos = new BonePosition
        {
            BoneName = null,
            UseScreenCenter = true
        };

        var fontStyle = Plugin.GetFontStyle(Plugin.enemyFontStyle.Value);
        DialogueStyle style = new DialogueStyle
        {
            FontSize = Plugin.dialogueFontSize.Value,
            Color = Color.white,
            IsBold = (fontStyle & FontStyle.Bold) != 0,
            IsItalic = (fontStyle & FontStyle.Italic) != 0,
            UseOutline = true,
            OutlineColor = Color.black,
            OutlineDistance = new Vector2(1f, -1f)
        };

        display.ShowOnomatopoeia(centerObj, text, bonePos, style);
        UnityEngine.Object.Destroy(centerObj, 0.1f);
    }

    /// <summary>
    /// Get enemyInstance from QTESystem
    /// </summary>
    private static object GetCurrentEnemyInstance()
    {
        try
        {
            var qteSystemType = typeof(NoREroMod.QTESystem);
            var currentEnemyField = qteSystemType.GetField("currentEnemyInstance", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            if (currentEnemyField != null)
            {
                return currentEnemyField.GetValue(null);
            }
        }
        catch { }
        
        return null;
    }

    /// <summary>
    /// Выбрать категорию реакции considering уровня MindBroken
    /// </summary>
    private static string GetCategoryByMindBroken(bool isEnemyReaction)
    {
        float mindBrokenPercent = MindBrokenSystem.Percent;
        
        if (isEnemyReaction)
        {
            // Реакции enemy (correctPress, comboMilestone)
            if (mindBrokenPercent < 0.5f) // 0-49% - низкий
            {
                // Больше доминирующtheir и насмешливых
                float roll = UnityEngine.Random.Range(0f, 1f);
                if (roll < 0.5f) return "dominant";
                if (roll < 0.8f) return "taunting";
                return "sexual";
            }
            else if (mindBrokenPercent < 0.8f) // 50-79% - средний
            {
                // Смешанные категории
                string[] categories = { "taunting", "dominant", "sexual" };
                return categories[UnityEngine.Random.Range(0, categories.Length)];
            }
            else // 80-100% - высокий
            {
                // Больше сексуальных и доминирующих
                float roll = UnityEngine.Random.Range(0f, 1f);
                if (roll < 0.4f) return "sexual";
                if (roll < 0.7f) return "dominant";
                return "taunting";
            }
        }
        else
        {
            // Реакции ГГ (wrongPress)
            if (mindBrokenPercent < 0.5f) // 0-49% - низкий
            {
                // Больше угрожающtheir и доминирующtheir (сопротивление)
                float roll = UnityEngine.Random.Range(0f, 1f);
                if (roll < 0.4f) return "threatening";
                if (roll < 0.7f) return "dominant";
                if (roll < 0.85f) return "punishment";
                return "sexual";
            }
            else if (mindBrokenPercent < 0.8f) // 50-79% - средний
            {
                // Смешанные категории
                string[] categories = { "punishment", "dominant", "sexual", "threatening" };
                return categories[UnityEngine.Random.Range(0, categories.Length)];
            }
            else // 80-100% - высокий
            {
                // Больше сексуальных и подчиненных
                float roll = UnityEngine.Random.Range(0f, 1f);
                if (roll < 0.5f) return "sexual";
                if (roll < 0.75f) return "dominant";
                if (roll < 0.9f) return "punishment";
                return "threatening";
            }
        }
    }

    /// <summary>
    /// Get current enemy (if доступно)
    /// </summary>
    private static string GetCurrentEnemyName()
    {
        try
        {
            // Get currentEnemyInstance from QTESystem via reflection
            var qteSystemType = typeof(NoREroMod.QTESystem);
            var currentEnemyField = qteSystemType.GetField("currentEnemyInstance", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            if (currentEnemyField != null)
            {
                object enemyInstance = currentEnemyField.GetValue(null);
                if (enemyInstance != null)
                {
                    string typeName = enemyInstance.GetType().Name;
                    // Маппинг типоin enemies on имеon in JSON
                    
                    // H-scene (Ero*)
                    if (typeName == "EroTouzokuAXE" || typeName.Contains("TouzokuAXE"))
                    {
                        return "TouzokuAxe";
                    }
                    else if (typeName == "EroTouzoku" || typeName.Contains("EroTouzoku"))
                    {
                        return "Touzoku";
                    }
                    else if (typeName == "SinnerslaveCrossbowERO" || typeName.Contains("SinnerslaveCrossbowERO"))
                    {
                        return "SinnerslaveCrossbow";
                    }
                    else if (typeName == "kakashi_ero2" || typeName.Contains("kakashi_ero"))
                    {
                        return "Kakasi";
                    }
                    else if (typeName == "goblinero" || typeName.Contains("goblinero"))
                    {
                        return "Goblin";
                    }
                    else if (typeName == "InquiBlackEro" || typeName.Contains("InquiBlackEro"))
                    {
                        return "BlackInquisitor";
                    }
                    else if (typeName == "Mutudeero" || typeName.Contains("Mutudeero"))
                    {
                        return "Mutude";
                    }
                    
                    // Обычные типы enemies (from GrabProtectionPatch)
                    else if (typeName == "TouzokuAxe" || typeName.Contains("TouzokuAxe"))
                    {
                        return "TouzokuAxe";
                    }
                    else if (typeName == "TouzokuNormal" || typeName.Contains("TouzokuNormal"))
                    {
                        return "Touzoku";
                    }
                    else if (typeName == "SinnerslaveCrossbow" || typeName.Contains("SinnerslaveCrossbow"))
                    {
                        return "SinnerslaveCrossbow";
                    }
                    else if (typeName == "Kakash" || typeName == "global::Kakash" || typeName.Contains("Kakash"))
                    {
                        return "Kakasi";
                    }
                    else if (typeName == "goblin" || typeName.Contains("goblin"))
                    {
                        return "Goblin";
                    }
                    else if (typeName == "Mutude" || typeName.Contains("Mutude"))
                    {
                        return "Mutude";
                    }
                    
                    // Inquisition типы (может быть BlackInquisitor)
                    else if (typeName.Contains("Inquisition") || typeName.Contains("InquisitionBlack"))
                    {
                        // Проверяем конкретbut BlackInquisitor
                        if (typeName.Contains("Black") || typeName.Contains("InquiBlack"))
                        {
                            return "BlackInquisitor";
                        }
                    }
                    
                }
            }
        }
        catch (System.Exception ex)
        {
        }
        
        return null; // Возвращаем null for use общtheir phrases
    }

    internal static bool IsInitialized => _initialized;
}

