using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoreInjuries.AI.NpcCombatMedicine;
using MoreInjuries.Defs.WellKnown;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MoreInjuries.HarmonyTests;

[TestClass, DoNotParallelize]
public sealed class NpcDepartureTests
{
    private const string OWNER = "MoreInjuries.test.Departure";
    [TestInitialize]
    public void Initialize()
    {
        new NpcPainCareTests().Initialize();
        NpcMedicalAi.OnDepartureCare = null; NpcMedicalAi.OnPreviewBleeding = null; NpcExtraction.OnPreview = null;
    }
    [TestCleanup]
    public void Cleanup() { new Harmony(OWNER).UnpatchAll(OWNER); NpcMedicalAi.OnDepartureCare = null; }
    private static (Pawn Actor, Pawn Patient, Lord Lord) Pair(bool pain = true)
    {
        Lord lord = new();
        Pawn actor = new() { Lord = lord, Position = new(20, 0, 20) };
        Pawn patient = new() { Map = actor.Map, Position = new(40, 0, 20), Downed = true, RawPain = pain ? .9f : 0f };
        actor.Map.mapPawns.AllPawnsSpawned.AddRange([actor, patient]); lord.ownedPawns.AddRange([actor, patient]);
        actor.CurJob = Exit(new(0, 0, 20));
        return (actor, patient, lord);
    }
    private static Job Exit(IntVec3 cell) => new() { def = JobDefOf.Goto, exitMapOnArrival = true, targetA = new(cell) };
    private static Thing Dose(Pawn actor)
    {
        Thing dose = new() { def = ThingDefOf.GoJuice, ParentHolder = actor.inventory, Spawned = false };
        actor.inventory.innerContainer.Items.Add(dose); return dose;
    }
    private static Thing Loot(Pawn actor)
    {
        Thing loot = new() { ParentHolder = actor.carryTracker, Spawned = false, stackCount = 1 };
        actor.carryTracker.innerContainer.Items.Add(loot); return loot;
    }
    private static void Hooks()
    {
        Harmony harmony = new(OWNER);
        harmony.CreateClassProcessor(typeof(NpcExtractionCarryPatch)).Patch();
        harmony.CreateClassProcessor(typeof(NpcDepartureTheftPatch)).Patch();
        harmony.CreateClassProcessor(typeof(NpcDepartureGotoPatch)).Patch();
    }

    [TestMethod]
    public void FourPawnRaidProvidesCareAndAssignsDistinctEvacuationBeforeLoot()
    {
        (Pawn first, Pawn patient, Lord lord) = Pair(); Thing dose = Dose(first);
        Pawn second = new() { Lord = lord, Map = first.Map, Position = new(22, 0, 20), DoctorCapable = false };
        Pawn other = new() { Downed = true, RawPain = 0, Map = first.Map, Position = new(42, 0, 20) };
        first.Map.mapPawns.AllPawnsSpawned.AddRange([second, other]); lord.ownedPawns.AddRange([second, other]);
        NpcDeparture.Result care = NpcDeparture.Decide(first), carry = NpcDeparture.Decide(second);
        Assert.AreEqual(NpcDeparture.Kind.Work, care.Kind); Assert.AreEqual(NpcDeparture.Kind.Work, carry.Kind);
        Assert.AreSame(KnownJobDefOf.MI_NpcPainAssist, care.Job!.def);
        Assert.AreSame(patient, care.Job.targetA.Thing);
        Assert.AreSame(KnownJobDefOf.MI_NpcEvacuate, carry.Job!.def);
        Assert.AreSame(other, carry.Job.targetA.Thing);
        dose.IngestionEffect = _ => { patient.RawPain = .05f; patient.Downed = false; };
        // The shared assist-base fixture treats native travel as a boundary.
        first.Position = patient.Position;
        first.CurJob = care.Job; new JobDriver_NpcPainAssist { pawn = first }.Run();
        second.CurJob = carry.Job; new JobDriver_NpcEvacuate { pawn = second }.Run();
        Assert.IsTrue(dose.Destroyed); Assert.IsFalse(patient.Downed); Assert.IsTrue(second.Exited);
        Assert.AreSame(other, second.carryTracker.CarriedThing);
    }

