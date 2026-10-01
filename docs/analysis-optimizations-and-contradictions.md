# Verified analysis and fix plan

code-synced: 2026-09-26

This document replaces the original list of "confirmed contradictions". Observations were checked against current C# and XML. User decisions take priority over earlier recommendations.

## Done 2026-09-26

Plans 1–3 and separate fixes A, B, and C are implemented. Optimizations in the last section were not done. Items in the "do not change" table were not changed.

| Stage | Status |
|---|---|
| 1. Shared stroke toggle | `implemented`. Four sources check `EnableHemorrhagicStroke` at runtime. An existing stroke is not removed, surgeries are not disabled, and its own progression is not stopped. |
| 2. `VacuumResistance_Total` | `implemented` under the working Odyssey owner, matching the hypoxia and shock defs. Gene ownership was not checked against vanilla DLC XML: it is not in the workspace dependencies. |
| 3. Blood-loss description | `implemented`. The zero direct gain is kept. The tooltip and wiki no longer promise independent hypoxia buildup. |
| A. Save loading | `implemented`. A missing `jobParameters` node is an empty cache, not a reason to delete wounds or resurrect a pawn. Targeted bionic relocation is kept and does not require mass wound deletion. |
| B. Temporary tamponade | `implemented`. The multiplier is `Lerp(1, base, remaining / total)`, with a guard against zero total time. |
| C. Repeat blood draw | `implemented`. The periodic handler does not add a second bill for the same recipe. Hemogen mode and the recipe are read only when Biotech is active. The notification filter was not changed. |
| Optimizations | not done |

## User decisions and scope

1. `EnableHemorrhagicStroke` must be a shared toggle for the mechanic: every More Injuries path that creates or further increases hemorrhagic stroke must honor it. Limiting the check to head trauma, or leaving independent sources and only fixing the description, is no longer acceptable.
2. `VacuumResistance_Total` checks must agree between XML and C#, including choking, tourniquet, hypoxia, shock, and the immunity check used by cardiac arrest.
3. A zero hypoxia gain from blood loss is not permission to increase brain damage. The user's note on intended behavior excludes the earlier proposal to copy non-zero values from shock. Keep the existing chain through shock.
4. The other confirmed defects below are separate proposed stages, not changes already made. Disputed balance changes and optimizations are kept apart from them.
5. Do not change identifiers, save or settings keys, dependency coordinates, or public patch surfaces. Do not add Harmony patches. Do not replace custom injury classes with stock ones. Do not delete health states in order to turn a setting off.

## Clarification: which setting promised hypoxia

This is the existing setting `PreventDeathByBloodLoss` in `Source/MoreInjuries/MoreInjuries/MoreInjuriesSettings.cs`, default `false`.

Before the 2026-09-26 text fix, `Languages/English/Keyed/Lang_Settings.xml` said:

- `MI_Settings_Features_LethalTriad_PreventDeathByBloodLoss_Label`: `Prevent direct death by blood loss (default: {DEFAULT})`.
- `MI_Settings_Features_LethalTriad_PreventDeathByBloodLoss_Tooltip`: `If enabled, pawns will not die directly from blood loss, but will instead receive hypoxia damage to the brain until death sets in.`

The tooltip, not the setting name or the code, promised brain damage until death. That text did not prove the zero values were accidental: the description could have been stale.

Current tooltip:

`If enabled, pawns will not die directly from blood loss. This setting does not itself add brain hypoxia. Lethal complications still depend on the other mechanisms that are currently active, such as hypovolemic shock.`

The label is unchanged.

Current implementation:

| Path | Condition and result |
|---|---|
| Blood loss → shock | `EnableHypovolemicShock`; chance appears above 0.45, checked every 250 ticks, only for tracked pawns and subject to the other limits |
| Shock → brain hypoxia | Separate handler every 250 ticks; gain 0.1–0.75 with its own chances and modifiers |
| Near-total blood loss → brain hypoxia | `PreventDeathByBloodLoss=true`; checked every 60 ticks, curve from 0 at 0.999 to 1 at 1.0; both gain bounds are zero |
| Direct death of tracked pawns | `PreventDeathByBloodLoss=false`; a separate handler writes lethal severity, bypassing the limit |
| Direct death of untracked pawns | A different handler, not the inverse of the previous flag; it keeps a separate path for pawns without `MoreInjuryComp` |
| Forced blood draw | `JobDriver_HarvestBlood`, on reaching 100%, calls the death handler without checking this flag |

