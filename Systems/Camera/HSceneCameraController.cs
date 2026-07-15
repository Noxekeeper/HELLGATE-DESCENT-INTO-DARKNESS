using System;
using UnityEngine;
using Com.LuisPedroFonseca.ProCamera2D;
using NoREroMod;
using Spine;
using Spine.Unity;
using System.Reflection;
using NoREroMod.Systems.Effects;

namespace NoREroMod.Systems.Camera;

/// <summary>
/// Main camera controller for H-scenes.
/// Manages position, zoom, effects and event integration.
/// </summary>
internal class HSceneCameraController
{
    private static HSceneCameraController _instance;
    private static bool _initialized = false;
    
    private ProCamera2D _proCamera2D;
    private UnityEngine.Camera _mainCamera;
    private playercon _player;
    
    private bool _isHSceneActive = false;
    private string _currentEnemyName = null;
    private Transform _playerTransform;
    private Transform _enemyTransform;
    
    private CameraSettings _settings;
    private CameraEffectsManager _effectsManager;
    private CameraEventSubscriber _eventSubscriber;
    private CumDisplayManager _cumDisplay;
    
    private MonoBehaviour _coroutineRunner;
    
    
    internal static HSceneCameraController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new HSceneCameraController();
            }
            return _instance;
        }
    }
    
    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }
        
        Instance._Initialize();
        _initialized = true;
    }
    
    private void _Initialize()
    {
        // Load settings
        _settings = CameraSettings.Load();
        
        // Initialize components
        _effectsManager = new CameraEffectsManager();
        _eventSubscriber = new CameraEventSubscriber();
        _cumDisplay = new CumDisplayManager();
        
        // Optimization: use cached components
        GameObject mainCameraObj = NoREroMod.Systems.Cache.UnifiedCameraCacheManager.GetMainCamera();
        if (mainCameraObj != null)
        {
            _mainCamera = mainCameraObj.GetComponent<UnityEngine.Camera>();
        }
        _proCamera2D = NoREroMod.Systems.Cache.UnifiedCameraCacheManager.GetProCamera2D();
        
        GameObject playerObj = NoREroMod.Systems.Cache.UnifiedPlayerCacheManager.GetPlayerObject();
        _player = NoREroMod.Systems.Cache.UnifiedPlayerCacheManager.GetPlayer();
        if (playerObj != null)
        {
            _playerTransform = playerObj.transform;
        }
        
        // Create coroutine runner object
        GameObject runnerObj = new GameObject("HSceneCameraRunner");
        UnityEngine.Object.DontDestroyOnLoad(runnerObj);
        _coroutineRunner = runnerObj.AddComponent<CameraCoroutineRunner>();
        
        // Subscribe to events
        _eventSubscriber.Initialize(this);
        
        // Initialize CumDisplay
        _cumDisplay.Initialize();
    }
    
    
    internal void OnHSceneStart()
    {
        _isHSceneActive = true;
        _currentEnemyName = GetCurrentEnemyName();
        _enemyTransform = FindEnemyTransform();
    }
    
    internal void OnHSceneEnd()
    {
        _isHSceneActive = false;
        _effectsManager.StopSlowmo();
        _effectsManager.StopShake();
        _cumDisplay.Hide();
        _cumDisplay.ResetClimaxFlag();
        _currentEnemyName = null;
        _enemyTransform = null;
    }
    
    /// <summary>
    /// Get current enemy name (uses public QTESystem method).
    /// </summary>
    private string GetCurrentEnemyName()
    {
        try
        {
            return QTESystem.GetCurrentEnemyName();
        }
        catch
        {
            // Ignore errors getting enemy name
        }
        
        return null;
    }
    
    /// <summary>
    /// Find enemy Transform (uses public QTESystem method).
    /// </summary>
    private Transform FindEnemyTransform()
    {
        try
        {
            object enemyInstance = QTESystem.GetCurrentEnemyInstance();
            if (enemyInstance != null)
            {
                MonoBehaviour mb = enemyInstance as MonoBehaviour;
                if (mb != null)
                {
                    return mb.transform;
                }
            }
        }
        catch
        {
            // Ignore errors getting enemy Transform
        }
        
        return null;
    }
    
    // Public methods for access from other systems
    
    internal CameraEffectsManager GetEffectsManager() => _effectsManager;
    internal CumDisplayManager GetCumDisplay() => _cumDisplay;
    internal bool IsHSceneActive() => _isHSceneActive;
    internal string GetCurrentEnemyNamePublic() => _currentEnemyName;
    
}

/// <summary>
/// Component for running coroutines.
/// </summary>
internal class CameraCoroutineRunner : MonoBehaviour
{
}

