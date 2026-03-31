using System;
using System.Reflection;
using HarmonyLib;
using Rewired;
using UnityEngine;
using UnityEngine.UI;
using NoREroMod.Patches.UI.MindBroken;
using DarkTonic.MasterAudio;

namespace NoREroMod;

/// <summary>
/// QTE System 3.0 - Simplified struggle system.
/// Triggered by "Struggle Out!" event from NoREroMod.
/// </summary>
public static class QTESystem {
    
    // ========== Events for other systems ==========
    
    /// <summary>
    /// Combo milestone event (x10, x20, x30...) - for future stages.
    /// </summary>
    public static event System.Action<int> OnQTEComboMilestone;
    
    /// <summary>
    /// Wrong input event (for Dialogue system) - for future stages.
    /// </summary>
    public static event System.Action OnQTEWrong;
    
    // ========== QTE State ==========
    
    /// <summary>
    /// Whether QTE system is active.
    /// </summary>
    private static bool isQTEActive = false;
    
    /// <summary>
    /// Whether "Struggle Out!" label is visible (main trigger).
    /// </summary>
    private static bool isStruggleOutVisible = false;
    
    
    /// <summary>
    /// Current player status.
    /// </summary>
    private static PlayerStatus currentPlayerStatus;
    
    /// <summary>
    /// Current playercon.
    /// </summary>
    private static playercon currentPlayerCon;
    
    /// <summary>
    /// Rewired Player for input handling.
    /// </summary>
    private static Player rewiredPlayer;
    
    /// <summary>
    /// Current enemy instance (for Camera system).
    /// </summary>
    private static object currentEnemyInstance;
    
    /// <summary>
    /// Reflection field for accessing PlayerConPatch.inPraymaidenStruggle.
    /// </summary>
    private static System.Reflection.FieldInfo inPraymaidenStruggleField;
    private static bool triedResolvePraymaidenField = false;
    
    // ========== UI Elements ==========
    
    /// <summary>
    /// Canvas for QTE elements.
    /// </summary>
    private static GameObject qteCanvas;
    
    /// <summary>
    /// Canvas RectTransform.
    /// </summary>
    private static RectTransform qteCanvasRect;
    
    /// <summary>
    /// Main camera.
    /// </summary>
    private static Camera mainCamera;
    
    // ========== QTE Buttons ==========
    
    /// <summary>
    /// Button size in pixels (reduced by 30% from original 80px).
    /// </summary>
    private const float BUTTON_SIZE = 56f; // 80 * 0.7 = 56
    
    /// <summary>
    /// Button spacing in horizontal row (configurable).
    /// </summary>
    private static float ButtonSpacing => Plugin.qteButtonSpacing?.Value ?? 100f;
    
    /// <summary>
    /// Left button (A).
    /// </summary>
    private static GameObject leftButton;
    
    /// <summary>
    /// Right button (D).
    /// </summary>
    private static GameObject rightButton;
    
    /// <summary>
    /// Up button (W).
    /// </summary>
    private static GameObject upButton;
    
    /// <summary>
    /// Down button (S).
    /// </summary>
    private static GameObject downButton;
    
    /// <summary>
    /// Glowing status bar above buttons (green when open, red when closed).
    /// </summary>
    private static GameObject statusBar;
    
    /// <summary>
    /// Arrow sprites cache.
    /// </summary>
    private static System.Collections.Generic.Dictionary<KeyCode, Sprite> arrowSprites = 
        new System.Collections.Generic.Dictionary<KeyCode, Sprite>();
    
    /// <summary>
    /// Saved center-top position in Canvas coordinates (set once on start).
    /// Used for fixed button positioning.
    /// </summary>
    private static Vector2? savedAnimationCenterCanvas = null;
    
    // ========== Penalties (STAGE 6) ==========
    
    /// <summary>
    /// Penalty percent MP from max (from config)
    /// </summary>
    private static float mpPenaltyPercent => Plugin.qteMPPenaltyPercent?.Value ?? 0.3f;
    
    /// <summary>
    /// Min duration QTE window (from config)
    /// </summary>
    private static float minWindowDuration => Plugin.qteWindowDurationMin?.Value ?? 2f;
    
    /// <summary>
    /// Max duration QTE window (from config)
    /// </summary>
    private static float maxWindowDuration => Plugin.qteWindowDurationMax?.Value ?? 3.5f;
    
    /// <summary>
    /// Min cooldown between windows (from config)
    /// </summary>
    private static float minCooldownDuration => Plugin.qteCooldownDurationMin?.Value ?? 2f;
    
    /// <summary>
    /// Max cooldown between windows (from config)
    /// </summary>
    private static float maxCooldownDuration => Plugin.qteCooldownDurationMax?.Value ?? 4f;
    
    
    // ========== Buttons up/down (STAGE 8) ==========
    
    /// <summary>
    /// Timer for alternating button colors up/down
    /// </summary>
    private static float upDownColorTimer = 0f;
    
    /// <summary>
    /// Color change interval (from config)
    /// </summary>
    private static float colorChangeInterval => Plugin.qteColorChangeInterval?.Value ?? 1f;
    
    /// <summary>
    /// Current color up button (true = yellow, false = red)
    /// </summary>
    private static bool isUpYellow = true; // Start with yellow
    
    /// <summary>
    /// Current color down button (true = yellow, false = red)
    /// </summary>
    private static bool isDownYellow = false; // Start with red (opposite of up)
    
    // ========== Combo system (STAGE 10) ==========
    
    /// <summary>
    /// Correct press counter presses on yellow button
    /// </summary>
    private static int yellowButtonCombo = 0;
    
    /// <summary>
    /// Combo threshold for bonus activation (from config)
    /// </summary>
    private static int COMBO_MILESTONE => Plugin.qteComboMilestone?.Value ?? 10;
    
    /// <summary>
    /// Is combo bonus active combo (after x10)
    /// </summary>
    private static bool isComboBonusActive = false;
    
    // ========== Visual difficulty (STAGE 11) ==========
    
    /// <summary>
    /// Max transparency of buttons at 100% MB (from config)
    /// </summary>
    
    
    // ========== Sounds (STAGE 12) ==========
    
    /// <summary>
    /// Successful press sounds press (from archive)
    /// </summary>
    private static readonly string[] SuccessKeyPressSoundIds = {
        "dame_ahaa",
        "dame_hguu",
        "dame_kuu",
        "dame_kuhuu",
        "dame_ugu",
        "vo_damage_1"
    };
    
    /// <summary>
    /// Successful press sounds press on yellow button (bonus)
    /// </summary>
    private static readonly string[] YellowButtonSuccessSoundIds = {
        "dame_3",
        "dame_ahaa",
        "vo_damage_1"
    };
    
    /// <summary>
    /// Sound volume (used from config via multipliers)
    /// </summary>
    private static float SOUND_VOLUME => 1f; // Base value, multipliers applied separately
    
    /// <summary>
    /// Dictionary for tracking previous key states (STAGE 5)
    /// </summary>
    private static System.Collections.Generic.Dictionary<KeyCode, bool> previousKeyStates = 
        new System.Collections.Generic.Dictionary<KeyCode, bool>();
    
    // ========== QTE window state (STAGE 4) ==========
    
    /// <summary>
    /// Is QTE window active for left/right buttons (A/D)
    /// </summary>
    private static bool isWindowActiveLeftRight = false;
    
    /// <summary>
    /// Active window timer for A/D
    /// </summary>
    private static float windowTimerLeftRight = 0f;
    
    /// <summary>
    /// Active window duration for A/D (2-3.5 sec, random)
    /// </summary>
    private static float windowDurationLeftRight = 0f;
    
    /// <summary>
    /// Cooldown timer for A/D
    /// </summary>
    private static float cooldownTimerLeftRight = 0f;
    
    /// <summary>
    /// Cooldown duration for A/D (2-4 sec, random)
    /// </summary>
    private static float cooldownDurationLeftRight = 0f;
    
    /// <summary>
    /// Is QTE window active for up/down buttons (W/S)
    /// </summary>
    private static bool isWindowActiveUpDown = false;
    
    /// <summary>
    /// Active window timer for W/S
    /// </summary>
    private static float windowTimerUpDown = 0f;
    
    /// <summary>
    /// Active window duration for W/S (2-3.5 sec, random)
    /// </summary>
    private static float windowDurationUpDown = 0f;
    
    /// <summary>
    /// Cooldown timer for W/S
    /// </summary>
    private static float cooldownTimerUpDown = 0f;
    
    /// <summary>
    /// Cooldown duration for W/S (2-4 sec, random)
    /// </summary>
    private static float cooldownDurationUpDown = 0f;
    
    
    // ========== Visual press indication ==========
    
    /// <summary>
    /// Duration of green indication on press (from config)
    /// </summary>
    private static float pressIndicatorDuration => Plugin.qtePressIndicatorDuration?.Value ?? 0.15f;
    
    /// <summary>
    /// Timers for button color reset to original state
    /// </summary>
    private static float leftButtonColorTimer = 0f;
    private static float rightButtonColorTimer = 0f;
    
    /// <summary>
    /// Timers for button color reset for up/down buttons after visual indication (STAGE 9)
    /// </summary>
    private static float upButtonColorTimer = 0f;
    private static float downButtonColorTimer = 0f;
    
    
    // ========== Public methods for access to Canvas ==========
    
    /// <summary>
    /// Gets RectTransform Canvas for QTE (for use in positioning classes)
    /// </summary>
    public static RectTransform GetQTECanvasRect()
    {
        if (qteCanvasRect == null && qteCanvas != null)
        {
            qteCanvasRect = qteCanvas.GetComponent<RectTransform>();
        }
        return qteCanvasRect;
    }
    
    /// <summary>
    /// Gets GameObject Canvas for QTE (for use in positioning classes)
    /// </summary>
    public static GameObject GetQTECanvas()
    {
        return qteCanvas;
    }
    
    // ========== Logging ==========
    // Logging disabled (can be re-enabled, by uncommenting code)
    
    private static void LogInfo(string message) {
        // Disabled: too many logs
        // Plugin.Log?.LogInfo($"[QTE 3.0] {message}");
    }
    
    private static void LogWarning(string message) {
        // Disabled: too many logs
        // Plugin.Log?.LogWarning($"[QTE 3.0] {message}");
    }
    
    private static void LogError(string message) {
        // Keep only errors
        Plugin.Log?.LogError($"[QTE 3.0] {message}");
    }
    
    // ========== Visibility check "Struggle Out!" ==========
    
