# Blood loss, medical drug information, and optional hydration

code-synced: 2026-10-17

## Current contracts

| Area | Status and contract |
|---|---|
| Secondary-condition scheduling | `implemented`. Preserve handler order, individual hash intervals, and random-value consumption. The wake interval is their greatest common divisor. A nullable cache distinguishes an uncomputed schedule from a genuinely empty one, including zero-initialized properties. |
| Blood-loss death | `implemented`. The ordinary direct-death handler checks `PreventDeathByBloodLoss` again at the lethal write boundary for trauma-eligible pawns. Ordinary animals retain their separate policy. Startup keeps a positive intrinsic lethal threshold above the severity cap if another XML patch has made the cap lethal. An already disabled lethal threshold stays disabled. |
| Shock progression | `implemented`. Reliable 250-tick onset above 45% blood loss, then bounded 150-tick changes toward a deficit-dependent target. No blood-loss copy or autonomous escalation above 55% shock. Tending and adrenaline support circulation without permanently erasing the volume deficit. |
| Trauma eligibility | `implemented`. Existing tracked pawns plus bleeding fleshy Anomaly entities while Anomaly is active. No ordinary-animal expansion or whole injury-worker injection into entities. HAR XML respects default-true `isFlesh`, explicit false values, existing comp entries, and mixed lists with and without comps. |
| Forced blood harvesting | `implemented`. The existing direct call to `Apply` remains separate and can kill at full blood loss, independently of the ordinary-bleeding toggle. |
| Drug information cards | `implemented`. `MedicalDrugHediffDef.SpecialDisplayStats` retains base entries and adds the game's own per-stage display statistics, severity thresholds, addiction states, and configured severity recovery rates. Tolerance samples call the game's drug-effect comp on an unattached condition; no pawn health state or ingestion action is changed. |
| Chemical gene icons | `implemented`. Six RGBA textures occupy the vanilla chemical-template paths for the two existing chemical defs. Injector foregrounds use large heart, half-shield, and full-shield backgrounds for dependency, resistance, and immunity. A Biotech-guarded initializer restricts path assignment to those chemicals and templates. Automatic gene generation and identifiers are unchanged. |
| Bad Hygiene hydration | `implemented`. The XML-visible hydration outcome class is available in normal Release builds. With the optional compile flag it uses the typed API; otherwise it resolves and caches `Drink(float)` on the loaded thirst type after checking the active mod. This preserves Bad Hygiene's clamping and gain timestamp without a required DLL reference. |

The player explanations belong to the [shock](wiki/injuries/hypovolemic-shock.md), [morphine](wiki/injuries/morphine-high.md), [ketamine](wiki/injuries/ketamine-buildup.md), and [medical-device](wiki/medical-devices.md) pages. Chloroform and adrenaline also expose their existing stages on information cards.

## Regression evidence and limits

The confirmed working comparison is commit `077997d`. Its secondary-condition loop had no aggregate wake gate. The uncommitted optimization used an `int.MinValue` field initializer to represent an uncomputed gate, while a default-zero value meant no handlers. A headless test that populates zero-initialized properties reproduces complete callback suppression in that implementation. Changing the cache to nullable fixes that failure mode while retaining the optimization. This test establishes the constructor-sensitive defect; it does not by itself establish which materialization path the running game's XML parser used.

The normal-initialization differential tests compare callback identity, ordering, and random draws with the previous loop over multiple pawn offsets, mixed 250/60-tick intervals, and repeated cached reads. Both normal and zero-initialized cases now match. Modifier caching and the other existing optimizations are retained.

The unsupported `causesNeed` nodes were removed from the two addiction defs. Their valid `chemicalNeed` links and existing need identifiers are retained. The missing Release hydration class caused both the blood outcome template and the saline ThingDef to fail parsing; the absent-template and absent-SalineBag errors were downstream failures, not missing identifiers to replace.