    [TestMethod]
    public void TwoNonDoctorsClaimTwoDifferentSafeEvacuationsInsteadOfLoot()
    {
        (Pawn first, Pawn patient, Lord lord) = Pair(false); first.DoctorCapable = false;
        Pawn second = new() { Lord = lord, Map = first.Map, Position = new(22, 0, 20), DoctorCapable = false };
        Pawn other = new() { Downed = true, Map = first.Map, Position = new(42, 0, 20) };
        first.Map.mapPawns.AllPawnsSpawned.AddRange([second, other]); lord.ownedPawns.AddRange([second, other]);
        Loot(first); Loot(second);
        Job firstRescue = NpcDeparture.Decide(first).Job!, secondRescue = NpcDeparture.Decide(second).Job!;
        Assert.AreSame(KnownJobDefOf.MI_NpcEvacuate, firstRescue.def);
        Assert.AreSame(KnownJobDefOf.MI_NpcEvacuate, secondRescue.def);
        Assert.AreNotSame(firstRescue.targetA.Thing, secondRescue.targetA.Thing);
        first.CurJob = firstRescue; second.CurJob = secondRescue;
        new JobDriver_NpcEvacuate { pawn = first }.Run(); new JobDriver_NpcEvacuate { pawn = second }.Run();
        Assert.IsTrue(first.Exited); Assert.IsTrue(second.Exited);
        Assert.AreSame(patient, first.carryTracker.CarriedThing); Assert.AreSame(other, second.carryTracker.CarriedThing);
    }

    [TestMethod]
    public void LootIsPlacedOnlyAfterProspectiveCareAndPatientClaimAreFeasible()
    {
        (Pawn actor, Pawn patient, _) = Pair(); Dose(actor); Thing loot = Loot(actor);
        NpcDeparture.Result result = NpcDeparture.Decide(actor);
        Assert.AreEqual(NpcDeparture.Kind.Work, result.Kind); Assert.IsTrue(loot.Spawned);
        Assert.IsNull(actor.carryTracker.CarriedThing); Assert.IsFalse(loot.Destroyed);
        Assert.IsTrue(actor.Map.reservationManager.Claims.Any(claim => claim.Pawn == actor && claim.Target == patient));
        Assert.IsFalse(NpcDeparture.ProspectiveHands(actor));
    }

    [TestMethod]
    public void FailedPlacementKeepsLootAndClearsOnlyProposedClaims()
    {
        (Pawn actor, Pawn patient, _) = Pair(); Dose(actor); Thing loot = Loot(actor);
        actor.carryTracker.DropAllowed = false;
        Thing unrelated = new(); Job unrelatedJob = new(); actor.Map.reservationManager.Reserve(actor, unrelatedJob, unrelated);
        Assert.AreEqual(NpcDeparture.Kind.Released, NpcDeparture.Decide(actor).Kind);
        Assert.AreSame(loot, actor.carryTracker.CarriedThing); Assert.IsFalse(loot.Destroyed);
        Assert.IsFalse(actor.Map.reservationManager.Claims.Any(claim => claim.Target == patient));
        Assert.IsTrue(actor.Map.reservationManager.Claims.Any(claim => claim.Job == unrelatedJob));
    }

    [TestMethod]
    public void UnknownOrUnsafeRoutesDoNotHoldSurvivorsOrDropTheirLoot()
    {
        foreach (bool unknown in new[] { false, true })
        {
            (Pawn actor, Pawn patient, _) = Pair(false); actor.DoctorCapable = false; Thing loot = Loot(actor);
            actor.Map.Risks[patient.Position] = new(unknown ? 0f : 1f, 0, 0, 0, !unknown);
            Assert.AreEqual(NpcDeparture.Kind.Released, NpcDeparture.Decide(actor).Kind);
            Assert.AreSame(loot, actor.carryTracker.CarriedThing); Assert.IsFalse(NpcDeparture.Owns(actor));
        }
    }

    [TestMethod]
    public void DepartureSearchFindsCasualtiesBeyondTwelveCellsAndAfterLordMembershipChanges()
    {
        (Pawn actor, Pawn patient, Lord lord) = Pair(false); lord.ownedPawns.Remove(patient); actor.DoctorCapable = false;
        Assert.IsFalse(patient.Position.InHorDistOf(actor.Position, 12));
        Assert.AreSame(patient, NpcDeparture.Decide(actor).Job!.targetA.Thing);
    }

