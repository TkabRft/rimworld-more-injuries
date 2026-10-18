# Delayed vital failure and systemic hypoxia

code-synced: 2026-10-18

## Current contract

Status: `implemented` for the current RimWorld 1.6 build. `EnableDelayedVitalFailure` defaults to true. The module covers appropriate fleshy pawns, including ordinary animals, HAR races and fleshy Anomaly entities, all factions, respecting the shambler setting. Mechanical pawns remain excluded. Animal trauma, shock/acidosis and direct blood-loss policy remain separate. See the [player hypoxia guide](wiki/injuries/hypoxia.md) and [future expansion work](medical-expansion-todo.md).

This is an independent implementation using More Injuries conditions and treatments. The optional [Death Rattle transition](death-rattle-transition.md) establishes MI priority while both mods are active, converts supported saved state and validates the saved copy before removal. Unknown layouts or references block a successful removal report. [Neurological runtime](neurological-runtime.md) owns clinical injury scaling, outcome profiles and artificial coma.

## Death policy and anatomy

`VitalFailure_Initializer` adds a lightweight `VitalFailureComp` once to eligible pawn defs after XML processing. It does not inject the full injury-worker pipeline into animals or Anomaly entities. Anatomy is cached by BodyDef using consciousness-source, pumping-source, liver, and kidney tags. All members of a present organ group must be nonfunctional before that group's failure is created. A group absent from the anatomy is not considered a missing organ. Added/replacement parts are evaluated through the native part-efficiency API.

The required-capacity guard suppresses only the supported first failed capacity and checks for other unsupported lethal capacities before returning. Structural destruction of all consciousness sources retains native death. Unrelated hediff lethality, lethal-damage thresholds, execution, forced blood harvesting, and the existing direct blood-loss policy remain unchanged.

Circulatory arrest reuses `CardiacArrest`. The new condition identifiers are `MI_RespiratoryFailure`, `MI_LiverFailure`, `MI_RenalFailure`, `MI_FiltrationFailure`, `MI_MetabolicFailure`, and `MI_Coma`. Generic filtration failure is used only when filtration is zero without complete liver/renal failure. Coma requires zero consciousness with a structurally surviving consciousness source; it does not itself drive oxygen deprivation.

All failure entries start at 0.001 severity. New failure defs are lethal at 1; cardiac lethality at 1 is guarded at runtime so disabling the module preserves legacy arrest behavior. Base daily rates are 8 for circulation/respiration, 1 for liver/generic filtration, 0.2 for renal failure/coma, and 0.34 for metabolism. Apply a shared fresh random factor in [0.8, 1.2] on each 250-tick update. The circulation/breathing failure timers alone average approximately three game hours, with mathematical bounds of 2.5-3.75 hours plus tick alignment before other protection or treatment.

Failure is removed when its underlying function returns. Mechanically caused arrest is distinguished from spontaneous arrest: restoring pumping resolves mechanical failure, whereas arrhythmia requires resuscitation. Terminal vanilla heart attack enters the arrest path instead of killing immediately once that arrest is registered. Oxygen immunity does not suppress an unrelated, unconverted lethal heart attack or liver disease.

## Cerebral progression

The controller reads actual XML curves from `MI_VitalFailureConfig`:

- Shock deprivation: (0,0), (0.5,0), (0.75,0.1), (0.9,0.4), (1,1), using existing effective shock. Multiply this source only by `(1 - 0.5q) / support`, with native active tend quality and existing adrenaline support.
- Breathing and pumping deprivation: `clamp01(1 - capacity / 0.5)`. Active arrest or terminal heart attack represents absent effective pumping. Coma does not substitute for either capacity.
- Dilution deprivation: (0,0), (0.45,0), (0.75,0.25), (1,1).
- Combine sources by maximum, then apply existing hypothermia protection: (0,1), (0.2,0.8), (0.6,0.45), (1,0.2).

