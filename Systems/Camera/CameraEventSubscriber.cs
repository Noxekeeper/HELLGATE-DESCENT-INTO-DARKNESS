using NoREroMod;
using NoREroMod.Systems.Dialogue;

namespace NoREroMod.Systems.Camera;

/// <summary>
/// Event subscription for camera reactions.
/// </summary>
internal class CameraEventSubscriber
{
    private HSceneCameraController _controller;
    private bool _initialized = false;
    
    internal void Initialize(HSceneCameraController controller)
    {
        if (_initialized)
        {
            return;
        }
        
        _controller = controller;
        
        // Subscribe to QTE events
        QTESystem.OnQTEWrong += OnQTEWrong;
        QTESystem.OnQTEComboMilestone += OnQTEComboMilestone;
        
        _initialized = true;
    }
    
    /// <summary>
    /// Wrong QTE press.
    /// </summary>
    private void OnQTEWrong()
    {
        var controller = HSceneCameraController.Instance;
        if (controller == null || !controller.IsHSceneActive())
        {
            return;
        }
        
        var effectsManager = controller.GetEffectsManager();
        if (effectsManager == null)
        {
            return;
        }
        
        // Shake effect disabled - camera works with game defaults
    }
    
    /// <summary>
    /// Combo milestone.
    /// </summary>
    private void OnQTEComboMilestone(int milestone)
    {
        var controller = HSceneCameraController.Instance;
        if (controller == null || !controller.IsHSceneActive())
        {
            return;
        }
        
        var effectsManager = controller.GetEffectsManager();
        if (effectsManager == null)
        {
            return;
        }
        
        // Shake effect disabled - camera works with game defaults
    }
    
}

