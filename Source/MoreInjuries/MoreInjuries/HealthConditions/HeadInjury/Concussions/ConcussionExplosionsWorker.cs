using MoreInjuries.Defs.WellKnown;
using RimWorld;
using UnityEngine;
using Verse;

namespace MoreInjuries.HealthConditions.HeadInjury.Concussions;

#region Concussion Severity Configuration
// ============================================================================
// CONCUSSION SEVERITY CONFIGURATION - Adjust these values to balance gameplay
// ============================================================================

/// <summary>
/// Body part damage multipliers for concussion generation.
/// Higher values = more concussion damage from the same hit.
/// These represent the "importance" of each brain region:
/// - Head (outer protection): Lowest multiplier, represents deflected/reduced damage
/// - Skull (bone): Medium multiplier, represents structural damage transmission
/// - Brain (core): Highest multiplier, represents direct neural tissue damage
/// </summary>
internal static class ConcussionBodyPartMultipliers
{
    /// <summary>Head - Outer head region, provides some protection. Least severe concussion source.</summary>
    public const float Head = 0.5f;
    
    /// <summary>Skull - Bone structure, medium severity. Impacts can transmit through bone.</summary>
    public const float Skull = 1.5f;
    
    /// <summary>Brain - Direct neural tissue damage, most severe concussion source.</summary>
    public const float Brain = 3.0f;
    
    /// <summary>Default multiplier for unknown/unrecognized head-related body parts.</summary>
    public const float Default = 1.0f;
}

/// <summary>
/// Damage type-to-concussion percentage mapping.
/// Represents how much concussion % is generated per 1 point of actual received damage.
/// Higher values = more concussion per damage.
/// 
/// These percentages represent the traumatic effect of different damage types:
/// - Blunt force (Blunt, Bomb, Thermobaric): High concussion (20-25%)
/// - Penetrating (Bullet, Arrow, Stab): Moderate concussion (5-10%)
/// - Special effects (Crush, Nerve): Varies by mechanism
/// - Other: Conservative default value for unrecognized mod damage types
/// </summary>
internal static class ConcussionDamageTypePercentages
{
    /// <summary>Blunt force trauma - HIGH concussion. Whole-head impact.</summary>
    public const float Blunt = 0.20f; // 20% concussion per 1 damage
    
    /// <summary>Explosive damage - HIGHEST concussion. Blast pressure and fragmentation.</summary>
    public const float Bomb = 0.25f; // 25% concussion per 1 damage
    
    /// <summary>Thermobaric/fuel-air explosives - VERY HIGH concussion. Extreme blast pressure.</summary>
    public const float Thermobaric = 0.25f; // 25% concussion per 1 damage
    
    /// <summary>Bullet impact - LOW concussion. Focused penetrating wound, less whole-head trauma.</summary>
    public const float Bullet = 0.05f; // 5% concussion per 1 damage
    
    /// <summary>Arrow/Bolt impact - LOW concussion. Similar to bullets, penetrating impact.</summary>
    public const float Arrow = 0.05f; // 5% concussion per 1 damage
    
    /// <summary>High-velocity arrows (Combat Extended) - LOW-MODERATE concussion.</summary>
    public const float ArrowHighVelocity = 0.08f; // 8% concussion per 1 damage
    
    /// <summary>Stabbing wound - VERY LOW concussion. Minimal blunt force.</summary>
    public const float Stab = 0.02f; // 2% concussion per 1 damage
    
    /// <summary>Crushing damage - HIGH concussion. Compressive trauma similar to blunt force.</summary>
    public const float Crush = 0.18f; // 18% concussion per 1 damage
    
    /// <summary>Bite damage - MODERATE concussion. Localized impact with some force.</summary>
    public const float Bite = 0.10f; // 10% concussion per 1 damage
    
    /// <summary>Toxic/Caustic damage - LOW concussion. Chemical burns don't cause concussions effectively.</summary>
    public const float Toxic = 0.01f; // 1% concussion per 1 damage
    
    /// <summary>Nerve/EMP damage - MODERATE concussion. Electrical/neural stimulation.</summary>
    public const float Nerve = 0.12f; // 12% concussion per 1 damage
    
    /// <summary>Energy/Plasma bolts - MODERATE-HIGH concussion. Heat + impact.</summary>
    public const float EnergyBolt = 0.15f; // 15% concussion per 1 damage
    
    /// <summary>Beanbag/Non-lethal impact - MODERATE concussion. Designed for blunt trauma.</summary>
    public const float Beanbag = 0.12f; // 12% concussion per 1 damage
    
    /// <summary>Unknown damage types from mods - CONSERVATIVE default.</summary>
    public const float Default = 0.08f; // 8% concussion per 1 damage (conservative middle ground)
}
#endregion

internal sealed class ConcussionExplosionsWorker(MoreInjuryComp parent) : InjuryWorker(parent), IPostPostApplyDamageHandler
{

    public override bool IsEnabled => MoreInjuriesMod.Settings.EnableConcussion;

