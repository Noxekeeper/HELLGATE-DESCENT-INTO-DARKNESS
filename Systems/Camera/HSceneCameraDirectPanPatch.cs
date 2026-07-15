using HarmonyLib;
using UnityEngine;
using Com.LuisPedroFonseca.ProCamera2D;
using NoREroMod;
using NoREroMod.Systems.Camera;
using NoREroMod.Systems.HSceneEffects;

namespace NoREroMod.Systems.Camera;

/// <summary>
/// Arrow key camera control during downed H-scenes.
/// Death-fatality (RequiemKnight only): vanilla ApplyInfluence on top of enemy camera.
/// Grab H-scenes including *Fatality grabs (Butcher/Slaughterer, BossScapegoat, …): pan-target.
/// </summary>
[HarmonyPatch(typeof(ProCamera2D), "Move")]
internal class HSceneCameraDirectPanPatch
{
    private static bool _hSceneActive = false;
    private static bool _enemyInfluencePan = false;
    private static Vector2 _cameraPanOffset = Vector2.zero;
    private static GameObject _panTargetObject = null;
    private static Transform _panTargetTransform = null;
    private static bool _panWasUsed = false;
    private static Vector3 _basePanPosition = Vector3.zero;
    private static float _cachedPanSpeed = 0.8f;
    private static float _lastPanSpeedUpdate = 0f;
    private const float PAN_SPEED_UPDATE_INTERVAL = 1f;

    /// <summary>Pan uses ApplyInfluence (enemy owns camera targets).</summary>
    internal static bool IsEnemyInfluencePanMode()
    {
        return _enemyInfluencePan;
    }

    internal static bool HasPanOffset()
    {
        if (!_panWasUsed)
            return false;

        if (_enemyInfluencePan)
            return true;

        return _panTargetTransform != null;
    }

    internal static Transform GetPanTargetTransform()
    {
        return _enemyInfluencePan ? null : _panTargetTransform;
    }

    [HarmonyPrefix]
    private static void Move_Prefix(ProCamera2D __instance, float deltaTime)
    {
        try
        {
            var playercon = CameraCache.GetPlayerCon();
            if (playercon != null)
            {
                bool wasActive = _hSceneActive;
                _hSceneActive = PlayerEroContextUtility.IsDownedHSceneForArrowPan(playercon);

                if (_hSceneActive && !wasActive)
                {
                    _enemyInfluencePan = PlayerEroContextUtility.IsEnemyFatalityPlaybackActive();
                    _cameraPanOffset = Vector2.zero;
                    _panWasUsed = false;
                    _basePanPosition = __instance.LocalPosition;
                }
                else if (!_hSceneActive && wasActive)
                {
                    RemovePanTarget(__instance);
                }
                else if (_hSceneActive)
                {
                    _enemyInfluencePan = PlayerEroContextUtility.IsEnemyFatalityPlaybackActive();
                }
            }
            else
            {
                _hSceneActive = false;
                _enemyInfluencePan = false;
            }
        }
        catch
        {
            _hSceneActive = false;
            _enemyInfluencePan = false;
        }

        if (!_hSceneActive)
            return;

        float panSpeed = GetPanSpeed();

        bool leftPressed = Input.GetKey(KeyCode.LeftArrow);
        bool rightPressed = Input.GetKey(KeyCode.RightArrow);
        bool upPressed = Input.GetKey(KeyCode.UpArrow);
        bool downPressed = Input.GetKey(KeyCode.DownArrow);

        if (!leftPressed && !rightPressed && !upPressed && !downPressed)
            return;

        _panWasUsed = true;

        if (_enemyInfluencePan)
        {
            // Same API as vanilla Cameramove.move() — works with enemy ero_camera targets.
            float hor = (rightPressed ? 1f : 0f) - (leftPressed ? 1f : 0f);
            float vert = (upPressed ? 1f : 0f) - (downPressed ? 1f : 0f);
            __instance.ApplyInfluence(new Vector2(hor * 2f * panSpeed, vert * 3f * panSpeed));
            return;
        }

        Vector2 arrowPanDelta = Vector2.zero;
        if (leftPressed)
            arrowPanDelta.x -= panSpeed * deltaTime;
        if (rightPressed)
            arrowPanDelta.x += panSpeed * deltaTime;
        if (upPressed)
            arrowPanDelta.y += panSpeed * deltaTime;
        if (downPressed)
            arrowPanDelta.y -= panSpeed * deltaTime;

        if (_panTargetTransform == null)
        {
            _cameraPanOffset = Vector2.zero;
            CreatePanTarget(__instance);
            RemoveCenterTargetFromZoomEffect(__instance);
        }

        if (arrowPanDelta != Vector2.zero)
            _cameraPanOffset += arrowPanDelta;

        if (_panTargetTransform != null)
        {
            Vector3 expectedPosition = _basePanPosition + new Vector3(_cameraPanOffset.x, _cameraPanOffset.y, 0f);
            _panTargetTransform.position = expectedPosition;
        }
    }

