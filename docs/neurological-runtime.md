# Neurological framework

Status: `implemented`. code-synced: 2026-10-18.

Owns lasting neurological outcomes, clinical cerebral severity and induced coma. See [vital failure](vital-failure.md), [migration](death-rattle-transition.md), and the player guides for [brain damage](wiki/injuries/brain-damage.md) and [artificial coma](wiki/injuries/artificial-coma.md).

## Clinical units and persistence

`CerebralInjury` uses `max(0.001, affectedPart.GetMaxHealth(pawn) / 10)` as the physical injury scale. Production and terminal injury use the same scale. Stages, secondary-condition curves and linked acidosis input use clinical severity, physical severity divided by that scale. Human adult systemic production is 128/day with a terminal threshold of 8 clinical units. Native structural deaths and unrelated lethality remain effective.

The `cerebralAnatomyScale` comp key records the prior scale. Missing data identifies pre-expansion saves: PostLoadInit multiplies their existing clinical injury by the new scale once. Reload and later anatomy/health-scale changes preserve clinical burden. Existing acidosis samples remain in their original clinical units, so neither its history keys nor its values require a reset. Injury tending and permanence are retained on the same hediff instance.

No oxygen-debt counter or additional injury-history buffer exists. The existing injury remains `BrainDamage_Hypoxia` and the existing named disability identifiers remain unchanged.

## Configuration and outcomes

`Defs/Internal/NeurologicalProfiles.xml` owns `MI_NeurologicalConfig`, damage-name mappings and profile defs. Unknown damage names select the default profile; duplicate mappings resolve last-wins, matching the concussion configuration convention. Each outcome provides its existing maker fields: `hediffDef`, `weight`, `minSeverity`, `maxSeverity`, `allowMultiple`, and `allowDuplicate`. Zero weight excludes an outcome. Profiles are selected by stable defs, never translated cause labels.

Default weights are 1. Blunt/crush/explosive profiles favor diffuse damage (3), memory and executive dysfunction (2). Penetrating favors motor/aphasia (3) and agnosia (2). Energy favors diffuse/motor/memory (2). Systemic hypoxia favors diffuse (3), memory/motor (2). Stroke favors motor/aphasia (3). Overdose and prolonged-coma complications favor diffuse (3), memory (2).

Drug-origin metadata follows newly generated arrest/stroke into cerebral injury. Head trauma records its damage profile on concussion/stroke. The latest contributing systemic arrest or injury source determines subsequent ordinary disability selection; this is bounded source metadata, not an exposure history. Existing saves without source metadata use their condition's default profile.

The ordinary cerebral complication handler keeps its 2500-tick interval, existing MTTF curve, 0.01-0.25 severity range and settings/tending gates. It selects one eligible outcome. Personality shifts retain their separate 2000-tick rare channel and setting, with explicit humanlike/skill/mutant eligibility. Ordinary animals receive diffuse or motor outcomes, rather than language or personality changes.

Direct brain-hit probability rolls are replaced by the milestone contract below. The existing `directBrainChanceFactor` XML field and accessor remain readable for compatibility and are not reinterpreted as a new probability. A concussion crossing 0.7 retains one 0.05 roll per episode, excluding direct brain hits. `neurologicalSourceProfile` and `severeConcussionOutcomeRolled` retain their keys. Disabling `EnableNeuralDamage` stops new lasting outcomes, not acute cerebral progression or existing disabilities.

`MI_DiffuseBrainDamage` preserves broad efficiency impairment at severities 0/0.2/0.4/0.6/0.8: -0.05/-0.15/-0.30/-0.50/-0.75. Severity 1 retains impairment and caps consciousness at 0.1, avoiding the original Death Rattle final-stage efficiency discontinuity. It participates in existing mechanite treatment, eligible regeneration, native chronic-disease handling and the optional EPOE neuro-cure procedure.

## Artificial coma

`MI_InduceArtificialComa` requires five `MedicineIndustrial`/`MedicineUltratech`, 400 work and normal surgery restrictions. Both recipe filters exclude herbal medicine. Patient care must permit industrial medicine, and native `MutantUtility.CanUseDrug` must permit the supported Wake-up reversal. Existing coma or active Wake-up prevents re-induction; no procedure resets coma severity.