Add `128 * deprivation * randomFactor * (250 / 60000) * anatomyScale * comaMultiplier` to the existing `BrainDamage_Hypoxia`, attached to a surviving consciousness source. Clinical severity 8 is terminal while the module owns progression and oxygen protection/deathrest does not apply. No oxygen-debt counter, new brain condition, or history buffer is stored.

Systemic cerebral rolls from shock, arrest, and hemodilution are skipped only when this controller owns progression. Random organ-injury handlers and focal sources such as stroke remain. Lasting neurological outcomes retain existing identifiers, chance handlers, and settings. Disabling lasting neurological damage does not disable acute cerebral deterioration. Native healing of organ/cerebral hypoxia is blocked during systemic deprivation and resumes after the source stops; tending cannot erase an unresolved oxygen deficit.

The [shock/acidosis implementation](medical-runtime.md) is retained, including maximum-based acidosis input. Direct blood-loss hypoxia remains zero. The existing OrganHypoxiaChance setting still governs random organ injury; it does not zero the module's cerebral progression.

## Treatments and guards

The pumping worker reports zero while arrest is active. Underlying pumping is read through a recursion-safe, read-only query that bypasses that override. Treatment-selection predicates do not mutate the health collection.

Production CPR and defibrillator methods retain skill, drug modifiers, research, and medical-care restrictions. Successful restart of a functioning pump clears arrest and an initiating terminal heart attack together, without erasing brain injury. A defibrillator cannot treat a nonfunctional pump; CPR cannot permanently restore one. Restoring a mechanically failed pump removes its failure. Artificial hearts retain the old spontaneous-arrest protection, while controller-created circulatory failure can represent a nonfunctional artificial pump. Existing airway treatments and organ-replacement operations remain available; new ventilation/dialysis equipment is deferred.

Deathless oxygen immunity and active deathrest suppress new systemic oxygen injury and oxygen-failure conditions. Liver, renal, filtration, and metabolic disease are not made oxygen-immune. Native gene/deathrest handling still governs their eventual outcome; the module does not call Pawn.Kill.

## Hooks, scheduling, and persistence

Narrow Harmony targets are `Pawn_HealthTracker.CheckForStateChange`, `Pawn_HealthTracker.HealthTickInterval`, `Pawn_HealthTracker.ShouldBeDeadFromRequiredCapacity`, `PawnCapacitiesHandler.Notify_CapacityLevelsDirty`, `PawnCapacityWorker_BloodPumping.CalculateCapacityLevel`, `Hediff.CauseDeathNow`, and `Hediff_Injury.Heal`. Capacity getters never create or remove conditions. Reentrant state checks can delay supported capacity death without recursively refreshing the controller.

The native health tick drives the 250-tick gate, covering map, caravan, and world-pawn health processing. Healthy snapshots are reused until invalidated; active conditions request a fresh scheduled snapshot. References are collected in one scan per refresh, with fixed per-pawn failure slots and per-body anatomy arrays. Existing 150/250-tick shock/acidosis scheduling and transfusion caches are unchanged. There is no global pawn scan, worker thread, or per-tick allocation. Existing native capacity/cause processing can allocate; the numeric helpers allocate zero managed bytes after warm-up.

Existing condition severities provide progression state. The new `lastVitalProgressTick` save key prevents replaying an eligible update after reload. The replacement arrest comp retains native severity-per-day serialization and adds `mechanicalFailure` to preserve arrest origin. Existing save keys and public signatures are unchanged. LoadingVars/ResolvingCrossRefs never create conditions. Turning the module off restores normal capacity-death rules and removes its newly owned failure entries at refresh; it is not a safe treatment operation.

## Validation

Release compilation succeeds against the pinned 1.6.4633 reference and Harmony 2.3.6. All 119 focused behavior/localization tests pass, including the earlier shock/acidosis and drug tests. Production-linked tests cover the controller, anatomy, death/healing guards, native-arrest adapter, actual CPR/defibrillator methods, source ownership, actual XML curves/rates, seeded rescue windows and pawn offsets, natural blood recovery, transfusion rescue, mechanical replacement, paired organs, multiple brains, immunity/deathrest/eligibility, recursive checks, scheduling, save/reload, and zero-allocation numeric helpers. Metadata tests verify hook methods and injected pawn fields against the pinned reference.

