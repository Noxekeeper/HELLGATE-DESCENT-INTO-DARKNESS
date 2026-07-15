using System.Reflection;
using HarmonyLib;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Systems.Diagnostics.Tentacle;

/// <summary>
/// Reflection helpers + per-instance snapshot for tentacle H-scene state.
/// Tentacle / Trap_TentacleIronmaiden hide all their visual references behind
/// <c>[SerializeField] private</c> fields; this struct centralizes the access so the
/// monitor and the lifecycle patches share one source of truth.
/// </summary>
internal struct TentacleHSceneSnapshot
{
    public int InstanceId;
    public string TypeName;
    public string ObjectName;

    /// <summary>Underlying actor's <c>eroflag</c> field (true while H-scene is active).</summary>
    public bool ActorEroflag;

    /// <summary>Whether the actor GameObject is alive and active in hierarchy.</summary>
    public bool ActorAlive;

    /// <summary>Whether the <c>erodata</c> child / partner GameObject is active. The H-scene visual + state machine lives here.</summary>
    public bool ErodataActive;
    public string ErodataName;

    /// <summary>Current animation on the H-scene spine (<c>erospine</c>).</summary>
    public string ErospineAnim;
    public bool ErospineEnabled;

    /// <summary>Current animation on the actor's main spine (<c>myspine</c>).</summary>
    public string MyspineAnim;
    public bool MyspineRendererEnabled;

    public float ActorHp;

    public float TimeScale;

    public bool PlayerEroflag;
    public int PlayerErodown;
    public string PlayerState;

    public bool IsEqual(TentacleHSceneSnapshot other)
    {
        return ActorEroflag == other.ActorEroflag
            && ActorAlive == other.ActorAlive
            && ErodataActive == other.ErodataActive
            && ErospineEnabled == other.ErospineEnabled
            && MyspineRendererEnabled == other.MyspineRendererEnabled
            && string.Equals(ErospineAnim, other.ErospineAnim)
            && string.Equals(MyspineAnim, other.MyspineAnim)
            && Mathf.Approximately(ActorHp, other.ActorHp)
            && PlayerEroflag == other.PlayerEroflag
            && PlayerErodown == other.PlayerErodown
            && string.Equals(PlayerState, other.PlayerState);
    }

    public override string ToString()
    {
        return $"{TypeName}/{ObjectName}#{InstanceId} actorEro={ActorEroflag} alive={ActorAlive} erodata.active={ErodataActive}({ErodataName}) " +
               $"erospine[{ErospineAnim}, en={ErospineEnabled}] myspine[{MyspineAnim}, render={MyspineRendererEnabled}] hp={ActorHp:0.##} " +
               $"player[ero={PlayerEroflag}, down={PlayerErodown}, state={PlayerState}] timeScale={TimeScale:0.##}";
    }
}

internal static class TentacleHSceneReflection
{
    // Reflection cached for the private [SerializeField] fields. erodata + eroflag are public
    // on EnemyDate / Trapdata so they need no reflection.
    private static FieldInfo s_tentacleErospine;
    private static FieldInfo s_tentacleMyspine;
    private static FieldInfo s_tentacleMyspineRendr;

    private static FieldInfo s_trapErospine;
    private static FieldInfo s_trapMyspine;
    private static FieldInfo s_trapMyspineRendr;

    private static playercon s_cachedPlayer;

    public static TentacleHSceneSnapshot CaptureFromTentacle(global::Tentacle t)
    {
        var snap = new TentacleHSceneSnapshot();
        if (t == null) return snap;

        snap.InstanceId = t.GetInstanceID();
        snap.TypeName = "Tentacle";
        snap.ObjectName = t.gameObject != null ? t.gameObject.name : "<destroyed>";
        snap.ActorAlive = t.gameObject != null && t.gameObject.activeInHierarchy;
        snap.ActorEroflag = t.eroflag;
        snap.ActorHp = t.Hp;

        snap.ErodataActive = t.erodata != null && t.erodata.activeInHierarchy;
        snap.ErodataName = t.erodata != null ? t.erodata.name : "<null>";

        EnsureTentacleFields();

        SkeletonAnimation erospine = ReadField<SkeletonAnimation>(s_tentacleErospine, t);
        snap.ErospineEnabled = erospine != null && erospine.enabled;
        snap.ErospineAnim = erospine != null ? erospine.AnimationName ?? string.Empty : "<null>";

        SkeletonAnimation myspine = ReadField<SkeletonAnimation>(s_tentacleMyspine, t);
        snap.MyspineAnim = myspine != null ? myspine.AnimationName ?? string.Empty : "<null>";

        MeshRenderer myrend = ReadField<MeshRenderer>(s_tentacleMyspineRendr, t);
        snap.MyspineRendererEnabled = myrend != null && myrend.enabled;

        FillPlayerSection(ref snap);
        snap.TimeScale = Time.timeScale;
        return snap;
    }