    [TestMethod]
    public void ApproachOverExistingTravelBudgetDoesNotCreateARescue()
    {
        (Pawn actor, _, _) = Pair(false); actor.DoctorCapable = false;
        actor.Map.pathFinder.Find = (start, end, _) => new() { TotalCost = 2501, NodesReversed = [end, start] };
        Assert.AreEqual(NpcDeparture.Kind.Released, NpcDeparture.Decide(actor).Kind);
    }

    [TestMethod]
    public void SearchBudgetRotatesPastFourRejectedCasualties()
    {
        (Pawn actor, Pawn first, Lord lord) = Pair(false); actor.DoctorCapable = false;
        first.Position = new(21, 0, 20); actor.Map.Risks[first.Position] = new(1, 0, 0, 0);
        for (int index = 1; index <= 4; index++)
        {
            Pawn patient = new() { Downed = true, RawPain = 0, Map = actor.Map, Position = new(21 + index, 0, 20) };
            actor.Map.mapPawns.AllPawnsSpawned.Add(patient); lord.ownedPawns.Add(patient);
            if (index < 4) { actor.Map.Risks[patient.Position] = new(1, 0, 0, 0); }
        }
        Assert.AreEqual(NpcDeparture.Kind.Wait, NpcDeparture.Decide(actor).Kind);
        Find.TickManager.TicksGame += 150;
        NpcDeparture.Result result = NpcDeparture.Decide(actor);
        Assert.AreEqual(NpcDeparture.Kind.Work, result.Kind); Assert.AreEqual(new IntVec3(25, 0, 20), result.Job!.targetA.Pawn.Position);
    }

    [TestMethod]
    public void RecentPatientDamageWaitsSafelyWithoutRestartingTheEpisode()
    {
        (Pawn actor, Pawn patient, _) = Pair(false); actor.DoctorCapable = false;
        DamageInfo damage = new(new() { isRanged = true });
        NpcMedicalSafety.NotifyDamage(patient, damage, 1f);
        Assert.AreEqual(NpcDeparture.Kind.Wait, NpcDeparture.Decide(actor).Kind);
        Find.TickManager.TicksGame += 600;
        Assert.AreEqual(NpcDeparture.Kind.Work, NpcDeparture.Decide(actor).Kind);
    }

    [TestMethod]
    public void NoProgressDeadlineIsLatchedAcrossRepeatedDepartureJobs()
    {
        (Pawn actor, Pawn patient, _) = Pair(false); actor.DoctorCapable = false;
        DamageInfo damage = new(new() { isRanged = true });
        NpcMedicalSafety.NotifyDamage(patient, damage, 1f);
        Assert.AreEqual(NpcDeparture.Kind.Wait, NpcDeparture.Decide(actor).Kind);
        Find.TickManager.TicksGame += 1250;
        for (int index = 0; index < 4; index++)
        {
            actor.CurJob = Exit(new(0, 0, 20));
            Assert.AreEqual(NpcDeparture.Kind.Released, NpcDeparture.Decide(actor, force: true).Kind);
        }
        Assert.IsFalse(NpcDeparture.Owns(actor));
    }

    [TestMethod]
    public void ActualClinicalProgressRefreshesIdleButTotalBudgetStillExpires()
    {
        (Pawn actor, Pawn patient, _) = Pair(); Dose(actor);
        Job work = NpcDeparture.Decide(actor).Job!; actor.CurJob = work;
        NpcMedicalAttempts.Attempt attempt = NpcMedicalAttempts.Begin(actor, work, patient, work.targetB.Thing)!;
        NpcMedicalAttempts.Applying(work); patient.RawPain -= .1f;
        Find.TickManager.TicksGame += 1000;
        NpcMedicalAttempts.Finish(attempt, JobCondition.Succeeded);
        actor.Map.reservationManager.ReleaseClaimedBy(actor, work); actor.CurJob = Exit(new(0, 0, 20));
        Find.TickManager.TicksGame += 300;
        Assert.AreEqual(NpcDeparture.Kind.Work, NpcDeparture.Decide(actor).Kind);
        Find.TickManager.TicksGame += 6200;
        actor.CurJob = Exit(new(0, 0, 20));
        Assert.AreEqual(NpcDeparture.Kind.Released, NpcDeparture.Decide(actor).Kind);
    }