The direct blood-loss hypoxia gain remains zero. Drug stages, doses, recovery comps, gene balance, settings defaults, save keys, and dependency pins are unchanged. Shock onset and progression deliberately changed after the user selected the continuous model following the repaired build's game test. Death Rattle's [capacity/death handling](https://github.com/kittycat2002/Death-Rattle-Continued/blob/master/1.6/Source/HarmonyPatches.cs) is separate from a hediff's intrinsic lethal severity; this fix enforces More Injuries' own blood-loss contract without patching Death Rattle.

## Continuous shock model

The reported 1–2% to 60–90% jump was existing behavior in `077997d`: the next shock update copied blood-loss severity while shock was below 55%, then subsequent updates used autonomous growth. It was not another scheduler failure. Repeated random onset checks could also delay visible shock until very high blood loss.

`ShockPerfusionModel` replaces those rules. These are explicit game-balance parameters, not clinically validated cutoffs. Clinical references informing the direction are the [European trauma guideline](https://doi.org/10.1186/s13054-023-04327-7) and the [ATLS classification validation study](https://pubmed.ncbi.nlm.nih.gov/22835498/). The implementation approximates perfusion using the existing volume deficit; it does not simulate blood pressure, cardiac output, or a separate lactate variable. Tissue injury accumulates through the existing hypoxia handlers, and hypoxia still drives acidosis and coagulopathy.

| Blood-loss gauge | Model's missing-volume mapping | Shock target before treatment |
|---|---|---|
| ≤45% | ≤22.5% | 0% |
| 60% | 30% | 50% |
| 80% | 40% | 85% |
| 100% | 50% | 100% |

Targets interpolate linearly between the points. The 45% onset boundary retains the existing safe routine-donation amount, 0.449. Onset returns a modifier of 1 above the boundary, rather than repeating probability rolls, while the existing feature, eligibility, deathrest, and oxygen-immunity guards remain effective. The new condition starts at 1% and approaches its target instead of snapping to it.

Every 150 ticks, support is `1 + clamp(adrenaline / 2, 0, 0.5)`, and the target is divided by support. Below target, growth is `(0.015 + 0.045 × target) × treatmentFactor / support`; treatment factor is 1 untreated, 0.75 tended, or 0.5 after a successful stabilization tend while still tended. Above target, recovery is `0.0075 × support`, multiplied by 1.5 while tended. Each step stops at its target. With no treatment, a full-gauge target grows by at most six percentage points per update. Recovery does not erase existing tissue injuries.

The competing `severityPerDayTended` is zero so one controller owns shock severity. The existing quality curve and `fixedNow` save key remain. A historical successful tend no longer grants permanent recovery despite persistent or renewed hemorrhage: its support expires when the condition is no longer tended. Deathrest forces recovery, as does disabling shock; oxygen-deficiency immunity still removes shock immediately. `PastFixedPoint` remains available for callers but no longer selects an autonomous-growth branch.

## Race and DLC boundaries

`TraumaEligibility` is shared by the BloodLoss handler modifiers and the direct-death write guard. It retains tracked pawns, or admits an Anomaly entity only when Anomaly is active, `IsAnomalyEntity` and `IsFlesh` are true, and the race bleed rate is positive. The eligibility modifiers are specific to blood-loss trauma; global tracked/untracked modifiers are unchanged. Standard fleshbeast bodies have a heart, brain, and bleedable tissue, allowing the existing complication targets to operate.

HAR's installed 1.6 `CompatibilityInfo` defaults `isFlesh` to true. The XML integration previously missed an existing compatibility block without an explicit field and mishandled mixed comp-list shapes. Separate per-node selectors now cover those cases and skip existing injury comps and abstract race definitions. Explicit false values remain excluded. Individual alien anatomies still require game acceptance. Anomaly support here covers physiology; automatic medical-device work scanning remains humanlike-only.

## Validation

The Release build passes. `MoreInjuries.LocalizationTests` contains source-linked headless scheduler, continuous shock progression, stabilization, resuscitation, feature-toggle, deathrest, immunity, eligibility, reliable-onset, and direct-death policy tests, plus HAR XML fixtures, reference-assembly field checks, Release XML-type coverage for defs and patches, texture-format checks, and localization checks. All 21 tests pass. The test runtime supplies minimal Verse/Unity stand-ins; it is not a substitute for game acceptance.

Drug stage, comp, severity-cap, and chemical-need XML values were compared against `077997d` and are unchanged. All six replacement PNGs have alpha channels, transparent corners, and visible foregrounds. The current 1.6 package was refreshed and checked for XML validity, copied-file hashes, required paths, and source/build artifact exclusion. The legacy 1.5 binary is not part of this current-version test snapshot.

The localization loader now accepts multiline English comments and repeated, identical stage-label translations. It still rejects conflicting text and duplicate fields outside stage lists. Missing English entries for the existing drug additions were supplied without renaming existing keys.

## Game acceptance checklist

- Restart with the refreshed package and Bad Hygiene active: no unknown `causesNeed`, missing hydration class, missing blood-template, or missing saline-def errors.
- With shock and blood-loss death prevention enabled, use ordinary bleeding on a human, a fleshy HAR pawn, and a fleshbeast. Shock must begin at the first eligible check above 45%, rise gradually, and generate complications when severe. Reaching the cap must not itself cause direct blood-loss death. Repeat with Death Rattle active.
- Confirm shock approaches a stable target at constant blood loss, recovers gradually after transfusion, and does not recover completely from tending alone while the deficit remains severe. Check existing Deathless and Breathless protections and deathrest separately.
- Check an ordinary animal, mechanical entity, and nonbleeding entity remain excluded. Check the disabled prevention setting and forced harvesting separately; do not conflate their intentional death policies.
- Open the linked high, tolerance, and addiction cards, including withdrawal and overdose states. Verify numerical text and all six gene icons. Start without Biotech and without Bad Hygiene to confirm optional behavior.
- Save and reload an existing affected pawn. No identifier or persistence migration is required. Existing shock adopts the new progression rule at its next update, preserving its current severity rather than snapping to the new target.