    public static TentacleHSceneSnapshot CaptureFromTrap(global::Trap_TentacleIronmaiden t)
    {
        var snap = new TentacleHSceneSnapshot();
        if (t == null) return snap;

        snap.InstanceId = t.GetInstanceID();
        snap.TypeName = "Trap_TentacleIronmaiden";
        snap.ObjectName = t.gameObject != null ? t.gameObject.name : "<destroyed>";
        snap.ActorAlive = t.gameObject != null && t.gameObject.activeInHierarchy;
        snap.ActorEroflag = t.eroflag;
        snap.ActorHp = t.Hp;

        snap.ErodataActive = t.erodata != null && t.erodata.activeInHierarchy;
        snap.ErodataName = t.erodata != null ? t.erodata.name : "<null>";

        EnsureTrapFields();

        SkeletonAnimation erospine = ReadField<SkeletonAnimation>(s_trapErospine, t);
        snap.ErospineEnabled = erospine != null && erospine.enabled;
        snap.ErospineAnim = erospine != null ? erospine.AnimationName ?? string.Empty : "<null>";

        SkeletonAnimation myspine = ReadField<SkeletonAnimation>(s_trapMyspine, t);
        snap.MyspineAnim = myspine != null ? myspine.AnimationName ?? string.Empty : "<null>";

        MeshRenderer myrend = ReadField<MeshRenderer>(s_trapMyspineRendr, t);
        snap.MyspineRendererEnabled = myrend != null && myrend.enabled;

        FillPlayerSection(ref snap);
        snap.TimeScale = Time.timeScale;
        return snap;
    }

    private static void FillPlayerSection(ref TentacleHSceneSnapshot snap)
    {
        playercon pc = ResolvePlayer();
        if (pc == null)
        {
            snap.PlayerEroflag = false;
            snap.PlayerErodown = 0;
            snap.PlayerState = "<null>";
            return;
        }
        snap.PlayerEroflag = pc.eroflag;
        snap.PlayerErodown = pc.erodown;
        snap.PlayerState = pc.state ?? string.Empty;
    }

    private static playercon ResolvePlayer()
    {
        if (s_cachedPlayer != null) return s_cachedPlayer;
        try
        {
            GameObject obj = GameObject.FindGameObjectWithTag("Player");
            s_cachedPlayer = obj != null ? obj.GetComponent<playercon>() : null;
        }
        catch
        {
            s_cachedPlayer = null;
        }
        return s_cachedPlayer;
    }

    public static void ResetCachedPlayer() => s_cachedPlayer = null;

    private static T ReadField<T>(FieldInfo field, object instance) where T : class
    {
        if (field == null || instance == null) return null;
        try { return field.GetValue(instance) as T; }
        catch { return null; }
    }

    private static void EnsureTentacleFields()
    {
        if (s_tentacleErospine != null) return;
        var t = typeof(global::Tentacle);
        s_tentacleErospine = AccessTools.Field(t, "erospine");
        s_tentacleMyspine = AccessTools.Field(t, "myspine");
        s_tentacleMyspineRendr = AccessTools.Field(t, "myspinerennder");
    }

    private static void EnsureTrapFields()
    {
        if (s_trapErospine != null) return;
        var t = typeof(global::Trap_TentacleIronmaiden);
        s_trapErospine = AccessTools.Field(t, "erospine");
        s_trapMyspine = AccessTools.Field(t, "mySpine");
        s_trapMyspineRendr = AccessTools.Field(t, "myspinerennder");
    }
}