    [TestMethod]
    public void BudgetOrModeChangeStopsNewPickupButAnAlreadyCarriedPatientCanFinish()
    {
        foreach (bool carried in new[] { false, true })
        {
            (Pawn actor, Pawn patient, _) = Pair(false); actor.DoctorCapable = false;
            Job job = NpcDeparture.Decide(actor).Job!; actor.CurJob = job;
            if (carried) { actor.carryTracker.TryStartCarry(patient, 1); }
            MoreInjuriesMod.Settings.NpcMedicalAiMode = 0;
            Assert.AreEqual(carried, NpcDeparture.Continue(actor, job));
            new JobDriver_NpcEvacuate { pawn = actor, ResumeIndex = carried ? 2 : 0 }.Run();
            Assert.AreEqual(carried, actor.Exited);
            MoreInjuriesMod.Settings.NpcMedicalAiMode = 2;
        }
    }

    [TestMethod]
    public void PanicProtectedJobsUnknownGroupsAndLowerModesKeepNativeControl()
    {
        foreach (int exclusion in Enumerable.Range(0, 8))
        {
            (Pawn actor, _, Lord lord) = Pair(); Dose(actor);
            switch (exclusion)
            {
                case 0: MoreInjuriesMod.Settings.NpcMedicalAiMode = 0; break;
                case 1: MoreInjuriesMod.Settings.NpcMedicalAiMode = 1; break;
                case 2: lord.CurLordToil = new LordToil_PanicFlee(); break;
                case 3: lord.LordJob = new LordJob(); break;
                case 4: actor.CurJob.playerForced = true; break;
                case 5: actor.CurJob.def = new() { defName = "EnterShuttle" }; break;
                case 6: actor.EmergencyUrgency = 100; break;
                case 7: lord.questTags = ["quest-owner"]; break;
            }
            Assert.AreEqual(NpcDeparture.Kind.Unhandled, NpcDeparture.Decide(actor).Kind);
            MoreInjuriesMod.Settings.NpcMedicalAiMode = 2;
        }
    }

    [TestMethod]
    public void RealStartJobGuardReplacesDepartureAndProtectsCommittedMedicalWork()
    {
        Hooks(); (Pawn actor, Pawn patient, _) = Pair(); Dose(actor);
        actor.jobs.StartJob(Exit(new(0, 0, 20)), resumeCurJobAfterwards: true);
        Assert.AreSame(KnownJobDefOf.MI_NpcPainAssist, actor.CurJob.def);
        Assert.IsFalse(actor.jobs.Resumed); Assert.HasCount(0, actor.jobs.jobQueue);
        actor.jobs.StartJob(Exit(new(0, 0, 20)));
        Assert.AreSame(KnownJobDefOf.MI_NpcPainAssist, actor.CurJob.def);
        Assert.AreSame(patient, actor.CurJob.targetA.Thing);
    }

    [TestMethod]
    public void RealTheftGuardStopsAnAlreadyActiveNonCasualJobBeforePickupOrExit()
    {
        Hooks(); (Pawn actor, _, _) = Pair(false); actor.DoctorCapable = false;
        Thing loot = new() { PositionHeld = actor.Position, Map = actor.Map };
        actor.CurJob = new() { def = JobDefOf.Steal, targetA = new(loot), targetB = new(new IntVec3(0, 0, 20)) };
        new JobDriver_TakeAndExitMap { pawn = actor }.Run();
        Assert.IsFalse(actor.Exited); Assert.IsTrue(loot.Spawned);
        Assert.AreSame(KnownJobDefOf.MI_NpcEvacuate, actor.CurJob.def);
    }

    [TestMethod]
    public void SavedExitNearEdgeAndSavedTheftWithLootRetainToilIndicesAndYield()
    {
        Hooks();
        foreach (bool theft in new[] { false, true })
        {
            (Pawn actor, _, _) = Pair(false); actor.DoctorCapable = false;
            Thing? loot = theft ? Loot(actor) : null;
            actor.CurJob = theft ? new() { def = JobDefOf.Steal, targetA = new(loot!), targetB = new(new IntVec3(0, 0, 20)) }
                : Exit(new(0, 0, 20));
            JobDriver driver = theft ? new JobDriver_TakeAndExitMap { pawn = actor, ResumeIndex = 2 }
                : new JobDriver_Goto { pawn = actor, ResumeIndex = 1 };
            Assert.HasCount(theft ? 3 : 2, driver.BuildToils());
            driver.Run(); Assert.IsFalse(actor.Exited);
            Assert.AreSame(KnownJobDefOf.MI_NpcEvacuate, actor.CurJob.def);
            if (loot is not null) { Assert.IsTrue(loot.Spawned); Assert.IsFalse(loot.Destroyed); }
        }
    }

