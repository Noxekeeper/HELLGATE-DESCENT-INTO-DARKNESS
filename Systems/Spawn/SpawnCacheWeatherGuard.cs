using System;
using DigitalRuby.RainMaker;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// Additive scene loads for spawn template cache leak rain/VFX/H-audio onto title/menu.
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
        SilenceEroAudioBuses();
    }

    internal static void EndHydrateBatch()
    {
        RestoreMainCameraRain();
        CleanupLeakedRainEffects();
        SilenceEroAudioBuses();
        MuteLeakedHydrateAudioSources();
    }

    internal static void OnAdditiveSceneLoaded(Scene scene)
    {
        SuppressWeatherInScene(scene);
        SuppressAudioInScene(scene);
        RestoreMainCameraRain();
        SilenceEroAudioBuses();
    }

    internal static void OnAdditiveSceneUnloaded()
    {
        RestoreMainCameraRain();
        CleanupLeakedRainEffects();
        SilenceEroAudioBuses();
    }

    /// <summary>Call when entering title menu — stop H moans left over from RanchEro2 hydrate.</summary>
    internal static void SilenceTitleMenuEroAudio()
    {
        SilenceEroAudioBuses();
        MuteLeakedHydrateAudioSources();
        DisableLingeringEroBehaviours();
    }

    private static void SilenceEroAudioBuses()
    {
        try
        {
            DarkTonic.MasterAudio.MasterAudio.StopBus("EroVoice");
        }
        catch { }

        try
        {
            DarkTonic.MasterAudio.MasterAudio.StopBus("EroSE");
        }
        catch { }

        try
        {
            Type masterAudio = Type.GetType("DarkTonic.MasterAudio.MasterAudio, Assembly-CSharp-firstpass")
                               ?? Type.GetType("DarkTonic.MasterAudio.MasterAudio, Assembly-CSharp");
            if (masterAudio == null)
                return;

            System.Reflection.MethodInfo stopBus = masterAudio.GetMethod(
                "StopBus",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null);
            if (stopBus == null)
                return;

            stopBus.Invoke(null, new object[] { "EroVoice" });
            stopBus.Invoke(null, new object[] { "EroSE" });
        }
        catch { }
    }

    private static void SuppressAudioInScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null)
                continue;

            AudioSource[] sources = root.GetComponentsInChildren<AudioSource>(true);
            for (int a = 0; a < sources.Length; a++)
            {
                AudioSource src = sources[a];
                if (src == null)
                    continue;
                src.playOnAwake = false;
                src.Stop();
                // Do NOT mute — that would leave the AudioSource permanently muted if it is later reused
                // by a real H-scene. Stopping and disabling playOnAwake is enough to keep it silent during
                // the additive scene load.
            }

            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int b = 0; b < behaviours.Length; b++)
            {
                MonoBehaviour mb = behaviours[b];
                if (mb == null)
                    continue;
                string typeName = mb.GetType().Name ?? "";
                if (typeName.IndexOf("ERO", StringComparison.OrdinalIgnoreCase) >= 0
                    || typeName.IndexOf("Ero", StringComparison.OrdinalIgnoreCase) >= 0)
                    mb.enabled = false;
            }
        }
    }

    private static void MuteLeakedHydrateAudioSources(Scene additiveScene)
    {
        try
        {
            if (!additiveScene.IsValid() || !additiveScene.isLoaded)
                return;

            GameObject[] roots = additiveScene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                GameObject root = roots[r];
                if (root == null)
                    continue;

                AudioSource[] sources = root.GetComponentsInChildren<AudioSource>(true);
                for (int i = 0; i < sources.Length; i++)
                {
                    AudioSource src = sources[i];
                    if (src == null || src.gameObject == null)
                        continue;

                    string goName = src.gameObject.name ?? "";
                    bool looksEro =
                        goName.IndexOf("ERO", StringComparison.OrdinalIgnoreCase) >= 0
                        || goName.IndexOf("Ero", StringComparison.OrdinalIgnoreCase) >= 0
                        || goName.IndexOf("tyoukyou", StringComparison.OrdinalIgnoreCase) >= 0
                        || goName.IndexOf("slave", StringComparison.OrdinalIgnoreCase) >= 0
                        || IsHellGateTemplateObject(src.gameObject);

                    if (!looksEro)
                        continue;

                    src.playOnAwake = false;
                    src.Stop();
                }
            }
        }
        catch { }
    }

    /// <summary>
    /// Back-compat overload for call sites that do not have a scene handle (title menu cleanup).
    /// Only acts on the additive Ero scene if one is currently loaded; otherwise no-ops.
    /// </summary>
    private static void MuteLeakedHydrateAudioSources()
    {
        try
        {
            Scene additiveScene = GetLoadedHydrateScene();
            if (!additiveScene.IsValid() || !additiveScene.isLoaded)
                return;

            MuteLeakedHydrateAudioSources(additiveScene);
        }
        catch { }
    }

    private static Scene GetLoadedHydrateScene()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.IsValid() && scene.isLoaded &&
                scene.name.IndexOf("Ero", StringComparison.OrdinalIgnoreCase) >= 0)
                return scene;
        }
        return new Scene();
    }

    private static void DisableLingeringEroBehaviours()
    {
        try
        {
            MonoBehaviour[] all = Object.FindObjectsOfType<MonoBehaviour>();
            for (int i = 0; i < all.Length; i++)
            {
                MonoBehaviour mb = all[i];
                if (mb == null)
                    continue;
                string typeName = mb.GetType().Name ?? "";
                if (typeName.IndexOf("ERO", StringComparison.OrdinalIgnoreCase) < 0
                    && typeName.IndexOf("Ero", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                GameObject go = mb.gameObject;
                if (go == null)
                    continue;
                if (!IsHellGateTemplateObject(go)
                    && go.scene.IsValid()
                    && !string.Equals(go.scene.name, "DontDestroyOnLoad", StringComparison.OrdinalIgnoreCase)
                    && go.scene.name.IndexOf("Ero", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                mb.enabled = false;
            }
        }
        catch { }
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
