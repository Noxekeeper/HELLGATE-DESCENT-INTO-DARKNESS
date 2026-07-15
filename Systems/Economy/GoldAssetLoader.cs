using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NoREroMod.Systems.Economy;

/// <summary>
/// Loads all PNG / WAV assets used by the Gold module. Hard-coded relative paths,
/// no fallback chain — the project owner controls the source layout. If a file is
/// missing, the affected sub-feature logs a warning and disables itself; the rest
/// of the module keeps running.
///
/// Layout (relative to game root, e.g. <c>Application.dataPath/..</c>):
///   sources/HellGate_sources/EconomicHG/Gold/frame_000..008.png   — pickup loop frames
///   sources/HellGate_sources/EconomicHG/UI/UI-Gold.png            — HUD/popup icon
///   sources/HellGate_sources/EconomicHG/Audio/NormalDrop/*.wav    — drop trigger
///   sources/HellGate_sources/EconomicHG/Audio/PickUpGold/*.wav    — pickup
/// </summary>
internal static class GoldAssetLoader
{
    private const string EconomicHgRel = "sources/HellGate_sources/EconomicHG";

    private static readonly List<Sprite> _pickupFrames = new List<Sprite>(9);
    private static Sprite _uiIcon;
    private static AudioClip _dropClip;
    private static readonly List<AudioClip> _pickupClips = new List<AudioClip>(2);
    private static bool _initialized;

    public static List<Sprite> PickupFrames => _pickupFrames;
    public static Sprite UiIcon => _uiIcon;
    public static AudioClip DropClip => _dropClip;
    public static List<AudioClip> PickupClips => _pickupClips;