    [HarmonyPostfix]
    private static void Move_Postfix(ProCamera2D __instance, float deltaTime)
    {
        if (!_hSceneActive || !_panWasUsed || _enemyInfluencePan || _panTargetTransform == null)
            return;

        Vector3 expectedPosition = _basePanPosition + new Vector3(_cameraPanOffset.x, _cameraPanOffset.y, 0f);
        _panTargetTransform.position = expectedPosition;

        if (Time.frameCount % 10 == 0)
            PruneNonPanTargets(__instance);
    }

    private static float GetPanSpeed()
    {
        float panSpeed = _cachedPanSpeed;
        float currentTime = Time.time;
        if (currentTime - _lastPanSpeedUpdate <= PAN_SPEED_UPDATE_INTERVAL)
            return panSpeed;

        try
        {
            var cameraSettings = CameraSettings.Load();
            var controller = HSceneCameraController.Instance;
            if (controller != null)
            {
                var enemyName = controller.GetCurrentEnemyNamePublic();
                var settings = cameraSettings.GetEnemySettings(enemyName);
                _cachedPanSpeed = settings.PanSpeed;
                panSpeed = _cachedPanSpeed;
                _lastPanSpeedUpdate = currentTime;
            }
        }
        catch
        {
        }

        return panSpeed;
    }

    private static float _originalHorizontalSmoothness = 0.15f;
    private static float _originalVerticalSmoothness = 0.15f;
    private static bool _smoothnessSaved = false;

    private static void CreatePanTarget(ProCamera2D proCamera2D)
    {
        if (_panTargetObject != null)
            return;

        _panTargetObject = new GameObject("HScenePanTarget");
        _panTargetTransform = _panTargetObject.transform;
        UnityEngine.Object.DontDestroyOnLoad(_panTargetObject);

        CameraCache.InitializeProCamera2DReflection();
        Vector3 currentCameraPosition;

        if (CameraCache.TargetsMidPointField != null)
            currentCameraPosition = (Vector3)CameraCache.TargetsMidPointField.GetValue(proCamera2D);
        else
            currentCameraPosition = proCamera2D.LocalPosition;

        _basePanPosition = currentCameraPosition;
        _panTargetTransform.position = _basePanPosition;

        if (!_smoothnessSaved)
        {
            _originalHorizontalSmoothness = proCamera2D.HorizontalFollowSmoothness;
            _originalVerticalSmoothness = proCamera2D.VerticalFollowSmoothness;
            _smoothnessSaved = true;
        }
        proCamera2D.HorizontalFollowSmoothness = 0f;
        proCamera2D.VerticalFollowSmoothness = 0f;

        proCamera2D.AddCameraTarget(_panTargetTransform, 1f, 1f, 0f, Vector2.zero);
        PruneNonPanTargets(proCamera2D);
    }

    private static void PruneNonPanTargets(ProCamera2D proCamera2D)
    {
        CameraCache.InitializeCameraTargetsReflection();
        if (CameraCache.CameraTargetsField == null)
            return;

        var cameraTargets = CameraCache.CameraTargetsField.GetValue(proCamera2D);
        if (cameraTargets == null || CameraCache.CountProperty == null ||
            CameraCache.GetItemMethod == null || CameraCache.RemoveAtMethod == null ||
            CameraCache.TargetTransformField == null)
            return;

        int count = (int)CameraCache.CountProperty.GetValue(cameraTargets, null);
        for (int i = count - 1; i >= 0; i--)
        {
            var target = CameraCache.GetItemMethod.Invoke(cameraTargets, new object[] { i });
            if (target == null)
                continue;

            Transform targetTransform = CameraCache.TargetTransformField.GetValue(target) as Transform;
            if (targetTransform != _panTargetTransform)
                CameraCache.RemoveAtMethod.Invoke(cameraTargets, new object[] { i });
        }
    }

    private static void RemovePanTarget(ProCamera2D proCamera2D)
    {
        if (_panTargetTransform != null && proCamera2D != null)
            proCamera2D.RemoveCameraTarget(_panTargetTransform, 0f);

        if (_panTargetObject != null)
        {
            UnityEngine.Object.Destroy(_panTargetObject);
            _panTargetObject = null;
            _panTargetTransform = null;
        }

        if (_smoothnessSaved && proCamera2D != null)
        {
            proCamera2D.HorizontalFollowSmoothness = _originalHorizontalSmoothness;
            proCamera2D.VerticalFollowSmoothness = _originalVerticalSmoothness;
            _smoothnessSaved = false;
        }

        _cameraPanOffset = Vector2.zero;
        _panWasUsed = false;
        _basePanPosition = Vector3.zero;
        _enemyInfluencePan = false;
    }

    private static void RemoveCenterTargetFromZoomEffect(ProCamera2D proCamera2D)
    {
        try
        {
            HSceneStartZoomEffect.RemoveCenterTarget();
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogWarning($"[HSceneCameraDirectPan] Failed to remove centerTarget: {ex.Message}");
        }
    }
}