The direct hypoxia handler returns a zero gain and does not increase existing damage. That does not mean hypoxia is absent through shock, hemodilution, cardiac arrest, or stroke. The brain-hypoxia def has no constant independent growth that replaces the zero gain.

The direct handler also sets `applyDownstreamModifiers=false`. Copying values from shock would change more than the numbers: intervals, chances, and the set of protections differ. Do not merge these paths.

## Plan 1. Shared hemorrhagic-stroke toggle

### Confirmed entry points

The base path `HealthConditions/HeadInjury/HeadInjuryWorker.cs` already checks `EnableHemorrhagicStroke`. The original analysis missed adrenaline overdose.

All four sources must be covered:

| Source | Owner | Plan |
|---|---|---|
| Head trauma | `HealthConditions/HeadInjury/HeadInjuryWorker.cs` | Keep the check separate from concussion; check both creation and an increase of an existing stroke |
| Hydrostatic shock | `HealthConditions/HydrostaticShock/HydrostaticShockWorker.cs` | Require both `EnableHydrostaticShock` and `EnableHemorrhagicStroke` before applying a stroke |
| Coagulopathy | `Defs/HediffDefs/Hediffs_Coagulopathy.xml` | Add the existing XML flag modifier to the handler that creates the stroke |
| Adrenaline overdose | `Defs/HediffDefs/Hediffs_Adrenaline.xml` | Add the same modifier to the stroke handler, without disabling the other adrenaline effects |

### Implementation sequence

1. Repeat a targeted search for every `HemorrhagicStroke` reference in current C#, defs, and patches. Separate creation and increase, treatment, and declarations. Do not stop at one worker name.
2. Add the checks on every stroke-producing path with existing C# and XML means. Do not disable all of coagulopathy, adrenaline, or concussion for one outcome.
3. Check the setting at runtime, so changing it does not require recreating the worker or loading a new game.
4. Do not erase an existing stroke from the save. Keep treatment available. Record the behavior of an existing stroke when the setting is off: a ban on new sources must not silently expand into deleting damage, disabling surgeries, or rewriting the save. Stopping the old stroke's own progression and complications is a separate behavior change and must be defined explicitly before implementation.
5. Fix the existing setting's tooltip: it controls every More Injuries stroke source, including trauma, hydrostatic shock, coagulopathy, and overdose. Update existing translations without renaming keys.

### Check

- Flag off: none of the four sources creates a stroke or adds severity to an existing stroke.
- Flag on: each source keeps its previous conditions, amounts, and intervals.
- For the hydrostatic path, check both settings in all four combinations.
- Turning stroke off does not turn off concussion, coagulopathy bleeding, or the other adrenaline effects.
- Check toggling the setting in the current game, and saving and loading with an existing stroke.
- The guarantee covers More Injuries paths. Blocking an arbitrary third-party mod from adding the state through a new global Harmony patch is out of this plan.

### Documentation closeout

Update `docs/wiki/injuries/hemorrhagic-stroke.md` and the matching source descriptions in `docs/wiki/injuries/hydrostatic-shock.md`, `docs/wiki/injuries/coagulopathy.md`, and `docs/wiki/injuries/adrenaline-rush.md`. Keep existing links. In `docs/concussion.md`, record the technical contract of the shared toggle and all four entry points; set `code-synced` only on the developer doc. On the affected wiki pages, remove internal statuses and `code-synced`, and do not replace them with other developer metadata.

## Plan 2. Align VacuumResistance_Total

### Confirmed mismatch

- `Defs/HediffDefs/Hediffs_Choking.xml`: two Biotech entries, for `ChokingOnBlood` and `ChokingOnTourniquet`.
- `Defs/HediffDefs/Hediffs_Ischemia.xml`: one more Biotech entry, for `TourniquetApplied`. Fixing only two entries does not close the task.
- `Defs/HediffDefs/Hediffs_Hypoxia.xml` and `Hediffs_HypovolemicShock.xml`: references to the same gene are under Odyssey.
- `Extensions/PawnExtensions.cs::HasOxygenDeficiencyImmunity` returns immediately when Biotech is inactive, before checking `VacuumResistance_Total`. Shock and cardiac arrest use the helper. Replacing XML alone does not align C#.

### Implementation sequence

