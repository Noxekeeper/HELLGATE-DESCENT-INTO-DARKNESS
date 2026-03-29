using System;
using System.Linq;
using UnityEngine;
using NoREroMod;

namespace NoREroMod.Systems.Dialogue;

/// <summary>
/// Select onomatopoeia from data arrays
/// </summary>
internal class DialogueSelector
{
    private readonly DialogueDatabase _database;

    internal DialogueSelector(DialogueDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    /// <summary>
    /// Select onomatopoeia for animation and se_count with segmentation support
    /// </summary>
    internal string SelectOnomatopoeia(string animationName, int seCount, float timeSinceSoundStart = 0f)
    {
        if (string.IsNullOrEmpty(animationName) || _database == null)
        {
            return string.Empty;
        }

        string animUpper = animationName.ToUpperInvariant();

        // PRIORITY 1: Check SoundRegistry (universal sounds)
        // Will be used when we intercept MasterAudio.PlaySound
        // Leave for future integration for now

        // PRIORITY 2: Check if segments exist for this sound
        if (_database.HasSegments(animUpper, seCount))
        {
            // Calculate current segment (each segment = 1 second)
            int currentSegment = Mathf.FloorToInt(timeSinceSoundStart) + 1;
            int maxSegments = _database.GetSegmentCount(animUpper, seCount);
            
            // Limit segment to maximum value
            if (currentSegment > maxSegments)
            {
                currentSegment = maxSegments;
            }
            
            // If segment is valid, use it
            if (currentSegment > 0 && currentSegment <= maxSegments)
            {
                string[] segmentArray = _database.GetSegmentOnomatopoeia(animUpper, seCount, currentSegment);
                if (segmentArray != null && segmentArray.Length > 0)
                {
                    return segmentArray[UnityEngine.Random.Range(0, segmentArray.Length)];
                }
            }
        }

        // Fallback: use regular arrays if segments don't exist or segment not found
        string[] mutudeSpecific = _database.GetMutudeOnomatopoeia(animUpper, seCount);
        if (mutudeSpecific != null && mutudeSpecific.Length > 0)
        {
            return mutudeSpecific[UnityEngine.Random.Range(0, mutudeSpecific.Length)];
        }

        string category = GetCategoryForAnimation(animUpper, seCount);
        string[] categoryArray = _database.GetOnomatopoeiaByCategory(category);
        
        if (categoryArray != null && categoryArray.Length > 0)
        {
            return categoryArray[UnityEngine.Random.Range(0, categoryArray.Length)];
        }

        return string.Empty;
    }

    /// <summary>
    /// Determine onomatopoeia category by animation
    /// </summary>
    private string GetCategoryForAnimation(string animationName, int seCount)
    {
        string animUpper = animationName?.ToUpperInvariant() ?? string.Empty;

        if (animUpper == "FIN" || animUpper == "FIN2" || animUpper == "ERO5")
        {
            return "ClimaxBurst";
        }

        if (animUpper == "ERO3" || animUpper == "ERO4")
        {
            if (seCount == 2 || seCount == 4)
            {
                return "SlimeWet";
            }
            return "ThrustSFX";
        }

        if (animUpper == "ERO2" || animUpper == "ERO2_2")
        {
            return "StaminaEffort";
        }

        if (animUpper == "ERO1" || animUpper == "ERO1_2")
        {
            return "StaminaEffort";
        }

        if (animUpper == "START")
        {
            return "StaminaEffort";
        }

        if (animUpper == "DRINK" || animUpper == "DRINK_END")
        {
            return "SlimeWet";
        }

        return "ThrustSFX";
    }
}