The runtime test harness supplies minimal Verse/Unity boundaries; it does not execute the game's serializer, capacity implementation, jobs, or Harmony patch chain. XML parsing, localization coverage, release XML-type coverage, and package load-layout/hash validation pass. The current local package contains 231 runtime files. No legacy 1.5 behavior is claimed as tested by this snapshot.

## Manual game acceptance

- Confirm no XML/type/patch errors with MI alone, then exercise the backed-up two-mod migration and MI-only reload described in the transition guide.
- Test absent pumping/breathing, terminal heart attack, and prolonged full shock. Confirm a usable rescue window, worsening cerebral hypoxia, and terminal deterioration without successful treatment.
- Compare poor/good shock tends, expiry, adrenaline, cold, transfusions, and renewed bleeding. Restored volume should stop its ongoing source before visible shock disappears.
- Run successful/failed CPR and early defibrillation; verify brain injury remains and a missing heart cannot be restarted. Restore an actual pump/lung or clear an airway.
- Test single/both kidneys, liver/metabolic/filtration failure, custom HAR anatomy, multiple brains, fleshbeasts, small/large ordinary animals, and excluded mechanical pawns. Preserve immediate structural brain death and forced-harvesting behavior.
- Test immunity, deathrest, shamblers, disabled module, save/reload, and caravan/world-pawn progression. Custom alien anatomy and third-party death patches require game acceptance.

## DLC and recovery contracts

Mutants with `breathesAir=false` do not acquire respiratory failure or a breathing deprivation source. Native bleeding suppression is preserved; `canBleed=false` does not grant circulatory immunity. Native `preventsDeath`, death refusal, scripted deaths, death-on-downed, execution and structural brain destruction remain effective. Repeated gene extraction (`XenogermReplicating >= 2`) retains its native lethal capacity route.

Odyssey environmental protection remains entirely native: MI neither recomputes `VacuumResistance` nor adds a parallel vacuum source. The prior `VacuumResistance_Total` blanket internal-oxygen immunity is removed from the shared helper and XML modifiers. Equipment and breathless genes do not repair pumping or missing lungs. Native vacuum exposure/decompression remains lethal independently of coma's protection.

`Pawn.Suspended` and death stop updates. Resurrection resumes from the current tick without replaying elapsed time. `Pawn_MutantTracker.Turn/Revert` and `Pawn_GeneTracker.Notify_GenesChanged` invalidate physiology; transformation additionally applies native chronic/permanent cleanup policy to registered neurological conditions. Healthy snapshots refresh after invalidation/settings changes; active failures and regeneration retain the 250-tick gate. Neurological regeneration is gathered in the same scan and remains available independently of the vital-failure setting.

Emergency work scanning and manual CPR/defibrillator/airway menus include eligible owned nonhuman patients without the full injury component. Native medical and mutant restrictions apply. Species still need a compatible native or modded organ-replacement recipe; this module does not invent transplantation operations for every anatomy.

## Faster cerebral progression and prognosis

Status: `implemented`. code-synced: 2026-10-18.