1. Record whether the gene def is available for the supported version. The working direction is Odyssey, as in the current hypoxia and shock defs; that is not yet a check against the original DLC XML. If the required vanilla XML is not in the target mod, do not search a Steam or game install, and do not treat gene ownership as verified from reference assemblies.
2. After the owner is confirmed, point every XML reference to that gene at one correct dependency. For the expected Odyssey owner, fix all three Biotech entries, keeping 0.05 on choking and tourniquet and 0 on the matching hypoxia blocks.
3. Split the C# conditions: `Deathless` stays Biotech-dependent, and the `VacuumResistance_Total` check must not depend by accident on an early return for another DLC. Keep safe behavior when `pawn.genes`, the def, or an active gene is missing.
4. Check shared XML gene modifiers and both callers of `HasOxygenDeficiencyImmunity`. Use APIs supported by the target references. Do not add a required DLC dependency.
5. Do not turn gene-availability alignment into a full rewrite of oxygen immunity. Keep the difference between a slower choking rate and a block on new hypoxic damage, and keep the intentional exceptions on the direct blood-loss path.
6. Do not also move the immunity check to a rarer tick: that changes reaction delay and is not required for alignment.

### Check

Check four configurations: neither DLC, Biotech only, Odyssey only, and both. In each, check that defs load without unresolved references and that pawns without a gene tracker behave correctly. Where the matching gene is available, check absent, inactive, and active gene state. Separately check `Deathless`, choking on blood, choking on a tourniquet, ischemia under a tourniquet, hypoxia, shock, and cardiac arrest.

### Documentation closeout

Update the in-game protection explanations in `docs/wiki/injuries/hypoxia.md`, `choking.md`, `ischemia.md`, `hypovolemic-shock.md`, and `cardiac-arrest.md` in the same folder, only where those pages are affected. Keep the technical DLC matrix and results in this developer doc, with a `code-synced` date. Change `COMPATIBILITY.md` only if the check changes a stated DLC support or conflict status.

## Plan 3. Align the blood-loss description with intended behavior

1. Keep the zero gain, intervals, `maxSeverity=1`, `lethalSeverity=1.001`, the toggle, and the separate tracked and untracked pawn paths unchanged in the ready fixes.
2. Do not treat the tooltip as a higher source of intent than the user's note. Record the mismatch: the description promised independent damage until death, and the current direct path does not increase it.
3. Check shock on and shock off separately from `PreventDeathByBloodLoss`. Do not attribute hypoxia observed from shock to the zero handler. Account for forced blood draw separately.
4. While the current zero path stays, fix the description first: remove the promise of guaranteed direct hypoxia buildup, and explain that lethal complications depend on the mechanisms that are active. If a non-zero direct path is explicitly chosen later, write a separate balance change with a target time to damage, protections, and checks. Do not copy the shock range.
5. Finish by updating the existing tooltip and its translations, `docs/wiki/injuries/hypoxia.md`, and, if needed, the matching diagram in `docs/wiki/pathophysiological-system.md`. The hypoxia page currently both describes the zero path and promises death through it; remove that contradiction, keep links, and do not hand-edit generated blocks. Check the technical path table in this document against the final decision.

## Separate proposed fixes from the previous audit

These stages do not replace the decisions on stroke, the gene, and hypoxia. A, B, and C were done on 2026-09-26. The text below is kept as the verification contract.

### A. Safe save loading — highest priority

Confirmed: a missing `jobParameters` node leads to `FailedLoading`. The load postfix deletes wounds and missing body parts, and, when the matching conditions are met, clears surgeries and immunity and changes a dead pawn's state.

Plan: separate a missing internal cache when the mod is added from an actual load error. Do not delete health states or resurrect pawns to fix a missing cache. Keep the targeted bionic fix, after checking that it works without mass wound deletion. Do not promise migration safety from the name of that fix alone.

Check: copies of an old save without mod data, a normal save with the mod, pawns with wounds, missing parts, bionics, immunity, and surgeries; dead pawns separately. Do not run the checks on the user's only save.

Documentation closeout: developer description of the cause and migration in this file. For the player instruction on adding the mod to an old save, create `docs/wiki/save-compatibility.md` during implementation, with limits confirmed by the check. Reflect the new owner page in `docs/FEATURES.md`.

### B. Temporary-tamponade decay formula

Confirmed: `total / remaining` uses integer division and the inverse ratio at the same time. Casting to float without swapping the ratio does not fix the effect.

