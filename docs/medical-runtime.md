# Blood loss, medical drug information, and optional hydration

code-synced: 2026-10-18

## Current contracts

The [vital failure owner](vital-failure.md) defines the new delayed-death and systemic cerebral progression paths. Its module supersedes systemic cerebral chance rolls while enabled; the shock and acidosis balance below remains unchanged.

| Area | Status and contract |
|---|---|
| Secondary-condition scheduling | `implemented`. Preserve handler order, individual hash intervals, and random-value consumption. The wake interval is their greatest common divisor. A nullable cache distinguishes an uncomputed schedule from a genuinely empty one, including zero-initialized properties. |
| Blood-loss death | `implemented`. The ordinary direct-death handler checks `PreventDeathByBloodLoss` again at the lethal write boundary for trauma-eligible pawns. Ordinary animals retain their separate policy. Startup keeps a positive intrinsic lethal threshold above the severity cap if another XML patch has made the cap lethal. An already disabled lethal threshold stays disabled. |
| Shock progression | `implemented`. Reliable 250-tick onset above 45% blood loss, then bounded 150-tick changes toward the unchanged deficit-dependent target. Native active tend quality controls support. Adrenaline changes rates but does not reduce the target. |
| Shock-driven acidosis | `implemented`. The existing Acidosis condition receives a deterministic source from sustained severe shock, using its existing 250-tick controller and saved injury history. Production competes by maximum with injury-driven input; no additional progress counter is stored. |
| Transfusion units | `implemented`. Blood-loss reductions remain in severity units. Dilution call boundaries convert by 0.5 to normalized fluid volume. Constant-time bag-count formulas account for overflow and verify rounding against the outcome equation. Existing timed caches and forced-treatment bypasses remain. |
| Trauma eligibility | `implemented`. Existing tracked pawns plus bleeding fleshy Anomaly entities while Anomaly is active. No ordinary-animal expansion or whole injury-worker injection into entities. HAR XML respects default-true `isFlesh`, explicit false values, existing comp entries, and mixed lists with and without comps. |
| Forced blood harvesting | `implemented`. The existing direct call to `Apply` remains separate and can kill at full blood loss, independently of the ordinary-bleeding toggle. |
| Drug information cards | `implemented`. `MedicalDrugHediffDef.SpecialDisplayStats` retains base entries and adds the game's own per-stage display statistics, severity thresholds, addiction states, and configured severity recovery rates. Tolerance samples call the game's drug-effect comp on an unattached condition. A private preview subclass sets the protected numeric severity field directly, avoiding native stage-change notifications that require a pawn; no pawn health state or ingestion action is changed. |
| Medical drug administration | `implemented`. Epinephrine, morphine, ketamine, and chloroform administration delegates to the same `Thing.Ingested` pipeline as direct use. Vanilla owns addiction, need satisfaction, tolerance, chemical gene notifications, overdose, and drug-use notifications. The job supplies one live split unit and preserves its social outcomes. Chloroform retains doctor-skill dosing through its ingestion outcome. |
| Chemical gene icons | `implemented`. Six RGBA textures occupy the vanilla chemical-template paths for the two existing chemical defs. Injector foregrounds use large heart, half-shield, and full-shield backgrounds for dependency, resistance, and immunity. A Biotech-guarded initializer restricts path assignment to those chemicals and templates. Automatic gene generation and identifiers are unchanged. |
| Bad Hygiene hydration | `implemented`. The XML-visible hydration outcome class is available in normal Release builds. With the optional compile flag it uses the typed API; otherwise it resolves and caches `Drink(float)` on the loaded thirst type after checking the active mod. This preserves Bad Hygiene's clamping and gain timestamp without a required DLL reference. |

The player explanations belong to the [shock](wiki/injuries/hypovolemic-shock.md), [morphine](wiki/injuries/morphine-high.md), [ketamine](wiki/injuries/ketamine-buildup.md), and [medical-device](wiki/medical-devices.md) pages. Chloroform and adrenaline also expose their existing stages on information cards.

