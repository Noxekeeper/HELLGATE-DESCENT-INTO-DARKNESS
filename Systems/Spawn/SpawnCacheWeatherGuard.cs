using System;
using DigitalRuby.RainMaker;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// Additive scene loads for spawn template cache leak rain/VFX onto MainCamera.
/// Snapshot gameplay weather, suppress loaded scene effects, restore after hydrate.
/// </summary>
internal static class SpawnCacheWeatherGuard
{
    private static bool hasSnapshot;
    private static bool savedRainEnabled;
    private static float savedRainFade;
    private static float savedRainIntensity;

    internal static void BeginHydrateBatch()
    {
        SnapshotMainCameraRain();
    }

    internal static void EndHydrateBatch()
    {
        RestoreMainCameraRain();
        CleanupLeakedRainEffects();
    }

    internal static void OnAdditiveSceneLoaded(Scene scene)
    {
        SuppressWeatherInScene(scene);
        RestoreMainCameraRain();
    }

    internal static void OnAdditiveSceneUnloaded()
    {
        RestoreMainCameraRain();
        CleanupLeakedRainEffects();
    }

    private static void SnapshotMainCameraRain()
    {
        CameraFilterPack_Atmosphere_Rain_Pro rain = GetMainCameraRain();
        if (rain == null)
        {
            hasSnapshot = false;
            return;
        }

        savedRainEnabled = rain.enabled;
        savedRainFade = rain.Fade;
        savedRainIntensity = rain.Intensity;
        hasSnapshot = true;
    }

    private static void RestoreMainCameraRain()
    {
        CameraFilterPack_Atmosphere_Rain_Pro rain = GetMainCameraRain();
        if (rain == null)
            return;

        if (hasSnapshot)
        {
            rain.enabled = savedRainEnabled;
            rain.Fade = savedRainFade;
            rain.Intensity = savedRainIntensity;
        }
        else if (!ShouldGameplaySceneHaveRain())
        {
            rain.enabled = false;
            rain.Fade = 0f;
            rain.Intensity = 0f;
        }

        if (!rain.enabled)
        {
            rain.Fade = 0f;
            StopRainAudio();
        }
    }

    private static void SuppressWeatherInScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null)
                continue;

            BaseRainScript[] rainScripts = root.GetComponentsInChildren<BaseRainScript>(true);
            for (int r = 0; r < rainScripts.Length; r++)
            {
                BaseRainScript script = rainScripts[r];
                if (script == null)
                    continue;

                script.RainIntensity = 0f;
                script.enabled = false;
                script.gameObject.SetActive(false);
            }

            ParticleSystem[] particles = root.GetComponentsInChildren<ParticleSystem>(true);
            for (int p = 0; p < particles.Length; p++)
            {
                ParticleSystem ps = particles[p];
                if (ps == null)
                    continue;

                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.gameObject.SetActive(false);
            }
        }
    }

    private static void CleanupLeakedRainEffects()
    {
        RestoreMainCameraRain();

        BaseRainScript[] rainScripts = Object.FindObjectsOfType<BaseRainScript>();
        string gameplayScene = HellGateLocationSpawnRefresh.GetReSceneName();
        for (int i = 0; i < rainScripts.Length; i++)
        {
            BaseRainScript script = rainScripts[i];
            if (script == null || script.gameObject == null)
                continue;

            if (IsHellGateTemplateObject(script.gameObject))
                continue;

            Scene owner = script.gameObject.scene;
            if (!owner.IsValid() || owner.name == "DontDestroyOnLoad")
            {
                script.RainIntensity = 0f;
                script.enabled = false;
                script.gameObject.SetActive(false);
                continue;
            }

            if (!string.IsNullOrEmpty(gameplayScene) &&
                !string.Equals(owner.name, gameplayScene, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(owner.name, "Common", StringComparison.OrdinalIgnoreCase))
            {
                script.RainIntensity = 0f;
                script.enabled = false;
                script.gameObject.SetActive(false);
            }
        }

        if (!ShouldGameplaySceneHaveRain())
            StopRainAudio();
    }

    private static bool ShouldGameplaySceneHaveRain()
    {
        string scene = HellGateLocationSpawnRefresh.GetReSceneName();
        if (string.IsNullOrEmpty(scene))
            return false;

        return scene.IndexOf("village", StringComparison.OrdinalIgnoreCase) >= 0 ||
               scene.IndexOf("ForestOfRequiem", StringComparison.OrdinalIgnoreCase) >= 0 ||
               scene.IndexOf("forest", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsHellGateTemplateObject(GameObject obj)
    {
        return obj != null &&
               obj.name.StartsWith("HellGate", StringComparison.Ordinal) &&
               obj.name.Contains("Template_");
    }

    private static CameraFilterPack_Atmosphere_Rain_Pro GetMainCameraRain()
    {
        try
        {
            GameObject cam = GameObject.FindWithTag("MainCamera");
            if (cam == null)
                return null;
            return cam.GetComponent<CameraFilterPack_Atmosphere_Rain_Pro>();
        }
        catch
        {
            return null;
        }
    }

    private static void StopRainAudio()
    {
        try
        {
            Type masterAudio = Type.GetType("DarkTonic.MasterAudio.MasterAudio, Assembly-CSharp-firstpass");
            if (masterAudio == null)
                return;

            System.Reflection.MethodInfo stop = masterAudio.GetMethod(
                "StopAllOfSound",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null);
            if (stop != null)
                stop.Invoke(null, new object[] { "rain_medium" });
        }
        catch
        {
        }
    }
}
