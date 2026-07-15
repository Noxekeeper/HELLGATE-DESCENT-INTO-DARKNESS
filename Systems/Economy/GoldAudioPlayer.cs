using UnityEngine;

namespace NoREroMod.Systems.Economy;

/// <summary>
/// 2D one-shot audio player for the Gold module. We do NOT use
/// <see cref="AudioSource.PlayClipAtPoint"/> because that creates a 3D-spatial source which
/// falls off with camera distance — NoR's audio mix is mostly 2D / MasterAudio-based, so
/// a 3D coin sound played far from camera was inaudible. This helper spawns a temporary
/// <see cref="AudioSource"/> with <c>spatialBlend = 0</c> (full 2D), plays the clip, then
/// destroys itself after the clip length elapses.
/// </summary>
internal static class GoldAudioPlayer
{
    public static void Play2D(AudioClip clip, float volume)
    {
        if (clip == null)
        {
            if (EconomicConfig.DebugLogging)
                Plugin.Log?.LogInfo("[GoldAudio] Play2D skipped: clip is null");
            return;
        }
        float v = Mathf.Clamp01(volume);
        if (v <= 0f)
        {
            if (EconomicConfig.DebugLogging)
                Plugin.Log?.LogInfo($"[GoldAudio] Play2D skipped: volume={volume} (clamped {v})");
            return;
        }

        try
        {
            GameObject host = new GameObject("GoldAudio_OneShot_XUAIGNORE");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;

            AudioSource src = host.AddComponent<AudioSource>();
            src.spatialBlend = 0f;
            src.playOnAwake = false;
            src.loop = false;
            src.priority = 0;
            // Bypass any AudioMixer / listener effects the game may have set up. Without this,
            // very short / quiet clips were inaudible while longer drop SFX still played.
            src.bypassEffects = true;
            src.bypassListenerEffects = true;
            src.bypassReverbZones = true;
            src.outputAudioMixerGroup = null;
            // PlayOneShot is the documented path for short fire-and-forget SFX and respects the
            // volumeScale parameter independently of AudioSource.volume.
            src.PlayOneShot(clip, v);

            UnityEngine.Object.Destroy(host, clip.length + 0.2f);

            if (EconomicConfig.DebugLogging)
                Plugin.Log?.LogInfo($"[GoldAudio] Play2D '{clip.name}' volume={v:0.##} length={clip.length:0.##}s");
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogWarning("[GoldAudio] Play2D failed: " + ex.Message);
        }
    }
}