    public void PostPostApplyDamage(ref readonly DamageInfo dinfo)
    {
        Pawn patient = Pawn;
        BodyPartRecord? hitPart = dinfo.HitPart;
        if (hitPart is null || !IsHeadPart(hitPart))
        {
            return;
        }

        BodyPartRecord? brain = patient.health.hediffSet.GetBrain();
        if (brain is null)
        {
            return;
        }

        float receivedDamage = dinfo.Amount;
        if (receivedDamage <= 0f)
        {
            return;
        }

        float concussionPercentPerDamage = GetConcussionPercentageForDamage(dinfo.Def);
        float bodyPartMultiplier = GetBodyPartMultiplier(hitPart);
        float concussionSeverity = CalculateConcussionSeverity(
            receivedDamage,
            bodyPartMultiplier,
            concussionPercentPerDamage,
            hitPart) * MoreInjuriesMod.Settings.ConcussionChance;

        if (concussionSeverity < 0.01f)
        {
            return;
        }

        if (!patient.health.hediffSet.TryGetHediff(KnownHediffDefOf.Concussion, out Hediff? concussion))
        {
            concussion = HediffMaker.MakeHediff(KnownHediffDefOf.Concussion, patient);
            patient.health.AddHediff(concussion, brain);
        }

        concussion.Severity = Mathf.Min(1.0f, concussion.Severity + concussionSeverity);
    }

    private static bool IsHeadPart(BodyPartRecord part)
    {
        // Check if it's the brain, skull, or any part in the head group
        if (part.def == KnownBodyPartDefOf.Brain || 
            part.def == KnownBodyPartDefOf.Skull ||
            part.def == BodyPartDefOf.Head)
        {
            return true;
        }
        
        // Check if it's a child/subpart of the head
        for (BodyPartRecord? current = part.parent; current is not null; current = current.parent)
        {
            if (current.def == BodyPartDefOf.Head)
            {
                return true;
            }
        }
        
        return false;
    }

    private static float GetBodyPartMultiplier(BodyPartRecord hitPart)
    {
        // Direct hits to the brain are most severe
        if (hitPart.def == KnownBodyPartDefOf.Brain)
        {
            return ConcussionBodyPartMultipliers.Brain;
        }
        
        // Skull hits are medium severity
        if (hitPart.def == KnownBodyPartDefOf.Skull)
        {
            return ConcussionBodyPartMultipliers.Skull;
        }
        
        // Generic head hits are least severe (dispersed impact)
        if (hitPart.def == BodyPartDefOf.Head)
        {
            return ConcussionBodyPartMultipliers.Head;
        }
        
        // Other head sub-parts (eyes, ears, jaw, etc.) get medium severity
        if (hitPart.parent?.def == BodyPartDefOf.Head || hitPart.parent?.def == KnownBodyPartDefOf.Skull)
        {
            return ConcussionBodyPartMultipliers.Skull;
        }
        
        return ConcussionBodyPartMultipliers.Default;
    }

    // Gets the concussion percentage generation for a specific damage type.
    // Returns the percentage of concussion created per 1 point of damage.
    // Unknown damage types get the default conservative value.
    private static float GetConcussionPercentageForDamage(DamageDef? damageDef)
    {
        if (damageDef is null)
        {
            return ConcussionDamageTypePercentages.Default;
        }
        
        string damageDefName = damageDef.defName;
        
        // Match against known damage types
        return damageDefName switch
        {
            "Blunt" => ConcussionDamageTypePercentages.Blunt,
            "Bomb" => ConcussionDamageTypePercentages.Bomb,
            "BombSuper" => ConcussionDamageTypePercentages.Bomb,
            "Bullet" => ConcussionDamageTypePercentages.Bullet,
            "BulletToxic" => ConcussionDamageTypePercentages.Bullet,
            "Stab" => ConcussionDamageTypePercentages.Stab,
            "Crush" => ConcussionDamageTypePercentages.Crush,
            
            "Arrow" => ConcussionDamageTypePercentages.Arrow,
            "ArrowHighVelocity" => ConcussionDamageTypePercentages.ArrowHighVelocity,
            "Bite" => ConcussionDamageTypePercentages.Bite,
            "BiteToxic" => ConcussionDamageTypePercentages.Toxic,
            "Beanbag" => ConcussionDamageTypePercentages.Beanbag,
            "EnergyBolt" => ConcussionDamageTypePercentages.EnergyBolt,
            "Nerve" => ConcussionDamageTypePercentages.Nerve,
            "Thermobaric" => ConcussionDamageTypePercentages.Thermobaric,
            
            // Unknown damage type - use conservative default
            _ => ConcussionDamageTypePercentages.Default
        };
    }

    // Calculates the final concussion severity based on received damage, body part sensitivity,
    // and damage type, with scaling based on target body part's HP.
    private static float CalculateConcussionSeverity(
        float receivedDamage,
        float bodyPartMultiplier,
        float concussionPercentPerDamage,
        BodyPartRecord targetPart)
    {
        float baseConcussion = receivedDamage * concussionPercentPerDamage * bodyPartMultiplier;
        float healthScaling = 1.0f;
        if (targetPart.def.hitPoints > 0)
        {
            const float REFERENCE_HIT_POINTS = 10.0f;
            healthScaling = REFERENCE_HIT_POINTS / targetPart.def.hitPoints;
        }

        return Mathf.Clamp(baseConcussion * healthScaling, 0f, 1.0f);
    }
}