    [TestMethod]
    public void NativeSafetyAndExplosivesAreNotInterceptedAndStaleCallbacksCannotExit()
    {
        Hooks(); (Pawn actor, _, _) = Pair(); Dose(actor);
        Job original = actor.CurJob; int id = original.loadID;
        List<Toil> oldToils = new JobDriver_Goto { pawn = actor }.BuildToils();
        actor.mindState.knownExploder = new() { Map = actor.Map, PositionHeld = actor.Position };
        Assert.AreEqual(NpcDeparture.Kind.Unhandled, NpcDeparture.Decide(actor).Kind);
        actor.CurJob = new() { def = new() { defName = "native-safety" } };
        oldToils.Last().initAction(); Assert.IsFalse(actor.Exited);
        Assert.IsFalse(NpcDeparture.YieldNative(actor, original, id, original.def, false));
    }

    [TestMethod]
    public void ActiveMedicalWorkOwnsDepartureOnlyUntilNativeSafetyNeedsControl()
    {
        (Pawn actor, Pawn patient, _) = Pair();
        actor.CurJob = new() { def = KnownJobDefOf.MI_NpcTend, targetA = new(patient) };
        Assert.IsTrue(NpcDeparture.Owns(actor));
        actor.NativeSafety = true;
        Assert.IsFalse(NpcDeparture.Owns(actor));
        actor.NativeSafety = false; actor.EmergencyUrgency = 100;
        Assert.IsFalse(NpcDeparture.Owns(actor));
    }

    [TestMethod]
    public void WaitingForAnActiveDoctorRequiresTheSurvivorsOwnPositionToBeSafe()
    {
        foreach (bool safe in new[] { true, false })
        {
            (Pawn actor, Pawn patient, _) = Pair();
            Pawn doctor = new() { Map = actor.Map };
            Job tending = new() { def = KnownJobDefOf.MI_NpcTend, targetA = new(patient) };
            doctor.CurJob = tending; actor.Map.mapPawns.AllPawnsSpawned.Add(doctor);
            actor.Map.reservationManager.Reserve(doctor, tending, patient);
            if (!safe) { actor.Map.Risks[actor.Position] = new(1, 0, 0, 0); }
            Assert.AreEqual(safe ? NpcDeparture.Kind.Wait : NpcDeparture.Kind.Released, NpcDeparture.Decide(actor).Kind);
            Assert.IsTrue(actor.Map.reservationManager.Claims.Any(claim => claim.Pawn == doctor && claim.Target == patient));
        }
    }

    [TestMethod]
    public void InvalidatedPendingWorkReleasesClaimsOnItsOriginalMap()
    {
        (Pawn actor, Pawn patient, _) = Pair(); Dose(actor);
        Job native = actor.CurJob;
        Job pending = NpcDeparture.Decide(actor).Job!; Map originalMap = actor.Map;
        actor.Map = new(); MoreInjuriesMod.Settings.NpcMedicalAiMode = 1;
        Assert.IsTrue(NpcDeparture.YieldNative(actor, native, native.loadID, native.def, false));
        Assert.IsFalse(originalMap.reservationManager.Claims.Any(claim => claim.Job == pending));
        Assert.IsFalse(NpcDeparture.IsWork(pending));
    }

    [TestMethod]
    public void AdministrationRechecksModeAndPatientSurvivalWithoutConsumingTheDose()
    {
        foreach (bool dead in new[] { false, true })
        {
            (Pawn actor, Pawn patient, _) = Pair(); Thing dose = Dose(actor);
            Job job = NpcDeparture.Decide(actor).Job!; actor.CurJob = job; actor.Position = patient.Position;
            if (dead) { patient.Dead = true; }
            else { MoreInjuriesMod.Settings.NpcMedicalAiMode = 0; }
            Assert.IsFalse(NpcPainCare.FinalCheck(actor, patient, dose));
            Assert.IsFalse(dose.Destroyed); Assert.IsTrue(actor.inventory.innerContainer.Contains(dose));
            MoreInjuriesMod.Settings.NpcMedicalAiMode = 2;
        }
    }