`MI_ArtificialComa` caps consciousness at 0.1 and multiplies positive MI systemic/focal cerebral production by 0.5, once. It does not alter deprivation, shock, acidosis, failure progression, existing injury or vanilla vacuum exposure. Its native severity comp adds 0.1/day. Stages become dangerous at 0.7 and life-threatening at 0.8; severity 1 is lethal under native death protection. At severity >=0.72 its 250-tick comp samples a 0.17-day MTB for a 0.05 lasting neurological outcome. Its own complication is not reduced by its cerebral protection.

`MI_AwakenArtificialComa` administers one ordinary Wake-up dose through the native ingestion procedure, preserving drug effects and restrictions. Active `WakeUpHigh` removes induced coma, including direct administration outside this procedure. Natural coma and existing brain injury are untouched.

## Recovery and performance

Anatomy uses consciousness-source tags. The controller gathers neurological references, injured parts and regeneration in its existing refresh scan, retaining the 250-tick gate. Recovery uses `regeneration / 400` severity/day after the associated injury/scar is gone: 100 regeneration clears full lasting severity in four days. It remains independently configurable and runs even when delayed vital failure is disabled. The old regeneration worker retains fracture/spinal recovery; brain recovery is not applied twice. Personality changes already made to skills are never undone.

Two indexed passes over each small compiled profile avoid interface-enumerator allocations. There are no per-tick lists, worker threads or global health scanners. Anatomy caches are per BodyDef; mutable physiology is refreshed through native invalidation and explicit gene/mutant/resurrection hooks. Existing medical work scanning additionally considers owned eligible nonhuman patients.

## Validation

The Release build against pinned 1.6.4633 succeeds. All 115 focused tests pass, covering actual XML profiles, scaling/reload/growth, source metadata, animal filtering, zero-weight exclusion, zero warmed helper allocations, coma production and medicine filters, regeneration, native protection/suspension, existing shock/acidosis balance, migration and localization. The harness executes production code with minimal native boundaries; live capacity, ingestion, surgery and Harmony-chain behavior remain game acceptance checks.

## Guaranteed milestone contract

Status: `implemented`. code-synced: 2026-10-18.

`NeurologicalConfigDef.injuryMilestones` owns ordered clinical thresholds and cumulative lasting severities: (0.5,0.05), (1,0.10), (2,0.25), (3,0.40), (4,0.60), (6,0.80). Config validation requires positive increasing values, with lasting severity capped at 1. Clinical injury is physical injury / affected part maximum health × 10; hypoxia uses its existing scale comp.

`VitalFailureComp.Neurology` aggregates `Hediff_Injury` on consciousness-source parts in the existing refresh scan. It tracks hypoxic and other traumatic injuries separately per part. The existing AddHediff hook records direct damage profiles; it makes no probability roll. The first eligible milestone selects one weighted ordinary outcome. The selection/profile stays fixed, later increments deepen that condition, and zero-weight/ineligible outcomes remain excluded. Native structural deaths remain unaffected.

Awards are differences between newly reached and already processed cumulative severity, added to the selected condition and capped at its existing maximum. Partial healing, tending, regeneration or advanced treatment cannot replay old awards. Removing all corresponding injuries ends the episode; a later episode can contribute again. Changes in part maximum health mark the resulting level as processed without awarding harm. The existing random cerebral-complication and personality channels remain independent.

The new controller keys are `neurologicalMilestoneVersion` (version 1) and `neurologicalMilestoneStates`. Entries save `part`, `hypoxic`, `outcome`, `profile`, `processedMilestone` and `anatomyScale`; no continuous exposure history is stored. Pawn PostLoadInit initializes missing-version saves against current injuries after references resolve, without retroactive awards. Existing disabilities are preserved. Disabled neurological settings, oxygen immunity/deathrest for the hypoxic channel, suspension and excluded shamblers suppress awards without generating a later backlog. Direct structural trauma does not inherit oxygen immunity.

The controller's 250-tick schedule covers milestones even when delayed vital failure is disabled. Lists allocate only when episodes are created, and numeric consumption is allocation-free once warmed. There is no new global scanner or per-tick history.

Production-linked tests exercise every XML threshold, skipped thresholds, aggregation/merged severity, separate sources/parts, source selection, episode reset, reload, legacy initialization, settings, resizing, treatment between awards, species filtering, zero weights, immunity/deathrest and warmed allocations. Native serialization/GUI/jobs and representative modded anatomy still require game acceptance.