Plan: use the normalized fraction of remaining time, with a guard against zero total time, without changing the rest of the bleeding formula. Check the start, middle, and end of the effect, and saving and loading the timer.

Documentation closeout: describe the decay in `docs/wiki/medical-devices.md`. Keep the technical formula and checked bounds in this document.

### C. Duplicate automatic blood-draw bills

Confirmed: the periodic handler has no check of its own for an already assigned surgery, although the UI path does. Having enough blood does not replace that check.

Plan: do not add a repeat surgery for the same recipe. Check Biotech before reading hemogen mode and the recipe. Do not rewrite the notification filter from an unverified assumption about the lifetime of the returned list.

Check: a long wait for a doctor, repeated intervals, turning the mode on and off, an existing surgery, low blood, and a start without Biotech. Do not delete unrelated surgeries.

Documentation closeout: player description in `docs/wiki/medical-devices.md`, technical queue conditions in this document. Update `COMPATIBILITY.md` only if the stated support status changes.

## Corrections to the other findings of the original analysis

| Original item | Checked finding and decision |
|---|---|
| Choking: two severity mechanisms | Constant growth and random change do coexist. With no source and the ability to cough, and with no extra modifiers, the mean change including constant growth is about −0.0625 per step. Removing +2/day automatically is not planned |
| Choking: repeated addition | There is no check for an existing state, but the final merge or coexistence, and the effect of several breathing caps, need an engine check. Do not call `setMax` a stacking penalty. Merging sources needs a separate decision |
| Shock: two severity mechanisms | Direct changes and `severityPerDayTended=-1.5` can compete. The 55% threshold applies to current shock; there is no permanent no-return flag. Recovery starts at blood loss ≤0.449. A balance change is not planned yet |
| Shock: treatment quality | The curve contains values above 1, but a universal 100% quality cap is not proven. Do not declare stabilization impossible |
| Shock: intervals in tooltips | Confirmed inaccuracy: the text says 300 ticks; the code uses 250/500. If the text is fixed separately, sync translations and `docs/wiki/injuries/hypovolemic-shock.md`. Do not change intervals to match the text |
| Repeat fracture of a healing bone | Possible. This is not a proven bug; a ban would add protection against a repeat fracture. Do not change it without a mechanic decision |
| Dry gangrene | Four days is a random-intensity parameter, not a required delay. Over a full day the infection chance is about 22.1%; auto-amputation competes with infection. Do not fix the balance automatically |
| Epinephrine | A separate menu item depends on `EnableAdrenaline`, but `JobDriver_ProvideFirstAid` can administer the drug without that flag. The claim that there is no other path is wrong. Define the shared use policy separately |
| Stock injury classes | Replacing them with `BetterInjury` / `BetterMissingPart` is intentional. Reverting that is not a fix for the defects listed here |
| Blood-loss lethality | Separating maximum and lethal severity is intentional. A guaranteed "double kill" from changing the threshold is not proven. Do not change the thresholds |
| Six Harmony postfixes | The current arrangement is confirmed. This plan does not require new patches or a change of patch type |
| XML flags, resuscitation, and the lung setting alias | Using flags in XML, treating vanilla HeartAttack, and keeping the old `LungCollapseChanceOnDamage` key are intentional |

## Optimizations: a separate stage after the fixes

There are no performance measurements yet. The order below is not a ranking of expected speedup. Do not do optimizations together with the DLC alignment or the stroke toggle.

1. Remove the iterator allocation from the tick walk in `HandlerChain.GetActive`, while keeping the dynamic settings check.
2. Do not add a strict `Human` filter in the damage handler: `Patches/Mod_HumanoidAlienRaces.xml` adds the component to alien races. Separately check callback reset for the off-map case and for an exception. A false bind of the next hit is not proven.
3. Repeated wound walks can be reduced only while keeping body-part relations, individual results, and cache-update rules. One bool per pawn is not enough.
4. A shared snapshot for acidosis and related states needs a check of change order in the same tick. The same interval does not prove they are equivalent.
5. Fix the repeated list creation in `Poolable.Initialize` as its own small change. Do not also change the shared pool contract and every lock.
6. Consider caching `GeneDef` separately from immunity-check frequency. Moving the check to 150 ticks changes reaction time.
7. Boxing settings values through `object` is confirmed, but modifiers are already evaluated after the interval check. The two death handlers cannot be replaced by opposite branches of one flag: tracked and untracked pawns differ.
8. Repeated acidosis causes are already deduplicated. Clearing them changes the displayed history and does not remove the ring-buffer walk. Define the required semantics first.
9. If LINQ random selection is replaced, keep inclusions, exclusions, no repeats, probabilities, and, where possible, random-value consumption.

