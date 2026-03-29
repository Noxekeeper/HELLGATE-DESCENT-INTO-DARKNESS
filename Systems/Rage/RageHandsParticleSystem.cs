using System;
using UnityEngine;
using Spine.Unity;

namespace NoREroMod.Systems.Rage;

/// <summary>
/// Fire particle effects on Aradia hands (bone3, bone8) during Rage and SlowMo.
/// Uses 'Particles/Additive' shader (confirmed working in game).
/// Copies sorting layer from player's renderer so particles render on top.
/// Rage = red fire, SlowMo = blue fire.
/// </summary>
internal static class RageHandsParticleSystem
{
    private static GameObject? _rageLeftHand;
    private static GameObject? _rageRightHand;
    private static GameObject? _slowMoLeftHand;
    private static GameObject? _slowMoRightHand;
    private static bool _initialized;
    private static Material? _cachedMaterial;
    private static Texture2D? _particleTexture;
    
    private static readonly string[] BoneNames = { "bone3", "bone8" };
    
    private static bool Enabled => Plugin.rageHandsParticleEnable?.Value ?? true;
    
    private static Color RageColor => new Color(
        Plugin.rageHandsParticleColorR?.Value ?? 1.0f,
        Plugin.rageHandsParticleColorG?.Value ?? 0.0f,
        Plugin.rageHandsParticleColorB?.Value ?? 0.15f,
        1.0f
    );
    
    private static Color SlowMoColor => new Color(
        Plugin.slowMoBoneGlowColorR?.Value ?? 0.3f,
        Plugin.slowMoBoneGlowColorG?.Value ?? 0.6f,
        Plugin.slowMoBoneGlowColorB?.Value ?? 1.0f,
        1.0f
    );
    
    private static float EffectSize => Plugin.rageHandsParticleSize?.Value ?? 4.0f;
    private static float EmissionRate => Plugin.rageHandsParticleEmissionRate?.Value ?? 10.0f; // Balanced emission rate

    // Extra scaling to reduce GPU overdraw / particle fill-rate.
    // Keeps the visual style but makes it noticeably lighter on FPS.
    private static float ParticleSizeScale => (Plugin.ragePerformanceMode?.Value ?? false) ? 0.3f : 0.375f;
    private static float ParticleEmissionScale => (Plugin.ragePerformanceMode?.Value ?? false) ? 0.25f : 0.35f;

    private static int MaxParticles => Plugin.ragePerformanceMode?.Value ?? false ?
        (Plugin.rageHandsParticleMaxParticles?.Value ?? 20) / 2 : // Half particles in performance mode
        (Plugin.rageHandsParticleMaxParticles?.Value ?? 20);
    
    internal static void Initialize()
    {
        if (_initialized) return;
        
        try
        {
            CreateParticleTexture();
            RageSystem.OnActivated += OnRageActivated;
            RageSystem.OnDeactivated += OnRageDeactivated;
            TimeSlowMoSystem.OnActivated += OnSlowMoActivated;
            TimeSlowMoSystem.OnDeactivated += OnSlowMoDeactivated;
            _initialized = true;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[RageHandsFire] Init: {ex.Message}");
        }
    }
    
