using UnityEngine;

namespace NoREroMod.Systems.Spawn;

/// <summary>Marks HellGate-managed spawns for altar hot-reload cleanup + F11 authoring edit link.</summary>
internal sealed class SpawnManagedInstance : MonoBehaviour
{
    internal bool SpawnHostileToPlayer;
    internal bool SuppressFactionMarker;

    /// <summary>True when this instance was created from a HellGate pack / authoring append.</summary>
    internal bool HasAuthoringLink;

    internal string AuthoringPackPath = string.Empty;
    /// <summary>0-based index into the pack file lines array.</summary>
    internal int AuthoringLineIndex = -1;
    internal string AuthoringSourceLineRaw = string.Empty;
    internal float AuthoringSpawnX;
    internal float AuthoringSpawnY;
    internal string AuthoringEnemyKey = string.Empty;
    internal string AuthoringFactionIdRaw = string.Empty;
    /// <summary>1 = static line; below 1 = RANDOM chance.</summary>
    internal float AuthoringChance = 1f;
    internal int AuthoringCount = 1;
    internal bool AuthoringFlipX;

    /// <summary>Z rotation from pack line (rot90 / rot180 / rot270).</summary>
    internal float AuthoringRotationZ;

    /// <summary>Pack-line sortingOrder delta (<c>sort±N</c>). Not the near/far presets.</summary>
    internal int AuthoringSortOffset;

    /// <summary>True when pack line is TRAP/OBJECT/DECOR/HOSTAGE (not an enemy XY line).</summary>
    internal bool AuthoringIsTemplate;

    /// <summary>Force NoREroMod elite (&lt;SUPER&gt;) for this HellGate spawn.</summary>
    internal bool ForceElite;

    /// <summary>Authoring/pack flag: line had |elite=1.</summary>
    internal bool AuthoringForceElite;

    /// <summary>Set only by F11 drag/nudge — never by in-game AI/physics. RMB writes these.</summary>
    internal bool AuthoringMovedInEditor;

    internal void MarkMovedInEditor()
    {
        AuthoringMovedInEditor = true;
    }

    internal void ApplyAuthoringLink(
        string packPath,
        int lineIndex,
        string sourceLineRaw,
        float spawnX,
        float spawnY,
        string enemyKey,
        string factionIdRaw,
        float chance,
        int count,
        bool flipX,
        bool forceElite = false,
        float rotationZ = 0f,
        int sortOffset = 0,
        bool isTemplate = false)
    {
        HasAuthoringLink = !string.IsNullOrEmpty(packPath) && lineIndex >= 0 && !string.IsNullOrEmpty(enemyKey);
        AuthoringPackPath = packPath ?? string.Empty;
        AuthoringLineIndex = lineIndex;
        AuthoringSourceLineRaw = sourceLineRaw ?? string.Empty;
        AuthoringSpawnX = spawnX;
        AuthoringSpawnY = spawnY;
        AuthoringEnemyKey = enemyKey ?? string.Empty;
        AuthoringFactionIdRaw = factionIdRaw ?? string.Empty;
        AuthoringChance = chance > 0f ? chance : 1f;
        AuthoringCount = count > 0 ? count : 1;
        AuthoringFlipX = flipX;
        AuthoringForceElite = forceElite;
        AuthoringRotationZ = rotationZ;
        AuthoringSortOffset = sortOffset;
        AuthoringIsTemplate = isTemplate ||
                              SpawnAuthoringPackEdit.IsTemplateAuthoringLine(sourceLineRaw);
        AuthoringMovedInEditor = false;
        if (forceElite)
            ForceElite = true;
    }

    internal void ClearAuthoringLink()
    {
        HasAuthoringLink = false;
        AuthoringPackPath = string.Empty;
        AuthoringLineIndex = -1;
        AuthoringSourceLineRaw = string.Empty;
        AuthoringEnemyKey = string.Empty;
    }
}