    /// <summary>
    /// Gets value PlayerConPatch.inPraymaidenStruggle via reflection
    /// </summary>
    private static bool IsInPraymaidenStruggle() {
        try {
            var field = GetInPraymaidenField();
            if (field == null) return false;
            return field.GetValue(null) is bool value && value;
        } catch (Exception ex) {
            LogWarning($"Failed to read inPraymaidenStruggle: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Gets FieldInfo for PlayerConPatch.inPraymaidenStruggle via reflection
    /// </summary>
    private static System.Reflection.FieldInfo GetInPraymaidenField() {
        if (!triedResolvePraymaidenField) {
            triedResolvePraymaidenField = true;
            var type = AccessTools.TypeByName("NoREroMod.PlayerConPatch");
            if (type != null) {
                inPraymaidenStruggleField = AccessTools.Field(type, "inPraymaidenStruggle");
            }
        }
        return inPraymaidenStruggleField;
    }
    
    /// <summary>
    /// Checks if label should be visible "Struggle Out!"
    /// Uses same conditions as UImngPatch.ShowStruggleWindowMessage
    /// FIX: Ignores _easyESC when QTE already active (for fade effects white inquisitor)
    /// </summary>
    private static bool CheckStruggleOutVisibility(playercon playerCon, PlayerStatus playerStatus) {
        if (playerCon == null || playerStatus == null) {
            return false;
        }
        
        // FIX: If QTE already active, ignore _easyESC (temporary fade effects should not stop QTE)
        // But check base conditions (erodown, eroflag, _SOUSA) for actual closing struggle window
        bool ignoreEasyESC = isQTEActive;
        
        // Conditions from UImngPatch.cs line 59-61
        bool shouldBeVisible = 
            playerCon.erodown != 0 && 
            (
                (playerCon.eroflag && (ignoreEasyESC || !playerCon._easyESC) && playerStatus._SOUSA &&
                 (StruggleSystem.isValidStruggleWindow() || playerStatus.Sp >= playerStatus.AllMaxSP())) ||
                IsInPraymaidenStruggle()
            );
        
        return shouldBeVisible;
    }
    
    // ========== QTE control ==========
    
    /// <summary>
    /// Starts QTE system
    /// </summary>
    public static void StartQTE(PlayerStatus playerStatus, playercon playerCon) {
        // Check: QTE system disabled in settings
        if (!(Plugin.enableQTESystem?.Value ?? true)) {
            return; // QTE system disabled
        }
        
        if (isQTEActive) {
            LogWarning("QTE already active, skipping StartQTE");
            return;
        }
        
        
        currentPlayerStatus = playerStatus;
        currentPlayerCon = playerCon;
        isQTEActive = true;
        isStruggleOutVisible = true;
        
        // Initialize UI (Canvas)
        InitializeUI();
        
        // Try get position from customization for different enemies
        Vector2? customPosition = null;
        
        // If currentEnemyInstance not set, try find it automatically
        // CRITICAL FIX: Find active ERO component instead of EnemyDate
        // Active ERO component = enemy that grabbed player
        if (currentEnemyInstance == null)
        {
            try
            {
                
                var assembly = System.Reflection.Assembly.GetAssembly(typeof(EnemyDate));
                if (assembly != null)
                {
                    // Full list of all ERO classes for search
                    string[] eroTypeNames = { 
                        "InquiBlackEro",           // black inquisitor
                        "InquisitionWhiteERO",     // white inquisitor
                        "InquisitionRedERO",       // red inquisitor
                        "GAInquisitionWhiteERO",   // GA version white inquisitor
                        "GAInquisitionRedERO",    // GA version red inquisitor
                        "EroTouzoku",             // TouzokuNormal
                        "EroTouzokuAXE",          // TouzokuAxe
                        "GAEroTouzoku",           // GA TouzokuNormal
                        "GAEroTouzokuAXE",        // GA TouzokuAxe
                        "goblinero",              // Goblin
                        "SinnerslaveCrossbowERO", // Dorei
                        "Kakash",                 // Kakasi (no ERO class, use base)
                        "Mutudeero",              // Mutude
                        "SlaveBigAxeEro",         // SlaveBigAxe
                        "EroAnimation_suraimu",   // suraimu/sraimu
                        "CocoonmanERO",           // CocoonmanStart
                        "ERODemonRequiemKnight",  // requiemKnight
                        "CrawlingDeadERO",        // CrawlingDead_spine (not only GA version)
                        "AngelStatue_ERO",        // angel_Statue
                        "GAAngelStatue_ERO",      // GA AngelStatue
                        "BigoniERO",              // BigoniBrother
                        "StartBigoniERO",         // BigoniBrother (start version)
                        "TyoukyoushiERO",         // Tyoukyoushi
                        "StartTyoukyoushiERO",    // Tyoukyoushi_Red (uses StartTyoukyoushiERO)
                        "GAtyoukyoushiERO",       // GA Tyoukyoushi
                        "PraymaidenERO",          // Praymaiden
                        "StartPraymaidenERO",     // Praymaiden (start version)
                        "PilgrimERO",             // Pilgrim
                        "GAPilgrimERO",           // GA Pilgrim
                        "UndeadERO",              // Undead
                        "GAundeadERO",            // GA Undead
                        "MummyManERO",            // MummyMan
                        "GAMummyManERO",          // GA MummyMan
                        "MummyDogERO",            // MummyDog
                        "GAMummyDogERO",          // GA MummyDog
                        "LibrarianERO",           // Librarian
                        "GALibrarianERO",         // GA Librarian
                        "MinotaurosuERO",         // Minotaurosu
                        "GAminotaurosuERO",       // GA Minotaurosu
                        "GorotukiERO",            // Gorotuki
                        "GAgorotukiERO",          // GA Gorotuki
                        "KinokoERO",              // Kinoko
                        "MushroomERO",            // Mushroom
                        "GAMushroomERO",          // GA Mushroom
                        "SnailshellERO",          // Snailshell
                        "GASnailshellERO",        // GA Snailshell
                        "SisiruiruiERO",          // Sisiruirui
                        "GAsisiruiruiERO",        // GA Sisiruirui
                        "VagrantERO",             // Vagrant
                        "VagrantMainERO",         // VagrantMain
                        "GAvagrantMainERO",       // GA VagrantMain
                        "PrisonOfficerERO",       // PrisonOfficer
                        "GAPrisonOffecerERO",     // GA PrisonOfficer
                        "skeltonOozeERO",         // SkeltonOoze
                        "GAskeltonOozeERO",       // GA SkeltonOoze
                        "SuccubusERO",            // Succubus
                        "GASuccubusERO",          // GA Succubus
                        "CrawlingSisterKnightERO", // CrawlingSisterKnight
                        "SisterKnightEro",        // Sisterknight
                        "SisterMobEroNoDialog",   // Sisterknight (no dialogue)
                        "SisterMobero",           // Sisterknight (alternative)
                        "GACrawlingDeadERO",      // GA CrawlingDead
                        "MimickERO",              // Mimick
                        "GAmimickERO",            // GA Mimick
                        "Ivy_ERO",                // Ivy
                        "GAivy_ERO",              // GA Ivy
                        "GACrowInquisitionERO",   // GA CrowInquisition
                        "SlumToiletERO",          // SlumToilet
                        "GASlumToiletERO",        // GA SlumToilet
                        "MermanERO",              // Merman
                        "GobRiderERO",            // GobRider
                        "GobBigAlterERO",         // GobBigAlter
                        "BeastBellyERO",          // BeastBelly
                        "WallHipERO",             // WallHip
                        "TrapSpiderERO",          // TrapSpider
                        "GAspiderTrapERO",        // GA TrapSpider
                        "TrapMachineERO",         // TrapMachine
                        "GATrapMachineERO",       // GA TrapMachine
                        "Trap_RockinghorseERO",   // Trap_Rockinghorse
                        "GATrap_RockinghorseERO", // GA Trap_Rockinghorse
                        "Trap_TentacleIronmaidenERO", // Trap_TentacleIronmaiden
                        "PunishmentEROTentacleIronmaiden", // PunishmentEROTentacleIronmaiden
                        "PunishmentEROTentacleIronmaidenCandore", // PunishmentEROTentacleIronmaidenCandore
                        "Mob_rockinghorseERO",    // Mob_rockinghorse
                        "GABlackOozeTypeBERO",    // GA BlackOozeTypeB
                        "GAblackoozetrapERO",     // GA blackoozetrap
                        "GACocconBOSS_ERO",       // GA CocconBOSS
                        "LastIbaranoMajyoERO",    // LastIbaranoMajyo
                        "BlackMafiaERO",          // BlackMafia (mafia_spine)
                        "GABlackMafiaERO",        // GA BlackMafia
                        "GAMuscleMafiaERO",       // GA MuscleMafia
                        "GAcoolmaidenERO",        // GA coolmaiden
                        "GAArulauneERO",          // GA Arulaune
                        "GAibaraERO",             // GA ibara
                        "GAinoriERO",             // GA inori
                        "EvCandoreERO",           // EvCandore
                        "GABlackOozeTypeBERO"      // GA BlackOozeTypeB (duplicate but keep)
                    };
                    
                    // First find active ERO components (this is the grabbing enemy)
                    foreach (string eroTypeName in eroTypeNames)
                    {
                        var eroType = assembly.GetType(eroTypeName);
                        if (eroType != null)
                        {
                            // Find all ERO components of this type
                            var eroComponents = UnityEngine.Object.FindObjectsOfType(eroType);
                            if (eroComponents != null && eroComponents.Length > 0)
                            {
                                // Find active ERO component (activeSelf == true)
                                foreach (var eroComponent in eroComponents)
                                {
                                    MonoBehaviour mb = eroComponent as MonoBehaviour;
                                    if (mb != null && mb.gameObject.activeSelf)
                                    {
                                        // Found active ERO component! Now need find parent EnemyDate
                                        // ERO component is in erodata GameObject, which is child of EnemyDate
                                        Transform eroTransform = mb.transform;
                                        
                                        // Find parent EnemyDate
                                        Transform parent = eroTransform.parent;
                                        int maxDepth = 10; // Protection from infinite loop
                                        int depth = 0;
                                        while (parent != null && depth < maxDepth)
                                        {
                                            EnemyDate enemyDate = parent.GetComponent<EnemyDate>();
                                            if (enemyDate != null)
                                            {
                                                currentEnemyInstance = enemyDate;
                                                break;
                                            }
                                            parent = parent.parent;
                                            depth++;
                                        }
                                        
                                        // If not found EnemyDate via parent, use the ERO component
                                        if (currentEnemyInstance == null)
                                        {
                                            currentEnemyInstance = eroComponent;
                                        }
                                        
                                        if (currentEnemyInstance != null)
                                        {
                                            break;
                                        }
                                    }
                                }
                                
                                if (currentEnemyInstance != null)
                                {
                                    break;
                                }
                            }
                        }
                    }
                    
                    // If not found active ERO component, use old method search by EnemyDate
                    // (fallback for enemies without ERO components or if ERO not yet activated)
                    if (currentEnemyInstance == null)
                    {
                        
                        string[] enemyTypeNames = { 
                            "Inquisition",        // black inquisitor (base class)
                            "InquisitionWhite",   // white inquisitor (base class)
                            "InquisitionRED",     // red inquisitor (base class)
                            "EroTouzoku", "EroTouzokuAXE", 
                            "goblinero", 
                            "SinnerslaveCrossbowERO", 
                            "Kakash", 
                            "Mutudeero", 
                            "Mutude", 
                            "BigoniERO" 
                        };
                        
                        foreach (string typeName in enemyTypeNames)
                        {
                            var enemyType = assembly.GetType(typeName);
                            if (enemyType != null)
                            {
                                var enemyObjects = UnityEngine.Object.FindObjectsOfType(enemyType);
                                if (enemyObjects != null && enemyObjects.Length > 0)
                                {
                                    foreach (var enemy in enemyObjects)
                                    {
                                        MonoBehaviour mb = enemy as MonoBehaviour;
                                        if (mb != null && mb.gameObject.activeInHierarchy)
                                        {
                                            currentEnemyInstance = enemy;
                                            break;
                                        }
                                    }
                                    
                                    if (currentEnemyInstance != null)
                                    {
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    
                    if (currentEnemyInstance == null)
                    {
                        LogWarning("[QTE] Could not auto-find any enemy in scene");
                    }
                }
                else
                {
                    LogWarning("[QTE] Could not get Assembly for EnemyDate");
                }
            }
            catch (Exception ex)
            {
                LogWarning($"[QTE] Failed to auto-find enemy: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        // Use fixed position: center-bottom, 1020px from bottom
        // Anchor (0.5, 0) = bottom center, anchoredPosition.y = 1020 = 1020px from bottom
        savedAnimationCenterCanvas = new Vector2(0f, 1020f);
        
        // Ensure that Canvas active before creating buttons
        if (qteCanvas != null && !qteCanvas.activeSelf) {
            qteCanvas.SetActive(true);
        }
        
        // Create buttons left/right
        CreateButtons();
        
        // STAGE 5: Get Rewired Player for processing input
        if (playerCon != null) {
            try {
                var playerIdField = AccessTools.Field(typeof(playercon), "playerId");
                if (playerIdField != null) {
                    int playerId = (int)playerIdField.GetValue(playerCon);
                    rewiredPlayer = ReInput.players.GetPlayer(playerId);
                }
            } catch (Exception ex) {
                LogWarning($"Failed to get Rewired Player: {ex.Message}");
            }
        }
        
        // Fallback: use first player
        if (rewiredPlayer == null) {
            try {
                rewiredPlayer = ReInput.players.GetPlayer(0);
            } catch (Exception ex) {
                LogWarning($"Failed to get Rewired Player 0: {ex.Message}");
            }
        }
        
        // STAGE 4: Open first windows immediately (separately for A/D and W/S)
        OpenWindowLeftRight();
        OpenWindowUpDown();
        
    }
    
    /// <summary>
    /// Stops QTE system
    /// </summary>
    public static void StopQTE() {
        if (!isQTEActive) {
            return;
        }
        
        
        isQTEActive = false;
        isStruggleOutVisible = false;
        currentPlayerStatus = null;
        currentPlayerCon = null;
        rewiredPlayer = null; // STAGE 5: Reset Rewired Player
        currentEnemyInstance = null;
        savedAnimationCenterCanvas = null; // Reset saved position
        
        // Hide status bar
        if (statusBar != null) {
            statusBar.SetActive(false);
        }
        previousKeyStates.Clear(); // STAGE 5: Clear key states
        
        // STAGE 4: Close both windows
        CloseWindowLeftRight();
        CloseWindowUpDown();
        
        // STAGE 4: Reset window state
        isWindowActiveLeftRight = false;
        windowTimerLeftRight = 0f;
        windowDurationLeftRight = 0f;
        cooldownTimerLeftRight = 0f;
        cooldownDurationLeftRight = 0f;
        
        isWindowActiveUpDown = false;
        windowTimerUpDown = 0f;
        windowDurationUpDown = 0f;
        cooldownTimerUpDown = 0f;
        cooldownDurationUpDown = 0f;
        
        // Reset visual indication
        leftButtonColorTimer = 0f;
        rightButtonColorTimer = 0f;
        ResetButtonColors();
        
        // Hide Canvas and buttons
        if (qteCanvas != null) {
            qteCanvas.SetActive(false);
        }
        
        // Remove buttons
        DestroyButtons();
        
    }
    
    /// <summary>
    /// Updates QTE system each frame.
    /// </summary>
    public static void Update(PlayerStatus playerStatus, playercon playerCon) {
        if (MindBrokenBadEndSystem.IsBadEndActive) {
            if (isQTEActive) StopQTE();
            return;
        }
        if (playerStatus == null || playerCon == null) {
            if (isQTEActive) {
                StopQTE();
            }
            return;
        }
        
        // Update refs
        currentPlayerStatus = playerStatus;
        currentPlayerCon = playerCon;
        
        // Check visibility "Struggle Out!"
        bool shouldBeVisible = CheckStruggleOutVisibility(playerCon, playerStatus);
        
        if (shouldBeVisible && !isQTEActive) {
            // "Struggle Out!" became visible → start QTE
            StartQTE(playerStatus, playerCon);
        } else if (!shouldBeVisible && isQTEActive) {
            // "Struggle Out!" hidden → stop QTE
            StopQTE();
        }
        
        // If QTE active, update state
        if (isQTEActive) {
            isStruggleOutVisible = shouldBeVisible;
            
            // STAGE 4: If "Struggle Out!" inactive → close QTE even during window
            if (!shouldBeVisible) {
                StopQTE();
                return;
            }
            
            // STAGE 4: Update window logic QTE (separately for A/D and W/S)
            UpdateQTEWindow();
            
            // Update status bar color based on window state
            UpdateStatusBarColor();
            
            // STAGE 8: Update alternation button colors up/down (always if buttons visible)
            if (isWindowActiveUpDown) {
                UpdateUpDownColorCycle();
            }
            
            // STAGE 11: Update visual difficulty (transparency and pink shadow)
            
            
            // STAGE 5: Process press keys (only during active windows)
            if (isWindowActiveLeftRight || isWindowActiveUpDown) {
                ProcessInput();
            }

            // If SP is full but vanilla did not auto-release this frame, force the same release path.
            // This prevents soft-lock grabs on specific enemies (e.g., Cocoonman variants).
            if (playerStatus.Sp >= playerStatus.AllMaxSP() && playerCon.erodown != 0)
            {
                if (TryExitHSceneViaVanillaFunNowdamage(playerCon, playerStatus))
                {
                    StopQTE();
                    return;
                }
            }
            
            // STAGE 6: Process wrong press during cooldowns
            if ((!isWindowActiveLeftRight && cooldownTimerLeftRight > 0f) || 
                (!isWindowActiveUpDown && cooldownTimerUpDown > 0f)) {
                ProcessWrongInput();
            }
            
            // Update visual indication presses (color reset)
            UpdateButtonColorIndicators();
            
            // Update UI (show/hide Canvas)
            if (qteCanvas != null) {
                // CRITICAL FIX: Force activate Canvas, if QTE active
                // This protection from black background system, which can hide Canvas after its creation
                if (shouldBeVisible && !qteCanvas.activeSelf) {
                    qteCanvas.SetActive(true);
                }
                qteCanvas.SetActive(shouldBeVisible);
                
                // FIX: Activate buttons only if corresponding windows active
                // Canvas always active when QTE active, but buttons shown only during windows
                if (shouldBeVisible && isQTEActive) {
                    // Check Canvas component
                    Canvas canvas = qteCanvas.GetComponent<Canvas>();
                    if (canvas != null && !canvas.enabled) {
                        canvas.enabled = true;
                    }
                    
                    // Activate buttons A/D only if window A/D active
                    if (isWindowActiveLeftRight) {
                        if (leftButton != null && !leftButton.activeSelf) {
                            leftButton.SetActive(true);
                            Image leftImage = leftButton.GetComponent<Image>();
                            if (leftImage != null && !leftImage.enabled) {
                                leftImage.enabled = true;
                            }
                        }
                        if (rightButton != null && !rightButton.activeSelf) {
                            rightButton.SetActive(true);
                            Image rightImage = rightButton.GetComponent<Image>();
                            if (rightImage != null && !rightImage.enabled) {
                                rightImage.enabled = true;
                            }
                        }
                    } else {
                        // Window A/D closed - ensure buttons hidden
                        if (leftButton != null && leftButton.activeSelf) {
                            leftButton.SetActive(false);
                        }
                        if (rightButton != null && rightButton.activeSelf) {
                            rightButton.SetActive(false);
                        }
                    }
                    
                    // Activate buttons W/S only if window W/S active
                    if (isWindowActiveUpDown) {
                        if (upButton != null && !upButton.activeSelf) {
                            upButton.SetActive(true);
                            Image upImage = upButton.GetComponent<Image>();
                            if (upImage != null && !upImage.enabled) {
                                upImage.enabled = true;
                            }
                        }
                        if (downButton != null && !downButton.activeSelf) {
                            downButton.SetActive(true);
                            Image downImage = downButton.GetComponent<Image>();
                            if (downImage != null && !downImage.enabled) {
                                downImage.enabled = true;
                            }
                        }
                    } else {
                        // Window W/S closed - ensure buttons hidden
                        if (upButton != null && upButton.activeSelf) {
                            upButton.SetActive(false);
                        }
                        if (downButton != null && downButton.activeSelf) {
                            downButton.SetActive(false);
                        }
                    }
                }
            }
        }
    }
    
    // ========== UI initialization ==========
    
    /// <summary>
    /// Initializes Canvas for QTE elements
    /// </summary>
    private static void InitializeUI() {
        if (qteCanvas != null) {
            // Canvas already created
            return;
        }
        
        
        // Find main camera
        mainCamera = Camera.main;
        if (mainCamera == null) {
            mainCamera = UnityEngine.Object.FindObjectOfType<Camera>();
        }
        
        if (mainCamera == null) {
            LogError("Cannot find main camera!");
            return;
        }
        
        // Create Canvas (Screen Space Overlay - independent of camera)
        GameObject canvasGO = new GameObject("QTECanvas3");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; // DO NOT depends on camera!
        canvas.sortingOrder = 9999; // Max priority
        
        // Important: do NOT attach Canvas to camera
        canvas.worldCamera = null; // Explicitly disable attachment to camera for ScreenSpaceOverlay
        
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        
        canvasGO.AddComponent<GraphicRaycaster>();
        
        // Important: do NOT use DontDestroyOnLoad, so that Canvas not saved between scenes
        // But can use for persistence in one scene
        // UnityEngine.Object.DontDestroyOnLoad(canvasGO);
        
        qteCanvas = canvasGO;
        qteCanvasRect = canvasGO.GetComponent<RectTransform>();
        
        // Canvas should cover full screen, but coordinates independent of camera
        qteCanvasRect.anchorMin = Vector2.zero;
        qteCanvasRect.anchorMax = Vector2.one;
        qteCanvasRect.sizeDelta = Vector2.zero;
        
        // Ensure that Canvas does not follow camera
        canvasGO.transform.position = Vector3.zero;
        canvasGO.transform.rotation = Quaternion.identity;
        canvasGO.transform.localScale = Vector3.one;
        
        LogInfo("Canvas created with ScreenSpaceOverlay (independent from camera)");
        
        // Canvas should be active to display buttons
        // But we will control visibility via SetActive in Update
        qteCanvas.SetActive(true);
        
        // CRITICAL FIX: Ensure that Canvas not hidden black background system
        // Check again via small delay, so that black background system had time to process
        // This protection from situation when Canvas is created AFTER activation black background system
        if (qteCanvas != null && !qteCanvas.activeSelf) {
            qteCanvas.SetActive(true);
            LogInfo("[QTE] Canvas was hidden after creation - forced reactivation");
        }
        
        LogInfo("QTE Canvas initialized successfully (active: true)");
    }
    
    
    /// <summary>
    /// Checks if QTE system
    /// </summary>
    public static bool IsQTEActive() {
        return isQTEActive;
    }
    
    /// <summary>
    /// Force stops QTE (for cleanup on restart etc.)
    /// </summary>
    public static void ForceStopQTE() {
        StopQTE();
    }
    
    /// <summary>
    /// Get current instance enemy (for Camera system)
    /// </summary>
    public static object GetCurrentEnemyInstance() {
        return currentEnemyInstance;
    }
    
    /// <summary>
    /// Get current enemy (for Camera system)
    /// </summary>
    public static string GetCurrentEnemyName() {
        if (currentEnemyInstance == null) {
            return null;
        }
        
        return MapEnemyTypeToName(currentEnemyInstance.GetType().Name);
    }
    
    /// <summary>
    /// Type mapping enemy to name (for use other systems)
    /// </summary>
    private static string MapEnemyTypeToName(string typeName) {
        if (string.IsNullOrEmpty(typeName)) {
            return null;
        }
        
        if (typeName == "EroTouzokuAXE" || typeName.Contains("TouzokuAXE"))
            return "TouzokuAxe";
        else if (typeName == "EroTouzoku" || typeName.Contains("EroTouzoku"))
            return "Touzoku";
        else if (typeName == "SinnerslaveCrossbowERO" || typeName.Contains("SinnerslaveCrossbow"))
            return "SinnerslaveCrossbow";
        else if (typeName == "kakashi_ero2" || typeName.Contains("Kakasi") || typeName.Contains("Kakash"))
            return "Kakasi";
        else if (typeName == "goblinero" || typeName.Contains("Goblin"))
            return "Goblin";
        else if (typeName == "Mutudeero" || typeName == "Mutude" || typeName.Contains("Mutude"))
            return "Mutude";
        else if (typeName == "TouzokuAxe")
            return "TouzokuAxe";
        else if (typeName == "TouzokuNormal")
            return "Touzoku";
        else if (typeName == "SinnerslaveCrossbow")
            return "SinnerslaveCrossbow";
        else if (typeName == "Kakash" || typeName == "global::Kakash")
            return "Kakasi";
        else if (typeName == "goblin")
            return "Goblin";
        
        return null;
    }
    
    /// <summary>
    /// Set current enemy (for compatibility, will be used in next stages)
    /// </summary>
    public static void SetCurrentEnemy(object enemyInstance) {
        currentEnemyInstance = enemyInstance;
    }
    
    /// <summary>
    /// Clear current enemy (for compatibility)
    /// </summary>
    public static void ClearCurrentEnemy() {
        currentEnemyInstance = null;
    }
    
    // ========== Creation and control buttons ==========
    
    /// <summary>
    /// Creates all QTE buttons (Left, Up, Down, Right).
    /// </summary>
    private static void CreateButtons() {
        if (qteCanvasRect == null) {
            LogError("Cannot create buttons: Canvas not initialized");
            return;
        }
        
        LogInfo("Creating QTE buttons (Left/Up/Down/Right)...");
        
        // Create left button (A)
        leftButton = CreateButton(KeyCode.A, "QTEButton_Left");
        if (leftButton != null) {
            LogInfo("Left button created");
        }
        
        // Create right button (D)
        rightButton = CreateButton(KeyCode.D, "QTEButton_Right");
        if (rightButton != null) {
            LogInfo("Right button created");
        }
        
        // Create up button (W)
        upButton = CreateButton(KeyCode.W, "QTEButton_Up");
        if (upButton != null) {
            LogInfo("Up button created");
        }
        
        // Create down button (S)
        downButton = CreateButton(KeyCode.S, "QTEButton_Down");
        if (downButton != null) {
            LogInfo("Down button created");
        }
        
        // Update positions immediately (using saved center-top position)
        UpdateButtonPositions();
        
        // Create status bar
        CreateStatusBar();
    }
    
    /// <summary>
    /// Creates single button with arrow sprite.
    /// </summary>
    private static GameObject CreateButton(KeyCode key, string name) {
        try {
            GameObject buttonGO = new GameObject(name);
            buttonGO.transform.SetParent(qteCanvasRect, false);
            
            RectTransform rect = buttonGO.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(BUTTON_SIZE, BUTTON_SIZE);
            // Use bottom-center anchor for positioning from bottom
            rect.anchorMin = new Vector2(0.5f, 0f); // Bottom center
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f); // Center of sprite
            rect.anchoredPosition = Vector2.zero; // Temporary, updated in UpdateButtonPositions
            
            // Create Image to display arrow
            Image arrowImage = buttonGO.AddComponent<Image>();
            arrowImage.sprite = CreateArrowSprite(key);
            arrowImage.preserveAspect = true;
            arrowImage.color = Color.white; // White color by default
            
            // Add Outline for readability (black outline)
            Outline outline = buttonGO.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(4f, -4f);
            
            // Add Shadow for additional visibility
            Shadow shadow = buttonGO.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(2f, -2f);
            
            // Button created, but will be shown only on opening corresponding window
            // Do NOT set SetActive(true) here - this is done in OpenWindowLeftRight/OpenWindowUpDown
            buttonGO.SetActive(false); // Hidden by default
            
            LogInfo($"Button {name} created successfully (active: {buttonGO.activeSelf}, visible: {buttonGO.activeInHierarchy})");
            return buttonGO;
        } catch (Exception ex) {
            LogError($"Failed to create button {name}: {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }
    
    /// <summary>
    /// Creates arrow sprite programmatically.
    /// </summary>
    private static Sprite CreateArrowSprite(KeyCode key) {
        if (arrowSprites.ContainsKey(key)) {
            return arrowSprites[key];
        }
        
        // Map WASD to corresponding arrow keys for display
        KeyCode arrowKey = key;
        if (key == KeyCode.W) arrowKey = KeyCode.UpArrow;
        else if (key == KeyCode.S) arrowKey = KeyCode.DownArrow;
        else if (key == KeyCode.A) arrowKey = KeyCode.LeftArrow;
        else if (key == KeyCode.D) arrowKey = KeyCode.RightArrow;
        
        // If sprite for arrow already created, use it
        if (arrowSprites.ContainsKey(arrowKey)) {
            arrowSprites[key] = arrowSprites[arrowKey];
            return arrowSprites[key];
        }
        
        int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        
        // Fill with transparent
        for (int i = 0; i < pixels.Length; i++) {
            pixels[i] = Color.clear;
        }
        
        int centerX = size / 2;
        int centerY = size / 2;
        int triangleSize = 50; // Triangle size (half base width)
        int triangleHeight = 70; // Triangle height
        
        if (arrowKey == KeyCode.UpArrow) { // W -> Up ▲
            for (int y = centerY; y < centerY + triangleHeight; y++) {
                int distanceFromTop = y - centerY;
                int width = (int)(triangleSize * (1f - (float)distanceFromTop / triangleHeight));
                for (int x = centerX - width; x < centerX + width; x++) {
                    if (x >= 0 && x < size && y >= 0 && y < size) {
                        pixels[y * size + x] = Color.white;
                    }
                }
            }
        } else if (arrowKey == KeyCode.DownArrow) { // S -> Down ▼
            for (int y = centerY - triangleHeight; y < centerY; y++) {
                int distanceFromBottom = centerY - y;
                int width = (int)(triangleSize * (1f - (float)distanceFromBottom / triangleHeight));
                for (int x = centerX - width; x < centerX + width; x++) {
                    if (x >= 0 && x < size && y >= 0 && y < size) {
                        pixels[y * size + x] = Color.white;
                    }
                }
            }
        } else if (arrowKey == KeyCode.LeftArrow) { // A -> Left ◄
            for (int x = centerX - triangleHeight; x < centerX; x++) {
                int distanceFromRight = centerX - x;
                int height = (int)(triangleSize * (1f - (float)distanceFromRight / triangleHeight));
                for (int y = centerY - height; y < centerY + height; y++) {
                    if (x >= 0 && x < size && y >= 0 && y < size) {
                        pixels[y * size + x] = Color.white;
                    }
                }
            }
        } else if (arrowKey == KeyCode.RightArrow) { // D -> Right ►
            for (int x = centerX; x < centerX + triangleHeight; x++) {
                int distanceFromLeft = x - centerX;
                int height = (int)(triangleSize * (1f - (float)distanceFromLeft / triangleHeight));
                for (int y = centerY - height; y < centerY + height; y++) {
                    if (x >= 0 && x < size && y >= 0 && y < size) {
                        pixels[y * size + x] = Color.white;
                    }
                }
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        arrowSprites[key] = sprite;
        if (arrowKey != key) {
            arrowSprites[arrowKey] = sprite; // Cache for arrow key too
        }
        return sprite;
    }
    
    /// <summary>
    /// Updates button positions in horizontal row: left, up, down, right (close together).
    /// Uses fixed center-top position (configurable Y from top).
    /// </summary>
    private static void UpdateButtonPositions() {
        if (qteCanvasRect == null) {
            return;
        }
        
        // Use saved center-top position
        if (!savedAnimationCenterCanvas.HasValue) {
            LogWarning("Cannot update button positions: saved screen center is null");
            return;
        }
        
        Vector2 centerTopPos = savedAnimationCenterCanvas.Value;
        float spacing = ButtonSpacing;
        
        // Horizontal row: left, up, down, right
        // Left button: -1.5 * spacing (leftmost)
        if (leftButton != null) {
            RectTransform leftRect = leftButton.GetComponent<RectTransform>();
            if (leftRect != null) {
                leftRect.anchoredPosition = centerTopPos + new Vector2(-1.5f * spacing, 0f);
            }
        }
        
        // Up button: -0.5 * spacing (second from left)
        if (upButton != null) {
            RectTransform upRect = upButton.GetComponent<RectTransform>();
            if (upRect != null) {
                upRect.anchoredPosition = centerTopPos + new Vector2(-0.5f * spacing, 0f);
            }
        }
        
        // Down button: +0.5 * spacing (second from right)
        if (downButton != null) {
            RectTransform downRect = downButton.GetComponent<RectTransform>();
            if (downRect != null) {
                downRect.anchoredPosition = centerTopPos + new Vector2(0.5f * spacing, 0f);
            }
        }
        
        // Right button: +1.5 * spacing (rightmost)
        if (rightButton != null) {
            RectTransform rightRect = rightButton.GetComponent<RectTransform>();
            if (rightRect != null) {
                rightRect.anchoredPosition = centerTopPos + new Vector2(1.5f * spacing, 0f);
            }
        }
        
        // Update status bar position (at top of screen)
        if (statusBar != null) {
            RectTransform barRect = statusBar.GetComponent<RectTransform>();
            if (barRect != null) {
                // Position bar at top of screen (anchor 0.5, 1 = top center)
                // Use screen height - small offset from top
                float screenHeight = Screen.height;
                barRect.anchoredPosition = new Vector2(0f, -20f); // 20px from top
            }
        }
    }
    
    /// <summary>
    /// Creates glowing status bar at top of screen.
    /// </summary>
    private static void CreateStatusBar() {
        if (qteCanvasRect == null || statusBar != null) {
            return;
        }
        
        try {
            GameObject barGO = new GameObject("QTEStatusBar");
            barGO.transform.SetParent(qteCanvasRect, false);
            
            RectTransform rect = barGO.AddComponent<RectTransform>();
            // Bar width: covers all 4 buttons + spacing
            float barWidth = ButtonSpacing * 4f;
            rect.sizeDelta = new Vector2(barWidth, 20f); // 20px height (thicker)
            rect.anchorMin = new Vector2(0.5f, 1f); // Top center
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero; // Will be updated in UpdateButtonPositions
            
            // Create Image for the bar
            Image barImage = barGO.AddComponent<Image>();
            barImage.color = Color.green; // Default: green (windows open)
            
            // Add glow effect using Outline
            Outline outline = barGO.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 1f, 0f, 0.8f); // Green glow
            outline.effectDistance = new Vector2(2f, 2f);
            
            statusBar = barGO;
            statusBar.SetActive(false); // Hidden by default, shown when QTE active
            
            LogInfo("QTE Status bar created");
        } catch (Exception ex) {
            LogError($"Failed to create status bar: {ex.Message}\n{ex.StackTrace}");
        }
    }
    
    /// <summary>
    /// Updates status bar color: green when windows open, red when closed.
    /// </summary>
    private static void UpdateStatusBarColor() {
        if (statusBar == null) {
            return;
        }
        
        Image barImage = statusBar.GetComponent<Image>();
        Outline outline = statusBar.GetComponent<Outline>();
        
        if (barImage == null || outline == null) {
            return;
        }
        
        // Green when any window is open, red when both closed
        bool anyWindowOpen = isWindowActiveLeftRight || isWindowActiveUpDown;
        
        if (anyWindowOpen) {
            // Green: windows open
            barImage.color = Color.green;
            outline.effectColor = new Color(0f, 1f, 0f, 0.8f); // Green glow
        } else {
            // Red: windows closed
            barImage.color = Color.red;
            outline.effectColor = new Color(1f, 0f, 0f, 0.8f); // Red glow
        }
    }
    
    /// <summary>
    /// Destroys all buttons.
    /// </summary>
    private static void DestroyButtons() {
        if (leftButton != null) {
            UnityEngine.Object.Destroy(leftButton);
            leftButton = null;
        }
        
        if (rightButton != null) {
            UnityEngine.Object.Destroy(rightButton);
            rightButton = null;
        }
        
        // STAGE 8: Remove buttons up/down
        if (upButton != null) {
            UnityEngine.Object.Destroy(upButton);
            upButton = null;
        }
        
        if (downButton != null) {
            UnityEngine.Object.Destroy(downButton);
            downButton = null;
        }
        
        if (statusBar != null) {
            UnityEngine.Object.Destroy(statusBar);
            statusBar = null;
        }
        
    }
    
    // ========== QTE Window Logic ==========
    
    /// <summary>
    /// Updates QTE window logic each frame (separately for A/D and W/S).
    /// FIX: When QTE is active, windows work independently of isStruggleOutVisible.
    /// This makes QTE reliable and standard for all enemies.
    /// </summary>
    private static void UpdateQTEWindow() {
        // If QTE not active, don't update windows
        if (!isQTEActive) {
            return;
        }
        
        // FIX: When QTE is active, windows always work
        // Don't check isStruggleOutVisible, as it may temporarily become false
        // due to fade effects or other temporary enemy states
        
        // Update window for A/D (left/right)
        UpdateWindowLeftRight();
        
        // Update window for W/S (up/down)
        UpdateWindowUpDown();
    }
    
    /// <summary>
    /// Updates window logic for A/D buttons.
    /// </summary>
    private static void UpdateWindowLeftRight() {
        if (isWindowActiveLeftRight) {
            // Window active → update timer
            windowTimerLeftRight += Time.unscaledDeltaTime;
            
            // Check if window should close
            float timeRemaining = windowDurationLeftRight - windowTimerLeftRight;
            if (timeRemaining <= 0f) {
                CloseWindowLeftRight();
                StartCooldownLeftRight();
            }
        } else if (cooldownTimerLeftRight > 0f) {
            // Cooldown active → update timer
            cooldownTimerLeftRight -= Time.unscaledDeltaTime;
            
            // Cooldown expired → open new window
            if (cooldownTimerLeftRight <= 0f) {
                OpenWindowLeftRight();
            }
        } else {
            // First window → open immediately (if not already open)
            if (!isWindowActiveLeftRight && cooldownTimerLeftRight <= 0f) {
                OpenWindowLeftRight();
            }
        }
    }
    
    /// <summary>
    /// Updates window logic for W/S buttons.
    /// </summary>
    private static void UpdateWindowUpDown() {
        if (isWindowActiveUpDown) {
            // Window active → update timer
            windowTimerUpDown += Time.unscaledDeltaTime;
            
            // Check if window should close
            float timeRemaining = windowDurationUpDown - windowTimerUpDown;
            if (timeRemaining <= 0f) {
                CloseWindowUpDown();
                StartCooldownUpDown();
            }
        } else if (cooldownTimerUpDown > 0f) {
            // Cooldown active → update timer
            cooldownTimerUpDown -= Time.unscaledDeltaTime;
            
            // Cooldown expired → open new window
            if (cooldownTimerUpDown <= 0f) {
                OpenWindowUpDown();
            }
        } else {
            // First window → open immediately (if not already open)
            if (!isWindowActiveUpDown && cooldownTimerUpDown <= 0f) {
                OpenWindowUpDown();
            }
        }
    }
    
    /// <summary>
    /// Opens QTE window for A/D buttons (left/right).
    /// </summary>
    private static void OpenWindowLeftRight() {
        if (isWindowActiveLeftRight) {
            return; // Window already open
        }
        
        // Check if buttons are created
        if (leftButton == null || rightButton == null) {
            if (qteCanvasRect != null) {
                CreateButtons();
            } else {
                LogError("[QTE] Cannot create buttons: qteCanvasRect is null!");
                return;
            }
        }
        
        isWindowActiveLeftRight = true;
        windowTimerLeftRight = 0f;
        windowDurationLeftRight = UnityEngine.Random.Range(minWindowDuration, maxWindowDuration);
        
        // Ensure Canvas is active
        if (qteCanvas != null) {
            qteCanvas.SetActive(true);
            Canvas canvas = qteCanvas.GetComponent<Canvas>();
            if (canvas != null && !canvas.enabled) {
                canvas.enabled = true;
            }
        } else {
            LogError("[QTE] qteCanvas is null in OpenWindowLeftRight!");
            return;
        }
        
        // Show status bar and update color
        if (statusBar != null) {
            statusBar.SetActive(true);
            UpdateStatusBarColor();
        }
        
        // Show A/D buttons
        if (leftButton != null) {
            leftButton.SetActive(true);
            Image leftImage = leftButton.GetComponent<Image>();
            if (leftImage != null) {
                if (!leftImage.enabled) {
                    leftImage.enabled = true;
                }
                if (leftImage.color.a < 0.01f) {
                    Color newColor = leftImage.color;
                    newColor.a = 1f;
                    leftImage.color = newColor;
                }
            }
        } else {
            LogError("[QTE] leftButton is null in OpenWindowLeftRight!");
        }
        
        if (rightButton != null) {
            rightButton.SetActive(true);
            Image rightImage = rightButton.GetComponent<Image>();
            if (rightImage != null) {
                if (!rightImage.enabled) {
                    rightImage.enabled = true;
                }
                if (rightImage.color.a < 0.01f) {
                    Color newColor = rightImage.color;
                    newColor.a = 1f;
                    rightImage.color = newColor;
                }
            }
        } else {
            LogError("[QTE] rightButton is null in OpenWindowLeftRight!");
        }
    }
    
    /// <summary>
    /// Closes QTE window for A/D buttons (left/right).
    /// </summary>
    private static void CloseWindowLeftRight() {
        if (!isWindowActiveLeftRight) {
            return; // Window already closed
        }
        
        isWindowActiveLeftRight = false;
        windowTimerLeftRight = 0f;
        windowDurationLeftRight = 0f;
        
        // Hide A/D buttons
        if (leftButton != null) {
            leftButton.SetActive(false);
        }
        if (rightButton != null) {
            rightButton.SetActive(false);
        }
        
        // Update status bar color (check if any window still open)
        UpdateStatusBarColor();
        
    }
    
    /// <summary>
    /// Starts cooldown between windows for A/D.
    /// </summary>
    private static void StartCooldownLeftRight() {
        cooldownTimerLeftRight = UnityEngine.Random.Range(minCooldownDuration, maxCooldownDuration);
        cooldownDurationLeftRight = cooldownTimerLeftRight;
        
    }
    
    /// <summary>
    /// Opens QTE window for W/S buttons (up/down).
    /// </summary>
    private static void OpenWindowUpDown() {
        if (isWindowActiveUpDown) {
            return; // Window already open
        }
        
        // Check if buttons are created
        if (upButton == null || downButton == null) {
            if (qteCanvasRect != null) {
                CreateButtons();
            } else {
                LogError("[QTE] Cannot create buttons: qteCanvasRect is null!");
                return;
            }
        }
        
        isWindowActiveUpDown = true;
        windowTimerUpDown = 0f;
        windowDurationUpDown = UnityEngine.Random.Range(minWindowDuration, maxWindowDuration);
        
        // Ensure Canvas is active
        if (qteCanvas != null) {
            qteCanvas.SetActive(true);
            Canvas canvas = qteCanvas.GetComponent<Canvas>();
            if (canvas != null && !canvas.enabled) {
                canvas.enabled = true;
            }
        } else {
            LogError("[QTE] qteCanvas is null in OpenWindowUpDown!");
            return;
        }
        
        // Show status bar and update color
        if (statusBar != null) {
            statusBar.SetActive(true);
            UpdateStatusBarColor();
        }
        
        // Show W/S buttons
        if (upButton != null) {
            upButton.SetActive(true);
            Image upImage = upButton.GetComponent<Image>();
            if (upImage != null) {
                if (!upImage.enabled) {
                    upImage.enabled = true;
                }
                if (upImage.color.a < 0.01f) {
                    Color newColor = upImage.color;
                    newColor.a = 1f;
                    upImage.color = newColor;
                }
            }
        } else {
            LogError("[QTE] upButton is null in OpenWindowUpDown!");
        }
        
        if (downButton != null) {
            downButton.SetActive(true);
            Image downImage = downButton.GetComponent<Image>();
            if (downImage != null) {
                if (!downImage.enabled) {
                    downImage.enabled = true;
                }
                if (downImage.color.a < 0.01f) {
                    Color newColor = downImage.color;
                    newColor.a = 1f;
                    downImage.color = newColor;
                }
            }
        } else {
            LogError("[QTE] downButton is null in OpenWindowUpDown!");
        }
        
        // Reset color timer and start with alternation (up yellow, down red)
        upDownColorTimer = 0f;
        isUpYellow = true;
        isDownYellow = false;
        UpdateUpDownButtonColors();
    }
    
    /// <summary>
    /// Closes QTE window for W/S buttons (up/down).
    /// </summary>
    private static void CloseWindowUpDown() {
        if (!isWindowActiveUpDown) {
            return; // Window already closed
        }
        
        isWindowActiveUpDown = false;
        windowTimerUpDown = 0f;
        windowDurationUpDown = 0f;
        
        // Hide W/S buttons
        if (upButton != null) {
            upButton.SetActive(false);
        }
        if (downButton != null) {
            downButton.SetActive(false);
        }
        
        // Update status bar color (check if any window still open)
        UpdateStatusBarColor();
        
        LogInfo("QTE Window UpDown closed");
    }
    
    /// <summary>
    /// Starts cooldown between windows for W/S.
    /// </summary>
    private static void StartCooldownUpDown() {
        cooldownTimerUpDown = UnityEngine.Random.Range(minCooldownDuration, maxCooldownDuration);
        cooldownDurationUpDown = cooldownTimerUpDown;
        
    }
    
    // ========== Input Handling ==========
    
    /// <summary>
    /// Processes A/D and W/S key presses during active windows.
    /// </summary>
    private static void ProcessInput() {
        if (currentPlayerStatus == null) {
            return;
        }
        
        // G during QTE: Rage activation + vanilla H exit (tiered costs handled inside RageSystem).
        if (isQTEActive && IsKeyDown(KeyCode.G)) {
            TryActivateRageDuringQTE();
        }
        
        // Process A/D presses only if A/D window is active
        if (isWindowActiveLeftRight) {
            if (IsKeyDown(KeyCode.A)) {
                OnButtonPress(KeyCode.A);
            }
            
            if (IsKeyDown(KeyCode.D)) {
                OnButtonPress(KeyCode.D);
            }
        }
        
        // Process W/S presses only if W/S window is active
        if (isWindowActiveUpDown) {
            if (IsKeyDown(KeyCode.W)) {
                OnUpDownButtonPress(KeyCode.W, isUpYellow);
            }
            
            if (IsKeyDown(KeyCode.S)) {
                OnUpDownButtonPress(KeyCode.S, isDownYellow);
            }
        }
    }
    
    /// <summary>
    /// Same exit pipeline as NoREroMod when struggle fills SP, then one vanilla <see cref="playercon"/> tick:
    /// <see cref="StruggleSystem.startGrabInvul"/>, <c>UpdateStruggleHistory</c>, <c>downup = 1</c>, <c>key_submit = true</c>, invoke private <c>fun_nowdamage</c>.
    /// </summary>
    private static bool TryExitHSceneViaVanillaFunNowdamage(playercon pc, PlayerStatus ps) {
        if (pc == null || ps == null)
            return false;
        if (pc.erodown == 0)
            return true;
        if (!ps._SOUSA)
            return false;

        float maxSP = ps.AllMaxSP();
        try {
            var spField = AccessTools.Field(typeof(PlayerStatus), "Sp");
            if (spField != null)
                spField.SetValue(ps, maxSP);
            else {
                var spProperty = AccessTools.Property(typeof(PlayerStatus), "Sp");
                if (spProperty != null && spProperty.CanWrite)
                    spProperty.SetValue(ps, maxSP, null);
            }
        } catch (Exception ex) {
            LogWarning($"[QTE RAGE] Failed to set SP to max: {ex.Message}");
            return false;
        }

        StruggleSystem.startGrabInvul();

        try {
            var playerConPatchType = AccessTools.TypeByName("NoREroMod.PlayerConPatch");
            var prayField = playerConPatchType != null ? AccessTools.Field(playerConPatchType, "inPraymaidenStruggle") : null;
            prayField?.SetValue(null, false);
        } catch {
            // optional — same as NoREroMod branch on successful max SP
        }

        Time.timeScale = 1f;

        try {
            var playerConPatchType = AccessTools.TypeByName("NoREroMod.PlayerConPatch");
            var histMethod = playerConPatchType != null ? AccessTools.Method(playerConPatchType, "UpdateStruggleHistory") : null;
            histMethod?.Invoke(null, null);
        } catch (Exception ex) {
            LogWarning($"[QTE RAGE] UpdateStruggleHistory failed: {ex.Message}");
            StruggleSystem.removePlayerEasyStruggle();
        }

        try {
            Traverse.Create(pc).Field("downup").SetValue(1);
            Traverse.Create(pc).Field("key_submit").SetValue(true);
        } catch (Exception ex) {
            LogWarning($"[QTE RAGE] Failed to prime downup/key_submit: {ex.Message}");
            return false;
        }

        try {
            var fn = AccessTools.Method(typeof(playercon), "fun_nowdamage");
            if (fn == null) {
                LogWarning("[QTE RAGE] fun_nowdamage not found on playercon");
                return false;
            }
            fn.Invoke(pc, null);
        } catch (Exception ex) {
            LogWarning($"[QTE RAGE] fun_nowdamage invoke failed: {ex.Message}");
            return false;
        }

        return pc.erodown == 0;
    }
    
    /// <summary>
    /// Attempts to activate Rage during QTE and automatically frees player.
    /// </summary>
    private static void TryActivateRageDuringQTE() {
        try {
            if (!NoREroMod.Systems.Rage.RageSystem.Enabled) {
                return;
            }
            
            if (currentPlayerStatus == null || currentPlayerCon == null) {
                return;
            }
            
            if (NoREroMod.Systems.Rage.RageSystem.IsActive) {
                return;
            }
            
            float currentRage = NoREroMod.Systems.Rage.RageSystem.Percent;
            float legacyRageCostDuringQTE = Plugin.rageCostDuringQTE?.Value ?? 50f;
            float minRageForActivation = Plugin.rageTier2Threshold?.Value ?? 60f;
            
            if (currentRage < minRageForActivation) {
                LogWarning($"[QTE RAGE] Not enough rage for activation! Current: {currentRage:F1}%, required >= {minRageForActivation}%");
                return;
            }
            
            if (!NoREroMod.Systems.Rage.RageSystem.TryActivateForQteEscape(legacyRageCostDuringQTE)) {
                LogWarning($"[QTE RAGE] Failed to activate Rage (current rage: {currentRage:F1}%)");
                return;
            }
            
            if (!TryExitHSceneViaVanillaFunNowdamage(currentPlayerCon, currentPlayerStatus)) {
                LogWarning($"[QTE RAGE] Vanilla fun_nowdamage exit did not clear erodown (erodown={currentPlayerCon.erodown}). Rage cost was still spent.");
            }
            
            float currentAfterActivation = NoREroMod.Systems.Rage.RageSystem.Percent;
            float actualSpent = Mathf.Max(0f, currentRage - currentAfterActivation);
            LogInfo($"[QTE RAGE] Rage activated during QTE! Spent {actualSpent:F1}% rage (was {currentRage:F1}%, now {currentAfterActivation:F1}%).");
        } catch (Exception ex) {
            LogWarning($"[QTE RAGE] Error activating Rage during QTE: {ex.Message}\n{ex.StackTrace}");
        }
    }
    
    /// <summary>
    /// Handles press QTE button
    /// </summary>
    private static void OnButtonPress(KeyCode key) {
        if (currentPlayerStatus == null) {
            return;
        }
        
        // Calculate SP gain considering MindBroken (1.6% → 0.2%)
        float spGain = QTESPCalculator.CalculateSPGain();
        
        // Apply SP gain to player
        float currentSP = currentPlayerStatus.Sp;
        float maxSP = currentPlayerStatus.AllMaxSP();
        float newSP = Mathf.Min(currentSP + (maxSP * spGain), maxSP);
        
        // Set new value SP via reflection (as Sp may be property)
        try {
            var spField = AccessTools.Field(typeof(PlayerStatus), "Sp");
            if (spField != null) {
                spField.SetValue(currentPlayerStatus, newSP);
            } else {
                // Fallback: try property
                var spProperty = AccessTools.Property(typeof(PlayerStatus), "Sp");
                if (spProperty != null && spProperty.CanWrite) {
                    spProperty.SetValue(currentPlayerStatus, newSP, null);
                }
            }
        } catch (Exception ex) {
            LogWarning($"Failed to set SP: {ex.Message}");
        }
        
        LogInfo($"QTE Button pressed: {key}, SP gain: +{spGain * 100f:F1}% (new SP: {newSP:F1}/{maxSP:F1})");
        
        // Visual indication: green color on press
        if (key == KeyCode.A) {
            SetButtonColor(leftButton, Color.green);
            leftButtonColorTimer = pressIndicatorDuration;
        } else if (key == KeyCode.D) {
            SetButtonColor(rightButton, Color.green);
            rightButtonColorTimer = pressIndicatorDuration;
        }
        
        // TODO STAGE 5: Success sound (from archive)
    }
    
    /// <summary>
    /// Checks if key pressed (GetKeyDown analog)
    /// Uses Unity Input + Rewired for compatibility
    /// </summary>
    private static bool IsKeyDown(KeyCode keyCode) {
        // 1) PRIORITY: Standard Unity Input.GetKeyDown (most reliable for keyboard)
        if (Input.GetKeyDown(keyCode)) {
            previousKeyStates[keyCode] = true;
            return true;
        }

        // 2) Rewired keyboard.GetKeyDown (for compatibility with Rewired)
        if (rewiredPlayer != null) {
            try {
                var keyboard = rewiredPlayer.controllers.Keyboard;
                if (keyboard != null) {
                    if (keyboard.GetKeyDown(keyCode)) {
                        previousKeyStates[keyCode] = true;
                        return true;
                    }
                }
            } catch (Exception ex) {
                LogWarning($"[QTE] Rewired keyboard GetKeyDown failed for {keyCode}: {ex.Message}");
            }
        }

        // 3) Rewired actions (only if standard methods failed)
        if (rewiredPlayer != null) {
            try {
                bool actionDown = false;
                switch (keyCode) {
                    case KeyCode.A:
                    case KeyCode.LeftArrow:
                        actionDown = rewiredPlayer.GetNegativeButtonDown("keyHorizontal");
                        break;
                    case KeyCode.D:
                    case KeyCode.RightArrow:
                        actionDown = rewiredPlayer.GetButtonDown("keyHorizontal");
                        break;
                    case KeyCode.W:
                    case KeyCode.UpArrow:
                        actionDown = rewiredPlayer.GetButtonDown("keyVertical");
                        break;
                    case KeyCode.S:
                    case KeyCode.DownArrow:
                        actionDown = rewiredPlayer.GetNegativeButtonDown("keyVertical");
                        break;
                }

                if (actionDown) {
                    previousKeyStates[keyCode] = true;
                    return true;
                }
            } catch (Exception ex) {
                LogWarning($"[QTE] Rewired action GetButtonDown failed for {keyCode}: {ex.Message}");
            }
        }

        // 4) Fallback via Rewired axes (track transition through threshold)
        if (rewiredPlayer != null) {
            try {
                float axisValue = 0f;
                bool axisPressed = false;
                if (keyCode == KeyCode.A || keyCode == KeyCode.D || 
                    keyCode == KeyCode.LeftArrow || keyCode == KeyCode.RightArrow) {
                    axisValue = rewiredPlayer.GetAxisRaw("keyHorizontal");
                    if (keyCode == KeyCode.A || keyCode == KeyCode.LeftArrow) {
                        axisPressed = axisValue < -0.5f;
                    } else {
                        axisPressed = axisValue > 0.5f;
                    }
                } else if (keyCode == KeyCode.W || keyCode == KeyCode.S || 
                           keyCode == KeyCode.UpArrow || keyCode == KeyCode.DownArrow) {
                    axisValue = rewiredPlayer.GetAxisRaw("keyVertical");
                    if (keyCode == KeyCode.W || keyCode == KeyCode.UpArrow) {
                        axisPressed = axisValue > 0.5f;
                    } else {
                        axisPressed = axisValue < -0.5f;
                    }
                }

                bool previousState = previousKeyStates.TryGetValue(keyCode, out bool prev) ? prev : false;
                previousKeyStates[keyCode] = axisPressed;

                if (axisPressed && !previousState) {
                    return true;
                }
            } catch (Exception ex) {
                LogWarning($"[QTE] Rewired axis fallback failed for {keyCode}: {ex.Message}");
            }
        } else {
            // If Rewired unavailable — reset previous state
            previousKeyStates[keyCode] = false;
        }

        return false;
    }
    
    // ========== Penalties for wrong input (STAGE 6) ==========
    
    /// <summary>
    /// Handles wrong press during cooldowns
    /// </summary>
    private static void ProcessWrongInput() {
        if (currentPlayerStatus == null) {
            return;
        }
        
        // STAGE 3: Check press A/D during cooldown A/D - SP penalty 2x larger
        if (!isWindowActiveLeftRight && cooldownTimerLeftRight > 0f) {
            if (IsKeyDown(KeyCode.A) || IsKeyDown(KeyCode.D)) {
                ApplySPPenaltyForLeftRight();
            }
        }
        
        // Check W/S press during W/S cooldown - apply standard penalty.
        if (!isWindowActiveUpDown && cooldownTimerUpDown > 0f) {
            if (IsKeyDown(KeyCode.W) || IsKeyDown(KeyCode.S)) {
                ApplyPenalty();
            }
        }
    }
    
    /// <summary>
    /// STAGE 3: Apply SP penalty for incorrect press A/D during cooldown
    /// SP penalty 2x larger, than given for correct press
    /// </summary>
    private static void ApplySPPenaltyForLeftRight() {
        if (currentPlayerStatus == null) {
            return;
        }
        
        // Calculate SP penalty (multiplier from config)
        float spPenaltyMultiplier = Plugin.qteSPPenaltyMultiplier?.Value ?? 2.0f;
        float spPenalty = QTESPCalculator.CalculateSPGain() * spPenaltyMultiplier;
        
        // Apply SP penalty
        float currentSP = currentPlayerStatus.Sp;
        float maxSP = currentPlayerStatus.AllMaxSP();
        float newSP = Mathf.Max(0f, currentSP - (maxSP * spPenalty));
        
        // Set new value SP via reflection
        try {
            var spField = AccessTools.Field(typeof(PlayerStatus), "Sp");
            if (spField != null) {
                spField.SetValue(currentPlayerStatus, newSP);
            } else {
                var spProperty = AccessTools.Property(typeof(PlayerStatus), "Sp");
                if (spProperty != null && spProperty.CanWrite) {
                    spProperty.SetValue(currentPlayerStatus, newSP, null);
                }
            }
        } catch (Exception ex) {
            LogWarning($"Failed to set SP: {ex.Message}");
        }
        
        LogInfo($"[QTE] Wrong A/D input penalty: -{spPenalty * 100f:F1}% SP ({newSP:F1}/{maxSP:F1} remaining)");
        
        // STAGE 12: Error sound
        try {
            MasterAudio.PlaySound("snd_nuno", SOUND_VOLUME);
        } catch (Exception ex) {
            LogWarning($"[QTE] Failed to play penalty sound: {ex.Message}");
        }
        
        // Invoke event for Dialogue systems
        OnQTEWrong?.Invoke();
    }
    
    /// <summary>
    /// STAGE 4: Apply penalty for press on red button W/S
    /// SP is set to 0 and MindBroken gets +2%.
    /// </summary>
    private static void ApplyRedButtonPenalty() {
        if (currentPlayerStatus == null) {
            return;
        }
        
        // Zero SP
        float maxSP = currentPlayerStatus.AllMaxSP();
        try {
            var spField = AccessTools.Field(typeof(PlayerStatus), "Sp");
            if (spField != null) {
                spField.SetValue(currentPlayerStatus, 0f);
            } else {
                var spProperty = AccessTools.Property(typeof(PlayerStatus), "Sp");
                if (spProperty != null && spProperty.CanWrite) {
                    spProperty.SetValue(currentPlayerStatus, 0f, null);
                }
            }
        } catch (Exception ex) {
            LogWarning($"Failed to set SP: {ex.Message}");
        }
        
        // Add MindBroken (from config)
        float mbPenalty = Plugin.qteRedButtonMindBrokenPenalty?.Value ?? 0.02f;
        if (MindBrokenSystem.Enabled) {
            MindBrokenSystem.AddPercent(mbPenalty, "qte-red-button");
        }
        
        LogInfo($"[QTE] Red button penalty: SP set to 0 (was: {maxSP:F1}), +{mbPenalty * 100f:F1}% MindBroken");
        
        // STAGE 12: Error sound (more severe)
        try {
            MasterAudio.PlaySound("snd_yabuer", SOUND_VOLUME);
        } catch (Exception ex) {
            // Fallback to other sound if snd_yabuer not found
            try {
                MasterAudio.PlaySound("down_aegi", SOUND_VOLUME);
            } catch (Exception ex2) {
                LogWarning($"[QTE] Failed to play red button sound: {ex.Message}, fallback failed: {ex2.Message}");
            }
        }
        
        // Invoke event for Dialogue systems
        OnQTEWrong?.Invoke();
    }
    
    /// <summary>
    /// Apply penalty for incorrect press
    /// MP -30% or +1% MindBroken (if MP insufficient)
    /// </summary>
    private static void ApplyPenalty() {
        if (currentPlayerStatus == null) {
            return;
        }
        
        // Calculate MP penalty (30% from max)
        float mpPenalty = currentPlayerStatus.MaxMp * mpPenaltyPercent;
        float currentMp = currentPlayerStatus.Mp;
        
        if (currentMp >= mpPenalty) {
            // MP is sufficient - spend only MP.
            try {
                var mpField = AccessTools.Field(typeof(PlayerStatus), "Mp");
                if (mpField != null) {
                    mpField.SetValue(currentPlayerStatus, Mathf.Max(0f, currentMp - mpPenalty));
                } else {
                    var mpProperty = AccessTools.Property(typeof(PlayerStatus), "Mp");
                    if (mpProperty != null && mpProperty.CanWrite) {
                        mpProperty.SetValue(currentPlayerStatus, Mathf.Max(0f, currentMp - mpPenalty), null);
                    }
                }
            } catch (Exception ex) {
                LogWarning($"Failed to set MP: {ex.Message}");
            }
            
            LogInfo($"[QTE] Wrong input penalty: -{mpPenalty:F1} MP ({currentPlayerStatus.Mp:F1}/{currentPlayerStatus.MaxMp:F1} remaining)");
            
            // STAGE 12: Cloth tear sound (snd_nuno)
            try {
                MasterAudio.PlaySound("snd_nuno", SOUND_VOLUME);
            } catch (Exception ex) {
                LogWarning($"[QTE] Failed to play snd_nuno sound: {ex.Message}");
            }
        } else {
            // MP is insufficient - spend all MP and add MindBroken.
            try {
                var mpField = AccessTools.Field(typeof(PlayerStatus), "Mp");
                if (mpField != null) {
                    mpField.SetValue(currentPlayerStatus, 0f);
                } else {
                    var mpProperty = AccessTools.Property(typeof(PlayerStatus), "Mp");
                    if (mpProperty != null && mpProperty.CanWrite) {
                        mpProperty.SetValue(currentPlayerStatus, 0f, null);
                    }
                }
            } catch (Exception ex) {
                LogWarning($"Failed to set MP: {ex.Message}");
            }
            
            // Add MindBroken (from config)
            float mbPenalty = Plugin.qteMindBrokenPenaltyPercent?.Value ?? 0.01f;
            if (MindBrokenSystem.Enabled) {
                MindBrokenSystem.AddPercent(mbPenalty, "qte-wrong-cooldown");
            }
            
            LogInfo($"[QTE] Wrong input penalty: MP depleted ({currentMp:F1}/{currentPlayerStatus.MaxMp:F1}), +{mbPenalty * 100f:F1}% MindBroken");
            
            // STAGE 12: Tearing sound (snd_yabuer or down_aegi).
            try {
                MasterAudio.PlaySound("snd_yabuer", SOUND_VOLUME);
            } catch (Exception ex) {
                // Fallback to other sound if snd_yabuer not found
                try {
                    MasterAudio.PlaySound("down_aegi", SOUND_VOLUME);
                } catch (Exception ex2) {
                    LogWarning($"[QTE] Failed to play mindbroken sound: {ex.Message}, fallback failed: {ex2.Message}");
                }
            }
        }
        
        // Invoke event for Dialogue systems
        OnQTEWrong?.Invoke();
        
        // TODO STAGE 6: Visual feedback (red flash or shake)
    }
    
    // ========== Up/Down button mechanics (STAGE 9) ==========
    
    /// <summary>
    /// Handles press on button up/down (W/S)
    /// </summary>
    private static void OnUpDownButtonPress(KeyCode key, bool isYellow) {
        if (currentPlayerStatus == null) {
            return;
        }
        
        if (isYellow) {
            // Yellow button -> SP bonus (5% -> 2.5% with MB).
            OnYellowButtonPress(key);
        } else {
            // Red button -> penalty.
            OnRedButtonPress(key);
        }
    }
    
    /// <summary>
    /// Handles press on yellow button (bonus)
    /// </summary>
    private static void OnYellowButtonPress(KeyCode key) {
        // STAGE 10: Increment combo counter
        yellowButtonCombo++;
        
        // STAGE 10: Check threshold reached x10
        if (yellowButtonCombo >= COMBO_MILESTONE && !isComboBonusActive) {
            isComboBonusActive = true;
            LogInfo($"QTE Combo milestone reached! x{yellowButtonCombo} - Bonus activated");
            
            // Invoke event for Dialogue systems
            OnQTEComboMilestone?.Invoke(yellowButtonCombo);
        }
        
        // STAGE 4: Random bonus (from config).
        float minBonus = Plugin.qteYellowButtonSPGainMin?.Value ?? 0.15f;
        float maxBonus = Plugin.qteYellowButtonSPGainMax?.Value ?? 0.3f;
        float randomBonus = UnityEngine.Random.Range(minBonus, maxBonus);
        
        // Apply MindBroken influence (linear interpolation).
        float mbPercent = MindBrokenSystem.Enabled ? Mathf.Clamp01(MindBrokenSystem.Percent) : 0f;
        // At 0% MB: full bonus (5-15%); at 100% MB: half bonus (2.5-7.5%).
        float spGain = Mathf.Lerp(randomBonus, randomBonus * 0.5f, mbPercent);
        
        // Apply SP gain to player
        float currentSP = currentPlayerStatus.Sp;
        float maxSP = currentPlayerStatus.AllMaxSP();
        float newSP = Mathf.Min(currentSP + (maxSP * spGain), maxSP);
        
        // Set new value SP via reflection
        try {
            var spField = AccessTools.Field(typeof(PlayerStatus), "Sp");
            if (spField != null) {
                spField.SetValue(currentPlayerStatus, newSP);
            } else {
                var spProperty = AccessTools.Property(typeof(PlayerStatus), "Sp");
                if (spProperty != null && spProperty.CanWrite) {
                    spProperty.SetValue(currentPlayerStatus, newSP, null);
                }
            }
        } catch (Exception ex) {
            LogWarning($"Failed to set SP: {ex.Message}");
        }
        
        LogInfo($"QTE Yellow button pressed: {key}, Combo: x{yellowButtonCombo}, SP gain: +{spGain * 100f:F1}% (new SP: {newSP:F1}/{maxSP:F1})");
        
        // Visual indication: green color on press
        if (key == KeyCode.W) {
            SetButtonColor(upButton, Color.green);
            upButtonColorTimer = pressIndicatorDuration;
        } else if (key == KeyCode.S) {
            SetButtonColor(downButton, Color.green);
            downButtonColorTimer = pressIndicatorDuration;
        }
        
        // STAGE 12: Success sound for yellow button.
        PlayRandomSound(YellowButtonSuccessSoundIds, SOUND_VOLUME);
    }
    
    /// <summary>
    /// Handles press on the red button (penalty).
    /// </summary>
    private static void OnRedButtonPress(KeyCode key) {
        // STAGE 10: Reset combo system on error.
        if (yellowButtonCombo > 0 || isComboBonusActive) {
            int previousCombo = yellowButtonCombo;
            yellowButtonCombo = 0;
            isComboBonusActive = false;
            LogInfo($"QTE Combo reset! Previous combo: x{previousCombo}");
        }
        
        // STAGE 4: Apply penalty: SP = 0 and +2% MindBroken.
        ApplyRedButtonPenalty();
        
        LogInfo($"QTE Red button pressed: {key}, SP set to 0, +2% MindBroken, combo reset");
        
        // Visual indication: red flash (already red, increase intensity).
        if (key == KeyCode.W) {
            SetButtonColor(upButton, new Color(1f, 0f, 0f, 1f)); // Bright red
            upButtonColorTimer = pressIndicatorDuration;
        } else if (key == KeyCode.S) {
            SetButtonColor(downButton, new Color(1f, 0f, 0f, 1f)); // Bright red
            downButtonColorTimer = pressIndicatorDuration;
        }
        
        // STAGE 12: Error sound (use penalty sound).
        // Sound will be played in ApplyPenalty()
    }
    
    // ========== Rotation before disappearance (STAGE 7) ==========
    
    
    // ========== Visual press indication ==========
    
    /// <summary>
    /// Sets button color.
    /// </summary>
    private static void SetButtonColor(GameObject button, Color color) {
        if (button == null) return;
        
        Image image = button.GetComponent<Image>();
        if (image != null) {
            image.color = color;
        }
    }
    
    /// <summary>
    /// Return color to original state (white)
    /// </summary>
    private static void ResetButtonColors() {
        SetButtonColor(leftButton, Color.white);
        SetButtonColor(rightButton, Color.white);
    }
    
    /// <summary>
    /// Updates visual indication timers and restores button colors.
    /// </summary>
    private static void UpdateButtonColorIndicators() {
        if (leftButtonColorTimer > 0f) {
            leftButtonColorTimer -= Time.unscaledDeltaTime;
            if (leftButtonColorTimer <= 0f) {
                SetButtonColor(leftButton, Color.white);
            }
        }
        
        if (rightButtonColorTimer > 0f) {
            rightButtonColorTimer -= Time.unscaledDeltaTime;
            if (rightButtonColorTimer <= 0f) {
                SetButtonColor(rightButton, Color.white);
            }
        }
        
        // STAGE 9: Return color for up/down buttons after visual indication
        if (upButtonColorTimer > 0f) {
            upButtonColorTimer -= Time.unscaledDeltaTime;
            if (upButtonColorTimer <= 0f) {
                ResetUpDownButtonColor(KeyCode.W);
            }
        }
        
        if (downButtonColorTimer > 0f) {
            downButtonColorTimer -= Time.unscaledDeltaTime;
            if (downButtonColorTimer <= 0f) {
                ResetUpDownButtonColor(KeyCode.S);
            }
        }
    }
    
    // ========== Up/Down buttons - color alternation (STAGE 8) ==========
    
    /// <summary>
    /// Update alternation cycle button colors up/down
    /// </summary>
    private static void UpdateUpDownColorCycle() {
        upDownColorTimer += Time.unscaledDeltaTime;
        
        if (upDownColorTimer >= colorChangeInterval) {
            // Swap colors every second (yellow <-> red).
            isUpYellow = !isUpYellow;
            isDownYellow = !isDownYellow;
            upDownColorTimer = 0f;
            UpdateUpDownButtonColors();
        }
    }
    
    /// <summary>
    /// Updates up/down button colors (always opposite).
    /// </summary>
    private static void UpdateUpDownButtonColors() {
        // Up button
        Color upColor = isUpYellow ? Color.yellow : Color.red;
        SetButtonColor(upButton, upColor);
        
        // Down button (always opposite color).
        Color downColor = isDownYellow ? Color.yellow : Color.red;
        SetButtonColor(downButton, downColor);
    }
    
    /// <summary>
    /// Restores up/down button color to original state after visual indication.
    /// </summary>
    private static void ResetUpDownButtonColor(KeyCode key) {
        if (key == KeyCode.W) {
            Color upColor = isUpYellow ? Color.yellow : Color.red;
            SetButtonColor(upButton, upColor);
        } else if (key == KeyCode.S) {
            Color downColor = isDownYellow ? Color.yellow : Color.red;
            SetButtonColor(downButton, downColor);
        }
    }
    
    // ========== Visual difficulty (STAGE 11) ==========
    
    
    // ========== Position customization for all enemies ==========
    
    
    // ========== Sounds (STAGE 12) ==========
    
    /// <summary>
    /// Plays random sound from the provided array.
    /// </summary>
    private static void PlayRandomSound(string[] soundIds, float volume) {
        if (soundIds == null || soundIds.Length == 0) {
            return;
        }
        
        try {
            int randomIndex = UnityEngine.Random.Range(0, soundIds.Length);
            string soundName = soundIds[randomIndex];
            if (!string.IsNullOrEmpty(soundName)) {
                MasterAudio.PlaySound(soundName, volume);
            }
        } catch (Exception ex) {
            LogWarning($"[QTE] Failed to play sound: {ex.Message}");
        }
    }
}

// ========== Harmony patches ==========

/// <summary>
/// Patch for clearing QTE on restart
/// </summary>
[HarmonyPatch(typeof(playercon), "Restart")]
class PlayerConQTE3RestartPatch {
    [HarmonyPostfix]
    static void ResetQTE3() {
        QTESystem.ForceStopQTE();
        // Plugin.Log?.LogInfo("[QTE 3.0] QTE cleared on Restart"); // Disabled by request
    }
}

/// <summary>
/// Patch for clearing QTE on give up
/// </summary>
[HarmonyPatch(typeof(playercon), "ImmediatelyERO")]
class PlayerConQTE3GiveUpPatch {
    [HarmonyPostfix]
    static void ResetQTE3OnGiveUp() {
        QTESystem.ForceStopQTE();
        // Plugin.Log?.LogInfo("[QTE 3.0] QTE cleared on GiveUp"); // Disabled by request
    }
}