    [TestMethod]
    public void StaleDepartureCompletionCannotReleaseAReusedJobsNewClaims()
    {
        (Pawn actor, Pawn patient, _) = Pair(false); actor.DoctorCapable = false;
        Job job = NpcDeparture.Decide(actor).Job!; int oldId = job.loadID; JobDef oldAction = job.def;
        actor.Map.reservationManager.ReleaseClaimedBy(actor, job);
        job.loadID++; job.def = JobDefOf.Goto; job.targetA = new(new Thing());
        actor.Map.reservationManager.Reserve(actor, job, job.targetA.Thing);
        NpcDeparture.Finish(job, oldId, oldAction);
        Assert.IsTrue(actor.Map.reservationManager.Claims.Any(claim => claim.Job == job));
        Assert.IsFalse(NpcDeparture.IsWork(job));
    }

    [TestMethod]
    public void RepeatedFreshNativeChecksDoNotExpandTheFourPatientIntervalBudget()
    {
        (Pawn actor, Pawn first, Lord lord) = Pair(false); actor.DoctorCapable = false;
        first.Position = new(21, 0, 20); actor.Map.Risks[first.Position] = new(1, 0, 0, 0);
        for (int index = 1; index <= 4; index++)
        {
            Pawn patient = new() { Downed = true, Map = actor.Map, Position = new(21 + index, 0, 20) };
            actor.Map.mapPawns.AllPawnsSpawned.Add(patient); lord.ownedPawns.Add(patient);
            if (index < 4) { actor.Map.Risks[patient.Position] = new(1, 0, 0, 0); }
        }
        for (int repeat = 0; repeat < 5; repeat++)
        { Assert.AreEqual(NpcDeparture.Kind.Wait, NpcDeparture.Decide(actor, force: true).Kind); }
        Find.TickManager.TicksGame += 150;
        Assert.AreEqual(NpcDeparture.Kind.Work, NpcDeparture.Decide(actor, force: true).Kind);
    }

    [TestMethod]
    public void LowerModesAndQuestDeparturesExecuteTheOriginalNativeExit()
    {
        Hooks();
        foreach (int scenario in Enumerable.Range(0, 3))
        {
            (Pawn actor, _, Lord lord) = Pair(); Dose(actor);
            MoreInjuriesMod.Settings.NpcMedicalAiMode = scenario;
            if (scenario == 2) { lord.questTags = ["quest-owner"]; }
            new JobDriver_Goto { pawn = actor }.Run();
            Assert.IsTrue(actor.Exited); Assert.AreSame(JobDefOf.Goto, actor.CurJob.def);
        }
    }

    [TestMethod]
    public void ClinicalProgressAfterAnExpiredIdleDeadlineCannotRestartDepartureOwnership()
    {
        (Pawn actor, Pawn patient, _) = Pair(); Dose(actor);
        Job work = NpcDeparture.Decide(actor).Job!;
        Find.TickManager.TicksGame += 1251;
        NpcDeparture.Finish(work, work.loadID, work.def, progress: true);
        Assert.AreEqual(NpcDeparture.Kind.Released, NpcDeparture.Decide(actor, force: true).Kind);
        Assert.IsFalse(actor.Map.reservationManager.Claims.Any(claim => claim.Target == patient));
    }

    [TestMethod]
    public void ExtractionHandoffSharesDepartureDeadlineAndDoesNotLoseItsNewClaims()
    {
        (Pawn actor, Pawn patient, _) = Pair(false); actor.DoctorCapable = false;
        Job transport = NpcDeparture.Decide(actor).Job!;
        Job treatment = new() { def = KnownJobDefOf.MI_NpcTend, targetA = new(patient) };
        actor.Map.reservationManager.Reserve(actor, treatment, patient);
        Assert.IsTrue(NpcDeparture.Handoff(actor, transport, treatment));
        NpcDeparture.Finish(transport, transport.loadID, transport.def);
        Assert.IsTrue(NpcDeparture.IsWork(treatment));
        Assert.IsTrue(actor.Map.reservationManager.Claims.Any(claim => claim.Job == treatment));
        Find.TickManager.TicksGame += 1250;
        Assert.IsFalse(NpcDeparture.Continue(actor, treatment));
    }