## Unified medical drug administration

`JobDriver_OutcomeDoerBase` supplies one live unit to the configured outcomes and destroys it afterward only if it was not already consumed. This also preserves single-unit consumption for blood and saline outcomes, including failure cleanup. Drug definitions use `JobOutcomeDoer_IngestDrug` after any social outcomes, with physiological effects defined only in `ingestible/outcomeDoers`.

`MedicalDrugAdministration` calls `Thing.Ingested(patient, 0f)` on that unit. It does not call the drug comp separately: vanilla runs `PrePostIngested` (addiction and chemical-need satisfaction), ingestion outcomes (high and tolerance), and `PostIngested` (overdose and notifications) exactly once. Vanilla also notifies chemical-dependency genes. The former job called only `CompDrug.PostIngested`, which could cause overdose without ever rolling addiction or satisfying withdrawal; its separate high outcomes omitted tolerance and its modifiers.

Chloroform's ingestion outcome retains the existing minimum/maximum doctor-skill curves. The synchronous administration scope identifies both doctor and dose, restores previous context on nested calls or exceptions, and exposes no context to unrelated doses. Direct use and vanilla operation bills retain the existing 0.35 buildup baseline. No saved fields, identifiers, gene balance, dependency pins, or Harmony patches changed. Existing saves need no migration; subsequent injections now carry the intended addiction and tolerance risks.

The eight source-linked administration cases cover single-item and stacked drugs, social-outcome ordering, one entry into vanilla ingestion, non-drug device consumption, failed-outcome cleanup, nested/exception context restoration, chloroform dose selection, and the four drug definitions. The boundary to vanilla ingestion is a stand-in; the tests do not execute the game's addiction RNG or replace in-game acceptance. The pinned reference API and production Release build validate the real call signatures.

## Inherited drug components and information-card failures

Medical drug definitions now inherit vanilla's non-drug components and declare exactly one concrete `CompProperties_Drug` each. `MedicalDrugBase` no longer contributes an unconfigured drug component. Verse XML inheritance appends `li` entries even when their `Class` attributes match; the former base component therefore preceded the configured component on all four drugs. `ThingDef.GetCompProperties<T>` returns the first match, so `DrugStatsUtility.GetAddictivenessAtTolerance` read zero addictiveness and no chemical despite morphine's later 0.99 entry. The full ingestion pipeline alone could not correct this definition error. Removing the redundant component restores the configured chemical and chance for both direct use and injections without clearing inherited Forbiddable or third-party components.

The blank, lagging drug information card has a separate deterministic failure path. An item's ingestion outcome enumerates the tolerance def's `SpecialDisplayStats`; its detached `HediffWithComps` sample previously used the native `Severity` setter. Moving from the small stage to the 0.5 stage dereferences `pawn.health`, but the preview has no pawn. Repeated card rendering can repeat that exception. `TolerancePreviewHediff.SetPreviewSeverity` now changes only the protected numeric backing field. The vanilla drug-effect comp still computes the displayed samples; no fake pawn, ingestion, broad exception catch, or runtime patch is needed.

Before these fixes, the added regression cases failed for all four inherited component lists and for the production information-card enumeration. Afterward, all six cases pass. The component tests resolve the mod's actual parent chain with the verified minimal Core drug-base fixture and list-append semantics, assert one drug comp plus the retained Forbiddable comp, and check the configured chemical/chance. The source-linked card test enumerates all display entries three times with a setter fixture that reproduces the native pawnless stage-change failure. These are targeted headless reproductions, not a running-game mod-stack test.

## Regression evidence and limits

