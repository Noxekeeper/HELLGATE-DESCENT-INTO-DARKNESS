using System;
using System.Collections.Generic;
using System.Reflection;
using Spine.Unity;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoREroMod.Systems.Spawn.Gimmicks;

/// <summary>Parsed optional tokens of a GIMMICK spawn line.</summary>
internal sealed class HellGateGimmickSpawnOptions
{
    public bool Open;
    /// <summary>Token <c>id=name</c>; preserved in the pack line, not consumed at runtime yet.</summary>
    public string Id = string.Empty;
    /// <summary>Built-in lever reacts to the player (token <c>lever</c>). Otherwise it only shows the state.</summary>
    public bool Lever;
    /// <summary>Token <c>radius=N</c>; used by the standalone lever only.</summary>
    public float Radius = HellGateGimmickLever.DefaultRadius;
    /// <summary>Key <c>lever</c>: spawn only the lever (gate/ladder of the assembly hidden).</summary>
    public bool LeverOnly;
    /// <summary>Token <c>guard=N</c>: guard zone length along X from the spawn point. 0 = no guard.</summary>
    public float GuardLength;
    /// <summary>Token <c>guardh=N</c>: guard zone total height, centered on the spawn point.</summary>
    public float GuardHeight = HellGateGimmickGuard.DefaultHeight;
    /// <summary>Token <c>guardside=left|right</c>; omitted = both sides.</summary>
    public HellGateGimmickGuardSide GuardSide = HellGateGimmickGuardSide.Both;
    /// <summary>Token <c>auto[=N]</c>: open only while the player is within N units along X. 0 = off.</summary>
    public float AutoRange;
}

/// <summary>
/// Scene gimmicks (Spine gate_fence gates, folding ladders) for GIMMICK,Key,X,Y,state spawn lines.
/// Caches lever + spine assembly from vanilla DoorOpen / ladder_on / GimmickLadder_on and swaps
/// the vanilla script for <see cref="HellGateGimmickController"/> on every spawned clone.
/// </summary>
internal static class HellGateGimmickRuntime
{
    private const int MaxAssemblyTransforms = 48;
    private const string SkeletonDataSuffix = "_SkeletonData";

    private sealed class TemplateInfo
    {
        public HellGateGimmickKind Kind;
        public string SkeletonKey = string.Empty;
    }

    private sealed class VanillaParts
    {
        public HellGateGimmickKind Kind;
        public SkeletonAnimation Main;
        public SkeletonAnimation Lever;
        public AudioSource Audio;
        public Canvas Prompt;
    }

    internal static readonly Type[] VanillaComponentTypes =
    {
        typeof(DoorOpen),
        typeof(ladder_on),
        typeof(GimmickLadder_on)
    };

    /// <summary>
    /// Only these maps feed GIMMICK templates. Each is already hydrated for other templates; caching from any
    /// other map writes it into the disk cache and adds a full additive load of that map to the splash preload.
    /// </summary>
    private static readonly string[] SourceScenes =
    {
        "UndergroundChurch",
        "UndergroundLaboratory",
        "PilgrimageEntrance",
        "InsomniaTownC"
    };