    private static void CreateParticleTexture()
    {
        int size = 32;
        _particleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        float cx = size * 0.5f;
        float cy = size * 0.5f;
        float maxR = size * 0.45f;
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(1f - dist / maxR);
                alpha *= alpha;
                pixels[y * size + x] = new Color(alpha, alpha, alpha, alpha);
            }
        }
        
        _particleTexture.SetPixels(pixels);
        _particleTexture.Apply(false, true);
    }
    
    private static Material GetOrCreateMaterial()
    {
        if (_cachedMaterial != null) return _cachedMaterial;
        
        // 'Particles/Additive' confirmed working in game log
        Shader shader = Shader.Find("Particles/Additive");
        if (shader == null)
        {
            Plugin.Log?.LogError("[RageHandsFire] Shader 'Particles/Additive' not found!");
            shader = Shader.Find("Sprites/Default");
        }
        
        if (shader == null)
        {
            Plugin.Log?.LogError("[RageHandsFire] No shader found at all!");
            return new Material(Shader.Find("UI/Default"));
        }
        
        _cachedMaterial = new Material(shader);
        if (_particleTexture != null)
        {
            _cachedMaterial.mainTexture = _particleTexture;
        }
        
        return _cachedMaterial;
    }
    
    // ═══════ EVENTS ═══════
    
    private static void OnRageActivated()
    {
        if (!Enabled) return;
        try
        {
        CreateFireEffects(ref _rageLeftHand, ref _rageRightHand, RageColor, "Rage");
        }
        catch (Exception ex) { Plugin.Log?.LogError($"[RageHandsFire] Rage on: {ex}"); }
    }
    
    private static void OnRageDeactivated()
    {
        try { DestroyEffects(ref _rageLeftHand, ref _rageRightHand); }
        catch (Exception ex) { Plugin.Log?.LogError($"[RageHandsFire] Rage off: {ex.Message}"); }
    }
    
    private static void OnSlowMoActivated()
    {
        if (!Enabled) return;
        try
        {
        CreateFireEffects(ref _slowMoLeftHand, ref _slowMoRightHand, SlowMoColor, "SlowMo");
        }
        catch (Exception ex) { Plugin.Log?.LogError($"[RageHandsFire] SlowMo on: {ex}"); }
    }
    
    private static void OnSlowMoDeactivated()
    {
        try { DestroyEffects(ref _slowMoLeftHand, ref _slowMoRightHand); }
        catch (Exception ex) { Plugin.Log?.LogError($"[RageHandsFire] SlowMo off: {ex.Message}"); }
    }
    
    // ═══════ CREATE/DESTROY ═══════
    
    private static void CreateFireEffects(ref GameObject? left, ref GameObject? right, Color color, string tag)
    {
        // Clean up existing effects first
        if (left != null) UnityEngine.Object.Destroy(left);
        if (right != null) UnityEngine.Object.Destroy(right);

        var playerObj = NoREroMod.Systems.Cache.UnifiedPlayerCacheManager.GetPlayerObject();
        if (playerObj == null)
            return;

        // Get sorting info from player's renderer
        string sortingLayerName = "Default";
        int baseSortingOrder = 0;
        var playerRenderer = playerObj.GetComponent<MeshRenderer>();
        if (playerRenderer == null) playerRenderer = playerObj.GetComponentInChildren<MeshRenderer>();
        if (playerRenderer != null)
        {
            sortingLayerName = playerRenderer.sortingLayerName;
            baseSortingOrder = playerRenderer.sortingOrder;
        }

        Material mat = GetOrCreateMaterial();

        left = CreateSinglePS(playerObj, BoneNames[0], color, mat, sortingLayerName, baseSortingOrder + 1, $"{tag}FireL_XUAIGNORE");
        right = CreateSinglePS(playerObj, BoneNames[1], color, mat, sortingLayerName, baseSortingOrder + 1, $"{tag}FireR_XUAIGNORE");
    }
    
    private static GameObject CreateSinglePS(
        GameObject player, string boneName, Color color, Material mat,
        string sortingLayer, int sortingOrder, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(player.transform);
        
        var ps = go.AddComponent<ParticleSystem>();
        
        // Main
        var main = ps.main;
        main.duration = 1.0f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(
            0.15f * EffectSize * ParticleSizeScale,
            0.4f * EffectSize * ParticleSizeScale
        );
        main.gravityModifier = -0.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = MaxParticles;
        main.startColor = color;
        
        // Emission
        var emission = ps.emission;
        emission.rateOverTime = EmissionRate * ParticleEmissionScale;
        
        // Shape - optimized for performance
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle; // Optimized from Sphere
        shape.radius = 0.06f;
        
        // Color over lifetime
        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.white, 0.0f),
                new GradientColorKey(color, 0.3f),
                new GradientColorKey(color * 0.3f, 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1.0f, 0.0f),
                new GradientAlphaKey(0.8f, 0.3f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);
        
        // Size over lifetime
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.6f),
            new Keyframe(0.2f, 1f),
            new Keyframe(1f, 0f)
        );
        sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);
        
        // Renderer - CRITICAL: sorting layer must match player
        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        psr.material = new Material(mat);
        psr.material.color = Color.white;
        psr.sortingLayerName = sortingLayer;
        psr.sortingOrder = sortingOrder;
        
        // Bone tracker
        var tracker = go.AddComponent<HandFireBoneTracker>();
        tracker.Setup(player, boneName);
        
        return go;
    }
    
    private static void DestroyEffects(ref GameObject? left, ref GameObject? right)
    {
        if (left != null) { UnityEngine.Object.Destroy(left); left = null; }
        if (right != null) { UnityEngine.Object.Destroy(right); right = null; }
    }
}

internal class HandFireBoneTracker : MonoBehaviour
{
    private SkeletonAnimation? _spine;
    private Spine.Bone? _bone;
    
    internal void Setup(GameObject player, string boneName)
    {
        _spine = player.GetComponentInChildren<SkeletonAnimation>(true);
        if (_spine?.skeleton != null)
            _bone = _spine.skeleton.FindBone(boneName);
    }
    
    private void LateUpdate()
    {
        if (_spine == null || _bone == null) return;
        transform.position = _spine.transform.TransformPoint(_bone.WorldX, _bone.WorldY, 0f);
    }
}