    [TestMethod]
    public void FailedRecomputedPreflightPreservesPlacedLootMedicineAndUnrelatedClaims()
    {
        (Pawn actor, Pawn patient, _) = Pair(); Thing medicine = Dose(actor), loot = Loot(actor);
        actor.carryTracker.MaxPickup = 0;
        Job unrelated = new(); actor.Map.reservationManager.Reserve(actor, unrelated, new Thing());
        NpcMedicalAi.OnDepartureCare = (_, target, _) => new()
        {
            def = KnownJobDefOf.MI_NpcTend, targetA = new(target),
            ReservationAttempt = (_, _) => false
        };
        Assert.AreEqual(NpcDeparture.Kind.Released, NpcDeparture.Decide(actor).Kind);
        Assert.IsTrue(loot.Spawned); Assert.IsFalse(loot.Destroyed);
        Assert.IsTrue(actor.inventory.innerContainer.Contains(medicine)); Assert.IsFalse(medicine.Destroyed);
        Assert.IsFalse(actor.Map.reservationManager.Claims.Any(claim => claim.Target == patient));
        Assert.IsTrue(actor.Map.reservationManager.Claims.Any(claim => claim.Job == unrelated));
    }

    [TestMethod]
    public void NativePreActionsAndIntervalCallbacksCannotExitAfterRescueTakesControl()
    {
        foreach (int callback in Enumerable.Range(0, 5))
        {
            (Pawn actor, _, _) = Pair(false); actor.DoctorCapable = false;
            Toil toil = Toils_General.Do(() => { });
            Action exit = () => actor.ExitMap(false, Rot4.Invalid);
            switch (callback)
            {
                case 0: toil.preInitActions = [exit]; break;
                case 1: toil.preTickActions = [exit]; break;
                case 2: toil.preTickIntervalActions = [_ => exit()]; break;
                case 3: toil.tickAction = exit; break;
                case 4: toil.tickIntervalAction = _ => exit(); break;
            }
            Toil guarded = NpcDepartureNativeJobs.Guard(new JobDriver_Goto { pawn = actor }, [toil]).Single();
            switch (callback)
            {
                case 0: guarded.preInitActions![0](); break;
                case 1: guarded.preTickActions![0](); break;
                case 2: guarded.preTickIntervalActions![0](30); break;
                case 3: guarded.tickAction!(); break;
                case 4: guarded.tickIntervalAction!(30); break;
            }
            Assert.IsFalse(actor.Exited); Assert.AreSame(KnownJobDefOf.MI_NpcEvacuate, actor.CurJob.def);
        }
    }

    [TestMethod]
    public void NativeFinishExitDefersStartingRescueUntilCleanupReturns()
    {
        Hooks(); (Pawn actor, _, _) = Pair(false); actor.DoctorCapable = false;
        JobDriver_Goto driver = new() { pawn = actor }; Job original = actor.CurJob;
        Toil finish = driver.FinishExitToil(); bool cleanup = false;
        finish.AddFinishAction(() => cleanup = true);
        Toil guarded = NpcDepartureNativeJobs.Guard(driver, [finish]).Single();
        foreach (Action action in guarded.FinishActions) { action(); }
        Assert.IsFalse(actor.Exited); Assert.IsTrue(cleanup); Assert.AreSame(original, actor.CurJob);
        actor.jobs.StartJob(Exit(new(0, 0, 20)));
        Assert.AreSame(KnownJobDefOf.MI_NpcEvacuate, actor.CurJob.def);
    }

    [TestMethod]
    public void StableMultiplePatientRejectionsAndWaitProbesDoNotRepeatDiagnostics()
    {
        Prefs.DevMode = true;
        try
        {
            (Pawn actor, Pawn first, Lord lord) = Pair(false); actor.DoctorCapable = false;
            Pawn second = new() { Downed = true, RawPain = 0, Map = actor.Map, Position = new(42, 0, 20) };
            actor.Map.mapPawns.AllPawnsSpawned.Add(second); lord.ownedPawns.Add(second);
            DamageInfo damage = new(new() { isRanged = true });
            NpcMedicalSafety.NotifyDamage(first, damage, 1f); NpcMedicalSafety.NotifyDamage(second, damage, 1f);
            Assert.AreEqual(NpcDeparture.Kind.Wait, NpcDeparture.Decide(actor, force: true).Kind);
            int count = Log.Messages.Count;
            for (int repeat = 0; repeat < 3; repeat++)
            { Assert.AreEqual(NpcDeparture.Kind.Wait, NpcDeparture.Decide(actor, force: true).Kind); }
            Assert.AreEqual(count, Log.Messages.Count);
        }
        finally { Prefs.DevMode = false; }
    }
}