Smaller proposals: estimate data preparation for the gangrene walk, including the early exit; cache unchanged def extensions; measure the benefit of replacing `Any`. A manual loop instead of `Contains` does not remove the linear search, and owner plus severity do not prove the wound is still in the list. An early exit on zero adrenaline damage is a behavior change, not a pure optimization. The unused def type `SingleExecutionPerInterval` does not create current tick cost.

Closeout for this stage: record measurement results, behavior-preservation checks, and the change scope in this developer doc. Do not change the wiki for internal optimizations that do not change behavior. For an adrenaline change, the owner is `docs/wiki/injuries/adrenaline-rush.md`.

## Checks and required closeout of a future implementation

1. Do the stages as small independent changes. Do not touch other mods or unrelated user edits.
2. After C# changes, run `Custom\Scripts\Build.ps1 -Target MoreInjuries` from the workspace root. Use Release. Do not run deploy or the game as an automatic check. For XML-only changes, check structure, references, and condition consistency. A C# build is not required.
3. Run the matching existing tests. For keyed-text changes, run the localization checks from `docs/FEATURES.md`. Add new checks for important branches, not to copy the implementation into a test.
4. Keep in-game scenario results separate from compilation. Do not declare the engine's internal reaction to raw `severityInt`, choking merge, or notification-list caching checked without matching evidence.
5. The last stage updates the listed owner wiki and developer pages from the final code, not from this plan. Remove stale claims, keep links, and do not hand-edit generated blocks. Set `code-synced` with the check date on developer pages. The wiki must not contain developer statuses.
6. Change `docs/FEATURES.md` only if the owner map, owner path, or validation command changes. Change `COMPATIBILITY.md` only if support or conflict status changes.
7. Update this file: separate changes that were actually made from remaining proposals, and record checks and open questions. The existence of a plan is not an `implemented` status.

## DLC matrix for VacuumResistance_Total

The working owner is Odyssey, from the already aligned hypoxia and shock defs. This is not a check against the original DLC XML.

| Configuration | XML | C# `HasOxygenDeficiencyImmunity` |
|---|---|---|
| Neither DLC | Gene modifiers do not load | `pawn.genes` is absent; returns `false` |
| Biotech only | `Deathless` loads. `VacuumResistance_Total` does not load | Only `Deathless` is checked |
| Odyssey only | `VacuumResistance_Total` modifiers load. `Deathless` does not load | `Deathless` is not checked. Breathless is checked only if `pawn.genes` and the gene def exist |
| Both DLCs | Both modifiers load | `Deathless` first, then `VacuumResistance_Total`. Either active gene is enough |

Coefficients are unchanged: 0.05 on choking and tourniquet, 0 on blocks of new hypoxia. A slower choking rate was not turned into a block. The direct zero blood-loss path did not gain an immunity check. The shock immunity-check interval was not moved.

## Temporary-tamponade formula

`TemporarilyTamponadedMultiplier` = `Lerp(1, base, remaining / total)`. When `total <= 0`, the multiplier is 1. The start (`remaining == total`) gives the full base effect. The middle is halfway between 1 and the base. The end (`remaining == 0`) gives 1, meaning no extra reduction. The rest of the bleeding formula was not changed. An in-game timer check after saving was not done.

## Blood-draw queue

The periodic handler creates `ExtractWholeBloodBag` only if that bill is not already in `BillStack`, using the same check as the UI. Unrelated surgeries are not deleted. `HemogenFarm` and `ExtractHemogenPack` are read only when `ModsConfig.BiotechActive`. `Patch_Alert_AwaitingMedicalOperation` was not changed.

## Load without a cache

A missing `jobParameters` node does not set a load error. Wounds, missing parts, surgeries, immunity, and a dead pawn's state are not changed because the cache is empty. This path calls only `FixMisplacedBionicsModExtension.FixPawn`: a bionic on an invalid part is moved to a present valid part, or removed if no such part exists. That does not prove the whole old-save migration is safe. Null entries in the loaded list are skipped and do not start health-state deletion.

## Implementation result

Plans 1–3 and stages A, B, and C are done. Optimizations were not done. An in-game check of the four DLC configurations, of toggling the stroke setting, and of old-save copies does not replace compilation.