    public static bool HasFrames => _pickupFrames.Count > 0;
    public static bool HasUiIcon => _uiIcon != null;
    public static bool HasDropClip => _dropClip != null;
    public static bool HasPickupClip => _pickupClips.Count > 0;

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        string root = ResolveEconomicHgRoot();
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            // ResolveEconomicHgRoot already logged a dual-path "not found" warning.
            return;
        }
        if (EconomicConfig.DebugLogging)
            Plugin.Log?.LogInfo("[GoldAssetLoader] EconomicHG root resolved to: " + root);

        LoadPickupFrames(Path.Combine(root, "Gold"));
        LoadUiIcon(Path.Combine(Path.Combine(root, "UI"), "UI-Gold.png"));

        if (EconomicConfig.Audio.Enable)
        {
            LoadDropClip(Path.Combine(root, EconomicConfig.Audio.DropFolder.Replace("EconomicHG/", "").Replace("EconomicHG\\", "")));
            LoadPickupClips(Path.Combine(root, EconomicConfig.Audio.PickupFolder.Replace("EconomicHG/", "").Replace("EconomicHG\\", "")));
        }
    }

    /// <summary>
    /// Two install layouts coexist on real users' machines:
    ///   1. <c>[GameRoot]/sources/HellGate_sources/EconomicHG</c>            (typical Steam install)
    ///   2. <c>[ParentOfGameRoot]/sources/HellGate_sources/EconomicHG</c>   (this project's dev layout — see RageVisualEffectsSystem path2)
    /// We probe (1) first, then (2). Whichever exists wins. Single warning if neither is found.
    /// </summary>
    private static string ResolveEconomicHgRoot()
    {
        try
        {
            string gameRoot = Application.dataPath;
            if (gameRoot.EndsWith("_Data", StringComparison.Ordinal))
                gameRoot = gameRoot.Substring(0, gameRoot.Length - 5);

            string p1 = Path.GetFullPath(Path.Combine(gameRoot, EconomicHgRel));
            if (Directory.Exists(p1)) return p1;

            string p2 = Path.GetFullPath(Path.Combine(Path.Combine(gameRoot, ".."), EconomicHgRel));
            if (Directory.Exists(p2)) return p2;

            // Return the primary path even if missing so the existing missing-folder warning
            // points at the most likely intended location.
            Plugin.Log?.LogWarning("[GoldAssetLoader] EconomicHG not found at either '" + p1 + "' or '" + p2 + "'.");
            return p1;
        }
        catch
        {
            return null;
        }
    }

    private static void LoadPickupFrames(string dir)
    {
        if (!Directory.Exists(dir))
        {
            Plugin.Log?.LogWarning("[GoldAssetLoader] Pickup frames folder missing: " + dir);
            return;
        }

        string[] files;
        try { files = Directory.GetFiles(dir, "frame_*.png"); }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[GoldAssetLoader] Failed to enumerate pickup frames: " + ex.Message);
            return;
        }

        if (files == null || files.Length == 0)
        {
            Plugin.Log?.LogWarning("[GoldAssetLoader] No frame_*.png files in: " + dir);
            return;
        }

        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < files.Length; i++)
        {
            Sprite s = LoadSpriteFromFile(files[i]);
            if (s != null) _pickupFrames.Add(s);
        }

        if (EconomicConfig.DebugLogging)
            Plugin.Log?.LogInfo("[GoldAssetLoader] Loaded " + _pickupFrames.Count + " pickup frame(s) from " + dir);
    }

    private static void LoadUiIcon(string path)
    {
        if (!File.Exists(path))
        {
            Plugin.Log?.LogWarning("[GoldAssetLoader] UI-Gold.png missing at: " + path);
            return;
        }

        _uiIcon = LoadSpriteFromFile(path);
        if (_uiIcon == null)
            Plugin.Log?.LogWarning("[GoldAssetLoader] UI-Gold.png failed to decode: " + path);
        else if (EconomicConfig.DebugLogging)
            Plugin.Log?.LogInfo("[GoldAssetLoader] HUD icon loaded.");
    }

    private static void LoadDropClip(string folder)
    {
        if (!Directory.Exists(folder))
        {
            Plugin.Log?.LogWarning("[GoldAssetLoader] Drop sound folder missing: " + folder);
            return;
        }

        string first = FirstWavInFolder(folder);
        if (first == null)
        {
            Plugin.Log?.LogWarning("[GoldAssetLoader] No WAV in drop folder: " + folder);
            return;
        }

        AudioClip clip = LoadWavFromFile(first);
        if (clip != null)
        {
            _dropClip = clip;
            if (EconomicConfig.DebugLogging)
                Plugin.Log?.LogInfo("[GoldAssetLoader] Drop clip loaded: " + Path.GetFileName(first));
        }
        else
        {
            Plugin.Log?.LogWarning("[GoldAssetLoader] Drop WAV failed to decode: " + first);
        }
    }

    private static void LoadPickupClips(string folder)
    {
        if (!Directory.Exists(folder))
        {
            Plugin.Log?.LogWarning("[GoldAssetLoader] Pickup sound folder missing: " + folder);
            return;
        }

        string[] wavs;
        try { wavs = Directory.GetFiles(folder, "*.wav"); }
        catch
        {
            Plugin.Log?.LogWarning("[GoldAssetLoader] Failed to enumerate pickup folder: " + folder);
            return;
        }

        if (wavs == null || wavs.Length == 0)
        {
            Plugin.Log?.LogWarning("[GoldAssetLoader] No WAV in pickup folder: " + folder);
            return;
        }

        Array.Sort(wavs, StringComparer.OrdinalIgnoreCase);
        // If randomization is off we still load only the first to keep memory low.
        int count = EconomicConfig.Audio.RandomizePickup ? wavs.Length : 1;
        for (int i = 0; i < count; i++)
        {
            AudioClip clip = LoadWavFromFile(wavs[i]);
            if (clip != null) _pickupClips.Add(clip);
        }
        if (EconomicConfig.DebugLogging)
            Plugin.Log?.LogInfo("[GoldAssetLoader] Pickup clips loaded: " + _pickupClips.Count);
    }

    private static string FirstWavInFolder(string folder)
    {
        string[] wavs;
        try { wavs = Directory.GetFiles(folder, "*.wav"); }
        catch { return null; }
        if (wavs == null || wavs.Length == 0) return null;
        Array.Sort(wavs, StringComparer.OrdinalIgnoreCase);
        return wavs[0];
    }

    private static Sprite LoadSpriteFromFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return null;
            byte[] data = File.ReadAllBytes(filePath);
            if (data == null || data.Length == 0) return null;

            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(data, false)) return null;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[GoldAssetLoader] Failed loading sprite " + filePath + ": " + ex.Message);
            return null;
        }
    }

    private static AudioClip LoadWavFromFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return null;
            byte[] data = File.ReadAllBytes(filePath);
            return WavDecoder.Decode(data, Path.GetFileNameWithoutExtension(filePath));
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[GoldAssetLoader] Failed loading WAV " + filePath + ": " + ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Minimal RIFF/WAVE PCM decoder (16-bit / 8-bit / 24-bit / 32-bit-PCM, mono/stereo).
    /// Returns a runtime <see cref="AudioClip"/>. We avoid <c>UnityWebRequest</c> /
    /// <c>WWW</c> coroutines so the gold module is fully synchronous at init.
    /// </summary>
    private static class WavDecoder
    {
        public static AudioClip Decode(byte[] data, string clipName)
        {
            if (data == null || data.Length < 44) return null;

            // RIFF header.
            if (!(data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F')) return null;
            if (!(data[8] == 'W' && data[9] == 'A' && data[10] == 'V' && data[11] == 'E')) return null;

            int pos = 12;
            ushort audioFormat = 0;
            ushort channels = 0;
            uint sampleRate = 0;
            ushort bitsPerSample = 0;
            int dataOffset = 0;
            int dataLength = 0;

            while (pos + 8 <= data.Length)
            {
                string chunkId = new string(new[] { (char)data[pos], (char)data[pos + 1], (char)data[pos + 2], (char)data[pos + 3] });
                int chunkSize = BitConverter.ToInt32(data, pos + 4);
                int chunkBodyOffset = pos + 8;

                if (chunkId == "fmt ")
                {
                    audioFormat = BitConverter.ToUInt16(data, chunkBodyOffset);
                    channels = BitConverter.ToUInt16(data, chunkBodyOffset + 2);
                    sampleRate = BitConverter.ToUInt32(data, chunkBodyOffset + 4);
                    bitsPerSample = BitConverter.ToUInt16(data, chunkBodyOffset + 14);
                }
                else if (chunkId == "data")
                {
                    dataOffset = chunkBodyOffset;
                    dataLength = chunkSize;
                    break;
                }

                pos = chunkBodyOffset + chunkSize;
                if ((chunkSize & 1) == 1) pos++; // RIFF chunks are word-aligned.
            }

            if (audioFormat != 1 /* PCM */ || channels == 0 || sampleRate == 0 || bitsPerSample == 0 || dataLength <= 0)
            {
                Plugin.Log?.LogWarning("[GoldAssetLoader] Unsupported WAV format (need PCM): " + clipName);
                return null;
            }

            int bytesPerSample = bitsPerSample / 8;
            int totalSamples = dataLength / bytesPerSample;
            int frameCount = totalSamples / channels;
            float[] floats = new float[totalSamples];

            int srcPos = dataOffset;
            for (int i = 0; i < totalSamples; i++)
            {
                float sample = 0f;
                switch (bitsPerSample)
                {
                    case 8:
                        sample = (data[srcPos] - 128) / 128f;
                        break;
                    case 16:
                        short s16 = BitConverter.ToInt16(data, srcPos);
                        sample = s16 / 32768f;
                        break;
                    case 24:
                        int s24 = data[srcPos] | (data[srcPos + 1] << 8) | (data[srcPos + 2] << 16);
                        if ((s24 & 0x800000) != 0) s24 |= unchecked((int)0xFF000000);
                        sample = s24 / 8388608f;
                        break;
                    case 32:
                        int s32 = BitConverter.ToInt32(data, srcPos);
                        sample = s32 / 2147483648f;
                        break;
                    default:
                        return null;
                }
                floats[i] = sample;
                srcPos += bytesPerSample;
            }

            AudioClip clip = AudioClip.Create(string.IsNullOrEmpty(clipName) ? "GoldClip" : clipName, frameCount, channels, (int)sampleRate, false);
            clip.SetData(floats, 0);
            return clip;
        }
    }
}