Systemic cerebral production is 128 clinical severity/day and the terminal threshold remains 8, producing a mean 1.5-game-hour complete-deprivation window without protection. Normalization, [lasting milestones](neurological-runtime.md#guaranteed-milestone-contract), maximum-based deprivation and all existing support factors remain. Focal injury rates, shock/acidosis equations, failure timers and transfusion doses are unchanged.

The controlled production/forecast targets are 1.5 hours at full untreated shock, 2.4 hours for fresh full loss with shock starting near zero, 2.6 hours with stopped bleeding and natural recovery, 1.8 hours with fresh 30% tending at full shock, and 3 hours with artificial coma. Tests allow one 250-tick alignment interval plus phase tolerance. These isolate cerebral progression; earlier complications/native deaths remain possible. The prior two-hour rescue example is no longer assured; the production test verifies a one-hour blood transfusion can prevent terminal cerebral injury.

`EnablePhysiologyDeathEstimate` is a default-true presentation setting. `Patch_PhysiologyDeathEstimate` remains a guarded postfix of the existing vanilla health-tab hediff listing. It paints the reserved footer with bleeding rate and an approximate death estimate. The tooltip contains only the projected cause and concise rescue advice. A false setting, a pre-game health preview, or `__runOriginal=false` returns before health reads/drawing. The open main button must be Inspect, its `MainTabWindow_Inspect.OpenTabType` must be `ITab_Pawn_Health`, and its singly selected thing must be this pawn. These checks precede controller access and snapshot/capacity reads. It does not rewrite `HealthUtility.TicksUntilDeathDueToBloodLoss`, health behavior or doctor priorities. A UI mod that replaces the method retains its own footer; no broader UI-mod compatibility claim is made.

`CapturePrognosis` reads a snapshot without calling the health-mutating `Refresh`. `PhysiologyPrognosis` advances numeric blood recovery/bleeding (60 ticks), shock/support (150 ticks) and cerebral production (250 ticks), using existing helpers and actual XML curves. It uses mean random factors and current anatomy-independent clinical injury. Tend duration expires; adrenaline decays using its configured comp rate. Current hypothermia protection, breathing, pumping and dilution are held constant. Existing fatal failure/coma timers are calculated directly and compete with the cerebral/direct-blood-loss paths.

The cerebral horizon is 60000 ticks (24 game hours), with a bounded event-stepping loop; longer existing failure timers remain displayable. No future random complications, treatments, organ restoration or other disease progression are forecast. Native death protection suppresses a finite death estimate, and oxygen-specific protections suppress only applicable routes. The existing ordinary-animal direct-blood-loss policy is retained.

Only visible Health-tab requests calculate forecasts. There is no background forecast tick, global scan or catch-up work for hidden pawns. Closing the tab, switching tabs or selecting another pawn stops requests for the previous pawn; actual physiology continues independently. Requested forecasts and their tooltip strings are cached for at most 250 ticks. Capacity/health invalidation and shock tending discard the cache without recalculating it; relevant settings are checked independently. Repeated frames reuse the result and count down elapsed ticks without rescanning capacities. Reopening refreshes expired or invalidated state. Snapshot capture and forecasting consume no RNG and add/remove no health conditions; warmed numeric forecast helpers allocate zero bytes.

The authored treatment catalogue is `Languages/English/Keyed/Lang_PrognosisHints.xml`; `PrognosisRescueHints` references these entries using only the cached snapshot. The same health scan records airway obstruction, collapsed-lung surgery requirements and arrest stage; a read-only underlying-pumping check distinguishes a restartable heart from a nonfunctional one. Cerebral advice combines the current oxygen-delivery problems, with blood transfusion listed once for blood loss and dilution together. Late arrest omits defibrillation. Coma, organ failures and artificial coma have separate advice; Wake-up respects native drug and medical-care restrictions. Terminal cerebral injury, native protection and absent fatal trends receive statuses without rescue promises. These are potential interventions, not a guarantee of treatment availability or success. Advice selection does not simulate a treatment or change forecast mathematics, save keys or public patch signatures.

The final Release build and all 119 focused behavior/localization tests pass. Coverage includes production timing, competing sources, support expiry, finite and long failure timers, native protection, UI bypass/toggle behavior, cache invalidation and allocation checks. Additional tests exercise 100 wounded pawns during normal health ticking with closed, other or unselected tabs; visible cache reuse; reopening after health changes; saved localized hint selection; combined-source deduplication; arrest-stage/heart-function restrictions; and protected, terminal and Wake-up-restricted cases. The 231-file runtime package passes XML, load-layout and source/copy SHA256 checks. Manual acceptance remains required for actual GUI placement, tooltip wrapping and live tab-switch performance, native death events, live saves and modded health-tab replacements.