The confirmed working comparison is commit `077997d`. Its secondary-condition loop had no aggregate wake gate. The uncommitted optimization used an `int.MinValue` field initializer to represent an uncomputed gate, while a default-zero value meant no handlers. A headless test that populates zero-initialized properties reproduces complete callback suppression in that implementation. Changing the cache to nullable fixes that failure mode while retaining the optimization. This test establishes the constructor-sensitive defect; it does not by itself establish which materialization path the running game's XML parser used.

The normal-initialization differential tests compare callback identity, ordering, and random draws with the previous loop over multiple pawn offsets, mixed 250/60-tick intervals, and repeated cached reads. Both normal and zero-initialized cases now match. Modifier caching and the other existing optimizations are retained.

The unsupported `causesNeed` nodes were removed from the two addiction defs. Their valid `chemicalNeed` links and existing need identifiers are retained. The missing Release hydration class caused both the blood outcome template and the saline ThingDef to fail parsing; the absent-template and absent-SalineBag errors were downstream failures, not missing identifiers to replace.

The direct blood-loss hypoxia gain remains zero. Drug stages, concrete dose/recovery values, gene balance, settings defaults, save keys, and dependency pins are unchanged; the redundant unconfigured base drug component is removed. Shock onset and progression deliberately changed after the user selected the continuous model following the repaired build's game test. Death Rattle's [capacity/death handling](https://github.com/kittycat2002/Death-Rattle-Continued/blob/master/1.6/Source/HarmonyPatches.cs) is separate from a hediff's intrinsic lethal severity; this fix enforces More Injuries' own blood-loss contract at its own lethal-write boundary. The later [transition bridge](death-rattle-transition.md) separately neutralizes Death Rattle during migration.

## Continuous shock model

The reported 1–2% to 60–90% jump was existing behavior in `077997d`: the next shock update copied blood-loss severity while shock was below 55%, then subsequent updates used autonomous growth. It was not another scheduler failure. Repeated random onset checks could also delay visible shock until very high blood loss.

`ShockPerfusionModel` replaces those rules. These are explicit game-balance parameters, not clinically validated cutoffs. Clinical references informing the direction are the [European trauma guideline](https://doi.org/10.1186/s13054-023-04327-7) and the [ATLS classification validation study](https://pubmed.ncbi.nlm.nih.gov/22835498/). The implementation approximates perfusion using the existing volume deficit; it does not simulate blood pressure, cardiac output, or a separate lactate variable. Random organ injury retains the existing hypoxia handlers. With [delayed vital failure](vital-failure.md) enabled, systemic cerebral injury accumulates through its unified controller. Hypoxia still drives acidosis and coagulopathy.

| Blood-loss gauge | Model's missing-volume mapping | Shock target before treatment |
|---|---|---|
| ≤45% | ≤22.5% | 0% |
| 60% | 30% | 50% |
| 80% | 40% | 85% |
| 100% | 50% | 100% |

Targets interpolate linearly between the points. The 45% onset boundary retains the existing safe routine-donation amount, 0.449. Onset returns a modifier of 1 above the boundary, rather than repeating probability rolls, while the existing feature, eligibility, deathrest, and oxygen-immunity guards remain effective. The new condition starts at 1% and approaches its target instead of snapping to it.

## Treatment quality and adrenaline

Quality is read from the shock condition's native `HediffComp_TendDuration`: zero when absent or expired, otherwise `clamp01(tendQuality)`. The four-hour duration remains. Wound treatment is unchanged. `HediffModifier_TendQuality` replaces only the three boolean-tended hypoxia modifiers on shock; its `atFullQuality` child retains the existing settings gauge. Other uses of the boolean modifier are unchanged.

With `q` as active quality, growth is `(0.015 + 0.045 * target) * (1 - 0.5q) / support` per 150 ticks. Recovery is `0.0075 * (1 + 0.5q) * support`. Both stop at the target. Hypoxia chance multiplies by `1 - (1 - configuredReductionFactor)q`: at the default factor 0.5, a 15% tend reduces chance by 7.5%, a 30% tend by 15%, and a 100% tend by 50%.

Support remains `1 + clamp(adrenaline / 2, 0, 0.5)`. It no longer divides the target. Adrenaline's dose, capacity stages, overdose mechanics, and -6/day decay are unchanged; one 0.75-severity dose lasts approximately three game hours. It can slow shock-driven acidosis input while active, but does not reset accumulated acidosis.

The competing `severityPerDayTended` remains zero. The existing quality curve, public `FixedNow`/`PastFixedPoint` members, and `fixedNow` save key remain for compatibility. Neither flag chooses treatment strength. Deathrest and the disabled shock setting still force shock recovery; oxygen-deficiency immunity removes shock immediately.

## Shock-driven acidosis

`HediffComp_Shock` collects an Acidosis reference in its existing scan and creates one condition at 0.001 severity when effective shock exceeds 0.5. Creation requires trauma eligibility, both shock and advanced-trauma features, a positive conversion factor, and absence of oxygen-deficiency immunity and active deathrest. The acidosis controller applies the same guards to ongoing production. Existing acidosis from other sources is retained.

Effective shock is `min(currentShockSeverity, TargetSeverity(currentBloodLoss))`, without an adrenaline target reduction. Consequently, actual volume restoration can stop systemic production before the visible shock condition has finished recovering. The acidosis scan gathers shock, blood loss, adrenaline, and deathrest alongside the existing linked-injury scan.

The XML `shockAcidosisPerDayCurve` interpolates `(0, 0), (0.5, 0), (0.75, 2), (0.9, 4), (1, 8)`. Multiply its result by `(1 - 0.5q) / support * HypoxiaAcidosisConversionFactor / 0.015`. A zero conversion setting disables this source as well as the existing injury conversion.

Each 250-tick update uses the greater of positive historical injury change and the shock production converted to that tick's day fraction. Production is not summed twice. While shock production is positive, subtract `recoveryPerDayWhileShocked`, 0.5/day. Otherwise preserve the previous injury-history behavior: positive injury input grows severity, and no positive input permits the existing `recoveryPerDay`, 8/day. Clamp the result at zero and remove a resolved condition.

The original one-hour history, `severityHistory`, nested entry keys, and `previousSeverity` remain unchanged. Initialization samples injury history without granting a free shock-production or clearance tick; a valid shock-driven condition is retained at its starting severity. Shock uses its localized definition label in the existing cause display, avoiding stage-label formatting on each update. Stages, linked coagulopathy severity, spontaneous bleeding, and other complication definitions are unchanged.

## Transfusion units and safe counts

`GetFluidVolumePerBag` retains its existing protected API and severity-unit meaning. Blood-loss job counts continue to use 0.35 for blood and 0.15 for saline. The normalized getter and dynamic dilution procedure use `FluidVolumeFromBloodLossReduction`, multiplying by 0.5 exactly once at the dilution boundary. Existing template parameter names and values are unchanged. The evaluator's fluid-volume arguments remain normalized; no public method signature changed.

The maximum-safe-saline formula now has two regions: replacement until normal volume is reached, then the existing linear overflow rule. Floor/ceiling counts receive constant-time checks against the actual dilution equation. Full blood treatment must also replace the missing volume, so rounding verification cannot discard bags merely because dilution is already zero.

At 80% blood loss with no dilution, a saline bag yields about 11.1% dilution. At 80% loss and 40% dilution, a blood bag leaves about 31.0%. Full automatic saline treatment in the first state permits five bags; a sixth would reach 45%, above the 44.9% safety threshold. Stabilization and full-treatment blood-loss goals are unchanged. Independent coagulopathy restrictions and the forced single-saline bypass remain intact.

No new red-cell recovery model was added. The existing hemodilution and natural blood-loss recovery systems remain the game's abstractions.

## Performance contract

No global pawn scanner, worker thread, additional history buffer, or per-tick object allocation was added. Existing 150/250-tick gates and transfusion timed caches remain. New condition references are gathered in the existing scans. Numerical helpers and rounded bag-count verification allocate zero managed bytes after warm-up; count verification has no loop over bags. Existing cause/history processing and one-time condition creation retain their normal allocations.

## Race and DLC boundaries

`TraumaEligibility` is shared by the BloodLoss handler modifiers and the direct-death write guard. It retains tracked pawns, or admits an Anomaly entity only when Anomaly is active, `IsAnomalyEntity` and `IsFlesh` are true, and the race bleed rate is positive. The eligibility modifiers are specific to blood-loss trauma; global tracked/untracked modifiers are unchanged. Standard fleshbeast bodies have a heart, brain, and bleedable tissue, allowing the existing complication targets to operate.

HAR's installed 1.6 `CompatibilityInfo` defaults `isFlesh` to true. The XML integration previously missed an existing compatibility block without an explicit field and mishandled mixed comp-list shapes. Separate per-node selectors now cover those cases and skip existing injury comps and abstract race definitions. Explicit false values remain excluded. Individual alien anatomies still require game acceptance. The later vital-failure expansion adds eligible owned nonhuman emergency patients to medical-device work scanning, respecting native patient and medical-care restrictions.

## Validation

The Release build passes. `MoreInjuries.LocalizationTests` contains source-linked headless scheduler, quality-scaled shock progression, persistent-shock acidosis, native treatment expiry/re-tending, resuscitation, feature-toggle, deathrest, immunity, eligibility, reliable-onset, direct-death policy, nested save/load, transfusion rounding/overflow, dynamic outcome unit conversion, production cache invalidation, and warmed zero-allocation tests, plus HAR XML fixtures, reference-assembly field checks, Release XML-type coverage for defs and patches, texture-format checks, and localization checks. All 115 focused tests pass, including the eight administration cases, six inherited-component/information-card cases, and the [vital failure coverage](vital-failure.md). The test runtime supplies minimal Verse/Unity stand-ins; it is not a substitute for game acceptance.

Drug stage, concrete dose/recovery comp, severity-cap, and chemical-need XML values were compared against `077997d` and are unchanged; the redundant base drug component is now removed. All six replacement PNGs have alpha channels, transparent corners, and visible foregrounds. The current 1.6 package was refreshed and checked for XML validity, copied-file hashes, required paths, and source/build artifact exclusion. The legacy 1.5 binary is not part of this current-version test snapshot.

Controlled production-linked scenarios start at 80% blood loss/shock, stopped bleeding, negligible acidosis, and one fresh four-hour tend. They include the inspected natural recovery of 0.00033333333 severity per 60 ticks (about 1.39 percentage points per game hour), exclude random injuries, and run across several pawn offsets. Approximate times to 40% acidosis are 5.3 hours at 15% tend quality, 5.9 at 30%, and 9 at 80%; 100% stays below that threshold through 12 hours. A blood bag or forced saline bag at hour two prevents that threshold and permits clearance. A 0.75 adrenaline dose at hour two delays the 15% case to about 6.1 hours. These are balance checks, not guaranteed game survival times.

Native treatment fields are represented by test stand-ins. The tests execute production shock/acidosis controllers, nested `ExposeData` methods, the actual dilution outcome bridge, cache implementation, and numerical helpers; game serialization and treatment job execution still require acceptance in RimWorld. The production APIs and compilation use the pinned references. Only the specific installed bleeding implementation was inspected to establish the natural-recovery behavior used by the scenarios.

The localization loader now accepts multiline English comments and repeated, identical stage-label translations. It still rejects conflicting text and duplicate fields outside stage lists. Missing English entries for the existing drug additions were supplied without renaming existing keys.

## Game acceptance checklist

- On an unprotected pawn, compare morphine and ketamine direct use with doctor injections and self-injection. Confirm one item is consumed, morphine uses its current 5% base addiction chance, tolerance builds, and a later injection refills the chemical need and ends withdrawal. Clearing the high and overdose alone must not prevent addiction. Check dependency, resistance, and immunity genes separately.
- Compare epinephrine high after direct use and injection. Check chloroform direct-use baseline and doctor-skill dosing, sedation social consequences, and single-bag blood/saline consumption.

- Restart with the refreshed package and Bad Hygiene active: no unknown `causesNeed`, missing hydration class, missing blood-template, or missing saline-def errors.
- With shock and blood-loss death prevention enabled, use ordinary bleeding on a human, a fleshy HAR pawn, and a fleshbeast. Shock must begin at the first eligible check above 45%, rise gradually, and generate complications when severe. Reaching the cap must not itself cause direct blood-loss death. For an existing Death Rattle save, follow the validated [transition workflow](death-rattle-transition.md); retain the original backup.
- Confirm shock approaches a stable target at constant blood loss, recovers gradually after transfusion, and does not recover completely from tending alone while the deficit remains severe. Check existing Deathless and Breathless protections and deathrest separately.
- Compare fresh 15-30% and 80-100% shock tends, including expiry and replacement. Severe shock should build acidosis even before a new hypoxic injury; low-quality tending should offer modest protection. Random injuries can shorten the controlled balance window.
- Give one adrenaline dose: worsening should slow while the blood-loss target stays unchanged. Give blood or forced saline: acid production should drop as volume improves, with the visible shock condition recovering gradually. Check automatic saline still refuses unsafe coagulopathy states.
- At 80% blood loss without dilution, check one saline bag produces approximately 11.1% dilution and that automatic full treatment stops before a sixth bag. Verify subsequent job counts refresh after each treatment.
- Check an ordinary animal, mechanical entity, and nonbleeding entity remain excluded. Check the disabled prevention setting and forced harvesting separately; do not conflate their intentional death policies.
- Open the morphine and ketamine item information cards and their linked high, tolerance, and addiction cards repeatedly, including withdrawal and overdose states. Confirm the cards populate without blank rendering, repeated errors, or severe lag. Verify numerical text and all six gene icons. Start without Biotech and without Bad Hygiene to confirm optional behavior.
- Save and reload an existing affected pawn. Existing shock, active treatment quality, and acidosis history adopt the new balance at the next eligible updates. No new saved fields or migration are required; poor prior tends can now provide less protection and sustained shock can worsen existing saves.

Cerebral injury now uses anatomy-scaled physical severity with clinical-unit curves and linked acidosis input. Existing acidosis history stays in clinical units, preserving the audited balance scenarios. See [neurological runtime](neurological-runtime.md) for scaling and coma; shock/acidosis rates and transfusion doses are unchanged.

## Medical drug risk rebalance and death estimate

Status: `implemented`. code-synced: 2026-10-18.

After in-game confirmation that native addiction handling works, the configured base addiction chances are now morphine 0.05 and ketamine 0.02 per dose (previously 0.99 and 0.05). Five independent rolls at those base chances give 22.6% and 9.6% probability of at least one success. Actual native tolerance/gene modifiers can change risk. Existing tolerance gain/recovery, high doses, chemical need satisfaction, withdrawal, overdose and existing-addiction severity offsets are unchanged. Epinephrine and chloroform gain no addiction chemical. Definition/inheritance tests verify exactly one configured drug comp and the selected chances; native ingestion remains the sole addiction pipeline.

The [vital-failure owner](vital-failure.md#faster-cerebral-progression-and-prognosis) owns the faster acute brain progression and optional health-tab forecast. Shock/acidosis balance scenarios above remain valid independently of cerebral lethality; those tests measure acidosis progression rather than guarantee pawn survival. The final suite contains 115 passing tests, including the existing hemorrhage/localization checks. No dependency pins, package identity, existing save keys or identifiers changed.