    internal static bool IsSourceScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return false;
        for (int i = 0; i < SourceScenes.Length; i++)
        {
            if (string.Equals(SourceScenes[i], sceneName.Trim(), StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    private static readonly FieldInfo doorSpineField = typeof(DoorOpen).GetField("DoorSpine", FieldFlags);
    private static readonly FieldInfo doorLeverField = typeof(DoorOpen).GetField("myspine", FieldFlags);
    private static readonly FieldInfo doorAudioField = typeof(DoorOpen).GetField("Audio", FieldFlags);
    private static readonly FieldInfo doorCanvasField = typeof(DoorOpen).GetField("canvas", FieldFlags);

    private static readonly FieldInfo ladderSpineField = typeof(ladder_on).GetField("ladderSpine", FieldFlags);
    private static readonly FieldInfo ladderLeverField = typeof(ladder_on).GetField("myspine", FieldFlags);
    private static readonly FieldInfo ladderAudioField = typeof(ladder_on).GetField("Audio", FieldFlags);
    private static readonly FieldInfo ladderCanvasField = typeof(ladder_on).GetField("canvas", FieldFlags);

    private static readonly FieldInfo gimmickLadderSpineField = typeof(GimmickLadder_on).GetField("ladderSpine", FieldFlags);
    private static readonly FieldInfo gimmickLadderAudioField = typeof(GimmickLadder_on).GetField("Audio", FieldFlags);

    // Template instance id -> info. Templates are DontDestroyOnLoad clones owned by SpawnTemplateCatalog.
    private static readonly Dictionary<int, TemplateInfo> templates = new Dictionary<int, TemplateInfo>();

    internal static bool IsGimmickTemplate(GameObject template)
    {
        return template != null && templates.ContainsKey(template.GetInstanceID());
    }

    internal static void RegisterTemplate(GameObject template, HellGateGimmickKind kind, string skeletonKey)
    {
        if (template == null || kind == HellGateGimmickKind.Unknown)
            return;

        templates[template.GetInstanceID()] = new TemplateInfo
        {
            Kind = kind,
            SkeletonKey = skeletonKey ?? string.Empty
        };
    }

    /// <summary>
    /// Cache source for one vanilla gimmick: common ancestor of lever script and animated spine.
    /// Falls back to the spine object alone when the ancestor is a large scene container.
    /// </summary>
    internal static bool TryResolveTemplateSource(
        Component vanilla,
        out GameObject source,
        out HellGateGimmickKind kind,
        out string skeletonKey)
    {
        source = null;
        kind = HellGateGimmickKind.Unknown;
        skeletonKey = string.Empty;

        VanillaParts parts = ReadVanillaParts(vanilla);
        if (parts == null || parts.Main == null)
            return false;

        kind = parts.Kind;
        skeletonKey = GetSkeletonKey(parts.Main);
        if (string.IsNullOrEmpty(skeletonKey))
            return false;

        GameObject ancestor = GetCommonAncestor(vanilla.gameObject, parts.Main.gameObject);
        if (ancestor != null && parts.Lever != null)
            ancestor = GetCommonAncestor(ancestor, parts.Lever.gameObject);

        if (ancestor != null && ancestor.GetComponentsInChildren<Transform>(true).Length <= MaxAssemblyTransforms)
        {
            source = ancestor;
        }
        else
        {
            // Gates often cache spine-only (lever sits elsewhere) — expected, not worth Info spam.
            source = parts.Main.gameObject;
        }

        return true;
    }

    /// <summary>Runs on every template spawn before activation. Returns null for non-gimmick spawns.</summary>
    internal static HellGateGimmickController PrepareSpawned(GameObject template, GameObject spawned)
    {
        if (spawned == null || template == null)
            return null;
        if (!templates.TryGetValue(template.GetInstanceID(), out TemplateInfo info))
            return null;

        try
        {
            Component vanilla = FindVanillaComponent(spawned);
            if (vanilla != null)
                return ReplaceVanilla(spawned, vanilla);

            return AttachFromSkeleton(spawned, info);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[GIMMICK] Failed to prepare spawned '{spawned.name}': {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Standalone lever line key. Spawns the wooden-ladder assembly (the only cached gimmick whose lever
    /// sits outside its spine; gates cache spine-only) with the ladder hidden.
    /// </summary>
    internal const string LeverOnlyKey = "lever";
    private const string LeverOnlyTemplateKey = "ladder_nomal";

    /// <summary>Maps the key written in the pack line to the template key actually cached.</summary>
    internal static string ResolveTemplateKey(string lineKey, out bool leverOnly)
    {
        leverOnly = string.Equals(lineKey?.Trim(), LeverOnlyKey, StringComparison.OrdinalIgnoreCase);
        return leverOnly ? LeverOnlyTemplateKey : lineKey;
    }

    internal static void ApplySpawnState(GameObject spawned, HellGateGimmickSpawnOptions options, string logPrefix)
    {
        if (spawned == null || options == null)
            return;

        if (options.LeverOnly)
        {
            ConvertToLeverOnly(spawned, options, logPrefix);
            return;
        }

        HellGateGimmickController controller = spawned.GetComponent<HellGateGimmickController>();
        if (controller == null)
        {
            Plugin.Log?.LogWarning($"{logPrefix} '{spawned.name}' is not a gimmick (no gate/ladder spine) — state ignored.");
            return;
        }

        controller.SetInitialState(options.Open);

        HellGateGimmickLever lever = spawned.GetComponentInChildren<HellGateGimmickLever>(true);
        if (lever != null)
        {
            lever.Configure(options.Lever, options.Radius);
            lever.SetInitialPulled(options.Open);
        }
        // Gate packs often request lever=true while the cached spine has none — expected, stay silent.

        ApplyGuard(controller, options);
    }

    private static void ApplyGuard(HellGateGimmickController controller, HellGateGimmickSpawnOptions options)
    {
        HellGateGimmickGuard guard = controller.GetComponent<HellGateGimmickGuard>();
        if (options.GuardLength <= 0f && options.AutoRange <= 0f)
        {
            if (guard != null)
                Object.Destroy(guard);
            return;
        }

        if (guard == null)
            guard = controller.gameObject.AddComponent<HellGateGimmickGuard>();
        guard.Configure(controller, options.GuardLength, options.GuardHeight, options.GuardSide, options.AutoRange);
    }

    /// <summary>F11 edit of a placed gimmick: animate to the new state, reconfigure lever and guard in place.</summary>
    internal static void ApplyLiveEdit(GameObject spawned, HellGateGimmickSpawnOptions options)
    {
        if (spawned == null || options == null)
            return;

        HellGateGimmickController controller = spawned.GetComponentInChildren<HellGateGimmickController>(true);
        if (controller != null)
        {
            if (controller.IsOpen != options.Open)
            {
                if (options.Open)
                    controller.Open();
                else
                    controller.Close();
            }
        }

        HellGateGimmickLever lever = spawned.GetComponentInChildren<HellGateGimmickLever>(true);
        if (lever != null)
        {
            lever.Configure(options.Lever || options.LeverOnly, options.Radius);
            lever.SetInitialPulled(options.Open);
        }

        if (controller != null)
            ApplyGuard(controller, options);
    }

    /// <summary>F11 preview bake, after <see cref="PrepareSpawned"/> and activation of the clone.</summary>
    internal static void PosePreview(GameObject clone, bool leverOnly, bool open)
    {
        HellGateGimmickController controller = clone != null ? clone.GetComponent<HellGateGimmickController>() : null;
        if (controller == null)
            return;

        if (!leverOnly)
        {
            controller.ApplyPoseNow(open);
            return;
        }

        GameObject main = controller.MainSpineObject;
        HellGateGimmickLever lever = clone.GetComponentInChildren<HellGateGimmickLever>(true);
        if (CanSplitLever(clone, lever, main))
            main.SetActive(false);
    }

    private static bool CanSplitLever(GameObject root, HellGateGimmickLever lever, GameObject main)
    {
        return lever != null && main != null && main != root &&
               !lever.transform.IsChildOf(main.transform) &&
               !lever.VisualTransform.IsChildOf(main.transform);
    }

    private static void ConvertToLeverOnly(GameObject spawned, HellGateGimmickSpawnOptions options, string logPrefix)
    {
        HellGateGimmickLever lever = spawned.GetComponentInChildren<HellGateGimmickLever>(true);
        HellGateGimmickController controller = spawned.GetComponent<HellGateGimmickController>();
        GameObject main = controller != null ? controller.MainSpineObject : null;

        if (!CanSplitLever(spawned, lever, main))
        {
            Plugin.Log?.LogWarning($"{logPrefix} Cannot split a standalone lever from '{spawned.name}' — removed.");
            Object.Destroy(spawned);
            return;
        }

        main.SetActive(false);
        Object.DestroyImmediate(controller);
        lever.ClearOwner();
        lever.Configure(true, options.Radius);
        lever.SetInitialPulled(options.Open);
    }

    internal static string DetectVanillaComponentName(GameObject source)
    {
        Component vanilla = FindVanillaComponent(source);
        return vanilla != null ? vanilla.GetType().Name : string.Empty;
    }

    internal static string GetSkeletonKey(SkeletonAnimation spine)
    {
        if (spine == null || spine.skeletonDataAsset == null)
            return string.Empty;

        string name = spine.skeletonDataAsset.name ?? string.Empty;
        if (name.EndsWith(SkeletonDataSuffix, StringComparison.OrdinalIgnoreCase))
            name = name.Substring(0, name.Length - SkeletonDataSuffix.Length);
        return name.Trim();
    }

    private static HellGateGimmickController ReplaceVanilla(GameObject spawned, Component vanilla)
    {
        VanillaParts parts = ReadVanillaParts(vanilla);
        Transform root = spawned.transform;
        GameObject leverHost = vanilla.gameObject;

        // References outside the clone still point at the vanilla scene objects — never touch those.
        SkeletonAnimation main = parts != null && IsInside(parts.Main, root) ? parts.Main : null;
        SkeletonAnimation lever = parts != null && IsInside(parts.Lever, root) ? parts.Lever : null;
        AudioSource audio = parts != null && IsInside(parts.Audio, root) ? parts.Audio : null;
        Canvas prompt = parts != null && IsInside(parts.Prompt, root) ? parts.Prompt : null;
        if (prompt != null)
            prompt.enabled = false;

        for (int t = 0; t < VanillaComponentTypes.Length; t++)
        {
            Component[] found = spawned.GetComponentsInChildren(VanillaComponentTypes[t], true);
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null)
                    Object.DestroyImmediate(found[i]);
            }
        }

        HellGateGimmickKind kind = parts != null ? parts.Kind : HellGateGimmickKind.Unknown;
        if (main == null || kind == HellGateGimmickKind.Unknown)
            return null;

        HellGateGimmickController controller = spawned.AddComponent<HellGateGimmickController>();
        controller.Bind(kind, main, audio);

        // Lever lives on the object that held the vanilla script — that is where its trigger collider is.
        if (lever != null && leverHost != null)
        {
            HellGateGimmickLever leverComponent = leverHost.AddComponent<HellGateGimmickLever>();
            leverComponent.Bind(lever, prompt, controller);
        }

        return controller;
    }

    private static HellGateGimmickController AttachFromSkeleton(GameObject spawned, TemplateInfo info)
    {
        SkeletonAnimation[] spines = spawned.GetComponentsInChildren<SkeletonAnimation>(true);
        for (int i = 0; i < spines.Length; i++)
        {
            if (!string.Equals(GetSkeletonKey(spines[i]), info.SkeletonKey, StringComparison.OrdinalIgnoreCase))
                continue;

            HellGateGimmickController controller = spawned.AddComponent<HellGateGimmickController>();
            controller.Bind(info.Kind, spines[i], null);
            return controller;
        }

        return null;
    }

    private static VanillaParts ReadVanillaParts(Component vanilla)
    {
        if (vanilla == null)
            return null;

        if (vanilla is DoorOpen)
        {
            return new VanillaParts
            {
                Kind = HellGateGimmickKind.Gate,
                Main = GetField<SkeletonAnimation>(doorSpineField, vanilla),
                Lever = GetField<SkeletonAnimation>(doorLeverField, vanilla),
                Audio = GetField<AudioSource>(doorAudioField, vanilla),
                Prompt = GetField<Canvas>(doorCanvasField, vanilla)
            };
        }

        if (vanilla is ladder_on)
        {
            return new VanillaParts
            {
                Kind = HellGateGimmickKind.Ladder,
                Main = GetField<SkeletonAnimation>(ladderSpineField, vanilla),
                Lever = GetField<SkeletonAnimation>(ladderLeverField, vanilla),
                Audio = GetField<AudioSource>(ladderAudioField, vanilla),
                Prompt = GetField<Canvas>(ladderCanvasField, vanilla)
            };
        }

        if (vanilla is GimmickLadder_on)
        {
            return new VanillaParts
            {
                Kind = HellGateGimmickKind.Ladder,
                Main = GetField<SkeletonAnimation>(gimmickLadderSpineField, vanilla),
                Audio = GetField<AudioSource>(gimmickLadderAudioField, vanilla)
            };
        }

        return null;
    }

    private static Component FindVanillaComponent(GameObject root)
    {
        if (root == null)
            return null;

        for (int t = 0; t < VanillaComponentTypes.Length; t++)
        {
            Component found = root.GetComponentInChildren(VanillaComponentTypes[t], true);
            if (found != null)
                return found;
        }

        return null;
    }

    private static T GetField<T>(FieldInfo field, Component owner) where T : Object
    {
        if (field == null || owner == null)
            return null;
        return field.GetValue(owner) as T;
    }

    private static bool IsInside(Component component, Transform root)
    {
        return component != null && root != null && component.transform.IsChildOf(root);
    }

    private static GameObject GetCommonAncestor(GameObject first, GameObject second)
    {
        if (first == null || second == null)
            return null;

        for (Transform a = first.transform; a != null; a = a.parent)
        {
            if (second.transform == a || second.transform.IsChildOf(a))
                return a.gameObject;
        }

        return null;
    }
}
