using HarmonyLib;
using MoreInjuries.Defs.WellKnown;
using MoreInjuries.HealthConditions.BrainDamage;
using MoreInjuries.HealthConditions.VitalFailure;
using MoreInjuries.Integrations.DeathRattle;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Verse;
using Verse.AI;

namespace MoreInjuries.Versioning;

public sealed class GameComponent_DeathRattleTransition : GameComponent
{
    private static readonly HashSet<Pawn> s_loadedPawns = [];
    private int _migrationVersion;
    private bool _noticeShown;
    private bool _ready;
    private readonly HashSet<Bill> _changedBills = [];
    internal readonly Dictionary<string, string> ReferenceReplacements = [];
    internal bool Ready => _ready && _migrationVersion == 1;

    public GameComponent_DeathRattleTransition(Game game) => s_loadedPawns.Clear();
    internal static void Register(Pawn pawn)
    {
        if (DeathRattleBridge.Active) { s_loadedPawns.Add(pawn); }
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref _migrationVersion, "deathRattleMigrationVersion", 0);
        Scribe_Values.Look(ref _noticeShown, "deathRattleTransitionNoticeShown", false);
    }

    private static HediffDef? Destination(Hediff source) => source.def.defName switch
    {
        "ClinicalDeathNoHeartbeat" => KnownHediffDefOf.CardiacArrest,
        "ClinicalDeathAsphyxiation" => VitalFailureDefOf.MI_RespiratoryFailure,
        "LiverFailure" => VitalFailureDefOf.MI_LiverFailure,
        "KidneyFailure" => VitalFailureDefOf.MI_RenalFailure,
        "IntestinalFailure" => VitalFailureDefOf.MI_MetabolicFailure,
        "Coma" => VitalFailureDefOf.MI_Coma,
        "BrainDamage" => NeurologicalDefOf.MI_DiffuseBrainDamage,
        "ArtificialComa" => NeurologicalDefOf.MI_ArtificialComa,
        _ => null
    };

    public override void LoadedGame()
    {
        if (!DeathRattleBridge.Active) { s_loadedPawns.Clear(); return; }
        DeathRattleBridge.Enforce();
        List<PawnSnapshot> snapshots = [];
        try
        {
            if (!DeathRattleBridge.Enforced) { throw new InvalidOperationException("MI_DR_PriorityFailed".Translate()); }
            if (!MoreInjuriesMod.Settings.EnableDelayedVitalFailure) { throw new InvalidOperationException("MI_DR_Disabled".Translate()); }
            if (DeathRattleBridge.DependencyBlocker() is { } dependency) { throw new InvalidOperationException("MI_DR_DependentMod".Translate(dependency)); }
            // Full preflight before the first condition or bill is changed.
            foreach (Pawn pawn in s_loadedPawns)
            {
                foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
                {
                    if (!DeathRattleBridge.Owns(hediff.def)) { continue; }
                    if (hediff.GetType() != typeof(Hediff) && hediff.GetType() != typeof(HediffWithComps))
                    { throw new InvalidOperationException("MI_DR_UnsupportedState".Translate(pawn.LabelShort, hediff.GetType().FullName)); }
                    if (hediff.def.comps is { } components)
                    {
                        foreach (HediffCompProperties component in components)
                        {
                            string? name = component.compClass?.FullName;
                            if (component.compClass == typeof(HediffComp_SeverityPerDay) || component.compClass == typeof(HediffComp_TendDuration)
                                || name is "DeathRattle.HediffComp_RemoveWhenHasCapacity" or "DeathRattle.HediffComp_RemoveWhenHasPart"
                                or "DeathRattle.HediffComp_StabilizeIfOtherHediff" or "DeathRattle.HediffComp_TendSeverity") { continue; }
                            throw new InvalidOperationException("MI_DR_UnsupportedState".Translate(pawn.LabelShort, name));
                        }
                    }
                    if (Destination(hediff) is null || !pawn.RaceProps.IsFlesh || hediff.Severity > Destination(hediff)!.maxSeverity)
                    { throw new InvalidOperationException(pawn.LabelShort + ": " + hediff.def.defName); }
                    if (hediff.def.defName == "ArtificialComa" && !Recipe_InduceArtificialComa.CanReverse(pawn))
                    { throw new InvalidOperationException("MI_DR_ComaRestriction".Translate(pawn.LabelShort)); }
                }
                if (pawn.BillStack is { } bills)
                {
                    foreach (Bill bill in bills.Bills)
                    {
                        if (DeathRattleBridge.Owns(bill.recipe) && DeathRattleBridge.Replacement(bill.recipe) is null)
                        { throw new InvalidOperationException(bill.recipe.defName); }
                    }
                }
            }
            foreach (Pawn pawn in s_loadedPawns) { snapshots.Add(new PawnSnapshot(pawn)); }
            foreach (Pawn pawn in s_loadedPawns) { Migrate(pawn); }
            foreach (Pawn actor in s_loadedPawns)
            {
                if (actor.CurJob?.bill is { } active && _changedBills.Contains(active))
                { actor.jobs.EndCurrentJob(JobCondition.Incompletable); }
            }
            _migrationVersion = 1;
            _ready = true;
            if (!_noticeShown)
            {
                _noticeShown = true;
                Find.LetterStack.ReceiveLetter("MI_DR_TransitionTitle".Translate(), "MI_DR_TransitionReady".Translate(), LetterDefOf.NeutralEvent);
            }
        }
        catch (Exception exception)
        {
            _ready = false;
            foreach (PawnSnapshot snapshot in snapshots) { snapshot.Restore(); }
            ReferenceReplacements.Clear();
            Log.Error("More Injuries Death Rattle transition: " + exception);
            Find.LetterStack.ReceiveLetter("MI_DR_TransitionTitle".Translate(), "MI_DR_TransitionBlocked".Translate(exception.Message), LetterDefOf.NegativeEvent);
        }
        finally { DeathRattleBridge.MigratingPawn = null; s_loadedPawns.Clear(); }
    }

    private void Migrate(Pawn pawn)
    {
        DeathRattleBridge.MigratingPawn = pawn;
        List<Hediff> sources = pawn.health.hediffSet.hediffs.FindAll(h => DeathRattleBridge.Owns(h.def));
        foreach (Hediff source in sources)
        {
            HediffDef target = Destination(source)!;
            Hediff? destination = pawn.health.hediffSet.hediffs.Find(h => h.def == target && h.Part == source.Part);
            if (destination is null)
            {
                destination = HediffMaker.MakeHediff(target, pawn, source.Part);
                destination.Severity = source.Severity;
                destination.ageTicks = source.ageTicks;
                // Preserve native references to this condition, rather than creating dangling IDs.
                FieldInfo id = AccessTools.Field(typeof(Hediff), "loadID");
                id.SetValue(destination, id.GetValue(source));
                VitalFailureComp? controller = VitalFailureComp.For(pawn);
                if (controller is not null) { controller.CreatingCirculatoryFailure = true; }
                try { pawn.health.AddHediff(destination); }
                finally { if (controller is not null) { controller.CreatingCirculatoryFailure = false; } }
                if (destination.TryGetComp<HediffComp_VitalArrest>() is { } arrest) { arrest.MechanicalFailure = true; }
            }
            else
            {
                destination.Severity = Math.Max(destination.Severity, source.Severity);
                ReferenceReplacements[source.GetUniqueLoadID()] = destination.GetUniqueLoadID();
            }
            if (source.TryGetComp<HediffComp_TendDuration>() is { } oldTend && destination.TryGetComp<HediffComp_TendDuration>() is { } newTend)
            {
                newTend.tendQuality = oldTend.tendQuality;
                FieldInfo ticks = AccessTools.Field(typeof(HediffComp_TendDuration), "tendTicksLeft");
                ticks.SetValue(newTend, ticks.GetValue(oldTend));
            }
            pawn.health.RemoveHediff(source);
        }
        if (pawn.BillStack is { } stack)
        {
            foreach (Bill bill in stack.Bills)
            {
                if (!DeathRattleBridge.Owns(bill.recipe)) { continue; }
                bill.recipe = DeathRattleBridge.Replacement(bill.recipe)!;
                if (bill.recipe == NeurologicalDefOf.MI_InduceArtificialComa)
                {
                    // Preserve narrower player selections, but never keep herbal as a valid ingredient.
                    foreach (ThingDef thing in bill.ingredientFilter.AllowedThingDefs.ToArray())
                    {
                        if (!bill.recipe.fixedIngredientFilter.Allows(thing)) { bill.ingredientFilter.SetAllow(thing, false); }
                    }
                }
                _changedBills.Add(bill);
            }
        }
        DeathRattleBridge.MigratingPawn = null;
        VitalFailureComp.For(pawn)?.Resume();
        VitalFailureComp.For(pawn)?.Refresh();
    }
    private sealed class PawnSnapshot
    {
        private readonly Pawn _pawn;
        private readonly Hediff[] _hediffs;
        private readonly float[] _severities;
        private readonly List<(Bill Bill, RecipeDef Recipe, ThingDef[] Ingredients)> _bills = [];
        internal PawnSnapshot(Pawn pawn)
        {
            _pawn = pawn;
            _hediffs = pawn.health.hediffSet.hediffs.ToArray();
            _severities = _hediffs.Select(h => h.Severity).ToArray();
            if (pawn.BillStack is { } stack)
            {
                foreach (Bill bill in stack.Bills) { _bills.Add((bill, bill.recipe, bill.ingredientFilter.AllowedThingDefs.ToArray())); }
            }
        }
        internal void Restore()
        {
            DeathRattleBridge.MigratingPawn = _pawn;
            _pawn.health.hediffSet.hediffs.Clear();
            _pawn.health.hediffSet.hediffs.AddRange(_hediffs);
            for (int i = 0; i < _hediffs.Length; i++) { _hediffs[i].Severity = _severities[i]; }
            foreach ((Bill bill, RecipeDef recipe, ThingDef[] ingredients) in _bills)
            {
                bill.recipe = recipe;
                bill.ingredientFilter.SetDisallowAll();
                foreach (ThingDef ingredient in ingredients) { bill.ingredientFilter.SetAllow(ingredient, true); }
            }
            _pawn.health.hediffSet.DirtyCache();
            VitalFailureComp.For(_pawn)?.Invalidate();
        }
    }

}
