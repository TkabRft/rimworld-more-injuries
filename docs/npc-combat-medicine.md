# NPC combat medicine

code-synced: 2026-10-20

Status: implemented. Compilation and automated contracts target the pinned RimWorld 1.6 API. The shared implementation is selected for 1.7 as well; live 1.6/1.7 acceptance is still required. Metadata and packaging do not establish tested 1.7 support. No new dependency or DLC requirement is added.

Player documentation: [medical devices](wiki/medical-devices.md). Shared treatment selection: [medical runtime](medical-runtime.md). Optional integration boundaries: [compatibility](../COMPATIBILITY.md).

## Ownership and lifecycle

`AI/NpcCombatMedicine/` owns profiles, generation, decisions, jobs, settings UI, optional adapters and dose history. `Defs/Internal/NpcMedicalProfiles.xml` provides five `NpcMedicalProfileDef` records. `NpcMedicalProfileExtension.profile` overrides the profile on a pawn kind, then faction; otherwise the actual faction technology selects a default. Undefined/Animal technology uses Neolithic, Archotech uses Ultra. Profile fields remain XML interfaces, independent of colony research.

The inserted humanlike think node handles living fleshy NPC combatants, armed visitors and prisoners. Player-controlled colonists, colony slaves and nonhuman actors are excluded. Medical capability is checked separately: incapable doctors may carry kits and take a permissible performance drug. Prisoners may self-treat; movement uses ordinary reachability, forbidden areas and doors. Withdrawal/evacuation ownership requires an existing prison break for a prisoner. The module never starts breaks, unlocks confinement or changes faction ownership.

Medical-only mode adds treatment without movement decisions. The default treatment-aware mode adds safer positions within 12 cells and an estimated 600-tick travel budget. Full survival also permits individual medical withdrawal and evacuation of nearby downed faction members, without changing group retreat decisions. Existing transport, kidnapping, exit and ritual duties retain ownership. Completing a temporary treatment/positioning job returns control to ordinary duties.

Patient searches use spatial queries within 12 cells, or 24 for medics. Arrest, critical airway failure, shock, impaired breathing/pumping and dangerous blood loss lead triage; comparable emergencies prefer own faction/group, then allies/player colonists, then neutrals. Cross-faction care requires an emergency. All four care flags default on; self-care is independent of the own-faction flag. Current pawn hostility is checked, including during jobs. Player medical-care restrictions and mutant medical incompatibilities apply. A prisoner's own care may override their assigned medical-care category without overriding physical access.

`EmergencyCare.NpcContext` reuses MI's weighted emergency selector without colony research. It supplies capability flags, a bounded item resolver and a separate clinical-drug gate. The existing colonist/drafted caller path remains the default context. Bleeding control, transfusion, CPR, defibrillation, suction, epinephrine, splints and native tending use existing medical behavior. Internal bleeding, dilution and organ/neurological impairment affect urgency and feasible care; surgery-only conditions receive no invented field cure. Routine decisions do not invoke the numerical prognosis simulator.

Decisions and local threat snapshots are throttled to 150 ticks. Supplies are sought in doctor/patient inventories, permitted nearby same-faction inventories, then reachable ground items within 12 cells. Collection uses physical retrieval and reservations, including native tending from another pawn's inventory. Jobs revalidate their targets; failed reservations/retrieval return control to normal AI.

`NpcMedicalReservations` applies the same exclusive patient availability check to self-care and other-pawn care. An NPC receiving treatment must not start competing self-treatment; managed self-device jobs also reserve their patient. Existing native tending jobs retain ownership rather than being interrupted by a new MI decision. CE's tactical manager calls `StartJob` directly, so the adapter performs `Job.TryMakePreToilReservations` quietly before returning an MI job. Failed preflight releases only that proposed job's claims, leaving the current job and other doctors' reservations intact. No job is started just to discover a predictable reservation failure.

`JobDriver_NpcTend` owns a bounded, single-application collection/positioning sequence and retains native `Toils_Tend.FinalizeTend` for tending quality, medicine consumption, experience and health callbacks. It reserves one dose and transfers it directly from the selected inventory into the doctor's carry tracker; ground supplies still require physical collection. It does not invoke native medicine collection or Smart Medicine's globally rewritten pickup toil, and it does not stage inventory medicine on the ground. Standing NPC patients can be treated in place without requiring bed-only laying status. A temporary patient wait is released only if it is still the exact wait this application created; player-controlled patients receive no forced wait. Changed inventory ownership, failed transfer, unrelated cargo, lost capability, hostility or care restrictions abort safely.

Unused selected medicine still in the carry tracker returns to the pawn's inventory on cleanup, up to remaining mass/bulk capacity. This does not create medicine, reclaim items already dropped on the ground, take unrelated carried cargo or bypass capacity; overflow retains normal cleanup behavior. No new save keys or Harmony targets are required for this collection path.

NPC medical travel has a 2,500-tick deadline measured from the existing job start tick, checked during movement rather than during the medical application. It covers native tending movement and MI device/patient retrieval, including moving inventory holders. Repositioning checks native path cost and has a hard 600-tick job expiry; it cannot remain protected indefinitely while walking a detour. NPC splinting and other selected device actions finish after one application. Evacuation selection requires free carrying hands, manipulation, sufficient movement and native carrying space; map exit requires the intended patient actually to be carried.

Generation observes completed gear generation and accepted pawn generation. Group enumeration is materialized once under a scoped generation context, then yielded in its original order. Only pawns with fresh gear events are provisioned; reused world pawns are never trained as new pawns. Existing items are counted, with no confiscation. Loading a save or changing settings grants nothing. Disabling stops new decisions/grants; current applications and carrying jobs can finish safely.

`NpcMedicalMemory` saves MI-only medic roles and drug history through a game component; it stores pawn references and primitive values, never third-party objects. Existing save keys are unchanged. New jobs and this component become part of saves; keep a backup before removing the mod mid-treatment. No migration or load-time replenishment is performed.

## Audited item balance and capacity

These existing values are retained. Injector inheritance through `MedicalDrugBase`/`AutoInjectorBase` is included in the audit, rather than requiring redundant nodes on every injector. CE bulk is conditional on CE being active.

| Item | Mass kg | CE bulk | Classification |
|---|---:|---:|---|
| Bandage | 0.05 | 0.05 | Personal, medic, scavenged |
| Hemostatic agent | 0.03 | 0.03 | Personal, medic, scavenged |
| Tourniquet | 0.08 | 0.08 | Personal, medic, scavenged |
| Splint | 0.10 | 0.10 | Medic |
| Epinephrine | 0.05 | 0.05 | Personal, medic, scavenged; clinical |
| Morphine | 0.05 | 0.05 | Performance; Industrial medic supply |
| Ketamine | 0.05 | 0.05 | Medic; carried but not automated |
| Chloroform | 0.20 | 0.30 | Medic; carried but not automated |
| Saline | 0.25 | 0.25 | Personal Industrial+, medic, scavenged fluid |
| Whole blood | 0.75 | 0.50 | Resolved fluid |
| Suction device | 0.40 | 1.50 | Industrial+ medic, reusable |
| Defibrillator | 0.50 | 2.00 | Industrial+ medic, reusable |
| Thoracoscope | 0.75 | 2.50 | Surgery only |

Comparison baseline: herbal medicine 0.35 kg, industrial/glitterworld medicine 0.50 kg (CE bulk defaults to 1 unless overridden); yayo 0.05/0.05, go-juice 0.10/0.20 and hemogen 0.50/0.25 kg/bulk. Consumables are smaller than complete medicine kits, while reusable devices consume appreciable bulk.

Actual instantiated item statistics determine capacity. Vanilla uses remaining carrying mass. CE refreshes its completed gear/inventory cache through `GetAvailableWeight(true)` and reads `GetAvailableBulk(false)` before kit planning, then invokes `CompInventory.CanFitInInventory(Thing, out int, bool, bool)` for every actual grant and updates its cache afterward. This includes other mods' effective statistics and hemogen's different mass/bulk. Missing or unsupported CE capacity APIs stop grants rather than falling back to a mass-only budget.

Generation rolls each medic kit before selecting its carrier. Medically capable pawns who can fit the core kit take precedence, ordered by existing Medical skill and pawn ID. If none can fit the entire core, select the pawn able to carry the largest fraction of its treatment types, then apply the same skill ordering. Existing supplies count toward the core. The allocator reserves one of each rolled essential treatment/device before adding extra consumable quantities; performance drugs and carried-only sedatives come last. Thus large bandage/medicine stacks cannot crowd out a defibrillator or suction device when the core fits. No capacity policy can guarantee a device for a group whose capable doctors cannot carry it. A rejected blood roll is not replaced with saline. Never shrink item stats merely to force a kit to fit; adjust kit quantities first. Weapons, armor and ammunition are never removed.

## Provisioning rules

Group strength is `clamp01((points - 400) / 2600)`. Quotas round to the nearest pawn. Medic selection uses core-kit capacity and existing Medical skill as described above; other recipients are randomized. Standalone generation uses low-point probabilities. Optional training, off by default, raises newly generated medic Medical skill to floors 4/6/8/10/12 without lowering skills or removing incapabilities.

At 2,000 points, the default Ultra/Empire medic quota is 13.0769%: 1-3 fresh eligible combatants round to zero, 4-11 to one, and 12-19 to two. Actual selection is capped by doctor-capable candidates. Roles do not change pawn titles; high Medical skill is not guaranteed while training is disabled. CE capacity can reduce a selected medic's actual kit.

The original group points are captured before enumeration. With Development mode enabled, generation logs `More Injuries NPC medicine:` summaries for total generated/fresh eligible pawns, profile, equipped count, capable doctors, requested medic quota and actual selected medics. Each selected medic's remaining mass/bulk before provisioning, requested/granted unit counts, pawn ID, name, Medical skill and actual carried medical supplies are logged. These diagnostics distinguish zero quota, incapabilities, missed gear events and missing kit grants without altering prevalence or granting equipment to existing pawns.

The reported raid logs on 2026-10-20 confirmed successful medic selection with an empty kit in one group and only seven bandages plus one hemostatic agent in another. That evidence drove the capacity-aware carrier choice, fresh CE budget and core-first allocation; increasing the medic percentage would not repair those grants. Validation on that date passed the pinned Release build, 199 definition/runtime/localization contracts, 24 Harmony/transfer fixtures and package hash/XML/load-layout checks. Source-linked fixtures exercise capacity selection/allocation, whole-blood versus hemogen budgets, direct own/other-inventory transfer, failed transfer and unused-dose restocking. New live raids are still required to accept the completed game behavior, including existing mod patches and already-ill arrivals. Active tending save/resume also needs acceptance because its toil sequence changed; no persistence keys changed.

| Technology | Equipped | Medics | Performance slot | Morphine/yayo/go-juice |
|---|---|---|---|---|
| Neolithic | 35–50% | 2–4% | Scavenging only | — |
| Medieval | 45–65% | 4–6% | Scavenging only | — |
| Industrial | 65–90% | 6–10% | 20–40% | 70/25/5 |
| Spacer | 85–97% | 8–12% | 25–50% | 20/50/30 |
| Ultra | 90–99% | 10–15% | 30–60% | 0/25/75 |

Personal kits follow the approved XML tier tables: Neolithic/Medieval bandages and herbal medicine; Industrial+ add hemostats, tourniquets, industrial medicine, epinephrine and fluid slots. Spacer/Ultra improve quantities. Personal profiles contain no glitterworld medicine.

Medic kits replace personal kits. Low tiers carry larger bandage/herbal supplies and splints; Medieval adds tourniquets. Chloroform chances are 25%/50%. Industrial adds six each of hemostats and industrial medicine, three tourniquets, four fluid slots, two splints, two epinephrine and one morphine; ketamine chance is 25%, and a 50% device roll selects either defibrillator or suction. Spacer has six fluid slots, both devices and a 50% chance of 1–2 ketamine. Ultra has eight fluid slots, ten hemostats, six glitterworld medicine replacing industrial, and a 75% chance of 1–2 ketamine. Ketamine/chloroform have no automatic administration path.

Ordinary consumable counts scale by `supply multiplier × (1 + 0.5 × strength)`. Reusable devices, sedatives and performance doses do not scale. Existing supplies reduce top-ups. Every fluid slot uses the same 75% saline / 25% resolved blood selection, including scavenging. The shared blood-treatment resolver checks active Biotech, integration settings and the hemogen definition; otherwise it uses whole blood.

Scavenging defaults to 3%, adds at most one item per equipped pawn and never adds devices or skill. Ten percent of successful selections are glitterworld medicine; otherwise the next tier's personal consumables/performance drugs are candidates, with fluids retained as a category using the shared resolver. Ultra falls back to its own tier. Existing-only performance provisioning excludes new performance drugs from scavenging as well.

## Drug policy and settings

The NPC controls use the shared expandable settings UI. Both AI behavior and drug provisioning have explicit choice menus with per-choice tooltips; rendering does not change saved values. The UI and persistence contract are documented in [mod settings](settings.md).

| Setting | Default | Range |
|---|---|---|
| Module / mode | Enabled / treatment-aware | Off; three modes |
| Supply / medic / performance prevalence | 1× each | 0–2× |
| Scavenging | 3% | 0–10% |
| Own / friendly / neutral / player care | All enabled | Independent flags |
| Performance drugs / preset | Enabled / tech-scaled | Tech-scaled, prefer cheap morphine, existing provisioning only |
| Eligible carrier use chance | 100% | 0–100% |
| Performance doses per engagement | 1 | 0–3 |
| Performance minimum interval | 20,000 ticks | 0–60,000 |
| Clinical repeat interval | 600 ticks | 0–2,500 |
| New medic skill floors | Disabled | On/off |

At most one performance dose is provisioned when an eligible drug is absent, including medic-kit morphine and scavenging. Existing-only and disabled performance provisioning also exclude medic-kit morphine. Existing vanilla/other-mod drugs are preserved. Morphine's base value 11 versus yayo 21 and go-juice 53 makes it a cheap loadout substitute, not a mechanical equivalent: pain suppression comes with sedation, addiction and respiratory risks. Automatic morphine use requires appreciable pain and preserved consciousness, breathing and circulation, without shock/significant blood loss. Teetotalers, overdose, active stimulant/sedative effects and incompatible physiology restrict use. Normal ingestion outcomes preserve tolerance, addiction, overdose and gene behavior.

Engagements are continuous visible local combat-threat episodes, reset after 2,500 ticks without a relevant threat. Carrier chance is rolled once per episode. Dose count resets between episodes; the minimum interval persists across them and across saves. Managed vanilla combat-drug selection cannot bypass MI's policy for the managed drugs. Existing-only changes provisioning, not the ability to use existing permitted supplies.

Clinical epinephrine is independent: shock/resuscitation indication, repeat interval and adrenaline safety threshold are checked separately, including immediately before applying the injector. A performance dose does not consume clinical allowance. Ketamine/chloroform remain carried supplies for a future behavior expansion.

## Optional adapters

Runtime binding checks active package IDs, declaring assembly, exact parameter/return types and static/instance ownership. Unsupported signatures leave the affected external behavior intact and log a diagnostic. No global unpatch, external setting mutation, compiled optional reference or third-party saved object is used.

| Package | Boundary |
|---|---|
| `CETeam.CombatExtended` | Remaining mass/bulk through `CompInventory`; `CompTend.TryGiveTacticalJob` preserves active MI/native tending and patients already receiving care. MI tactical jobs pass reservation preflight before CE receives them; failed candidates are released quietly. Otherwise CE retains control. Shared stabilization receives exclusively bounded permitted medicine. Unsupported inventory APIs stop new MI provisioning instead of assuming unlimited bulk. |
| `Memegoddess.SmartMedicine` | Recognized medicine/job prefixes use bounded medicine/counts for managed NPC tending. MI's custom tend driver suppresses Smart Medicine's inventory-drop/count rewrite. Colonist settings, stocking and unrelated tending retain Smart Medicine behavior. |
| `Mlie.EnemySelfPreservation` | Guard `MUR_ESP.Pawn_PostApplyDamage.Postfix` only when full survival owns that pawn's withdrawal. |
| `NinaGoblin.NoOneLeftBehind` | Guard `CM_No_Pawn_Left_Behind.JobGiver_Rescue.TryGiveJob` only when full survival owns evacuation. |
| `Andromeda.NITICE` | `Integrations/NiceInventoryTab/NiceInventoryTabGuard` guards the recognized `NITIntegrationCE.MakeItemSlot_Patch.IsGrenade(ThingDef)` method. Missing definitions/tag lists return false; all tagged definitions retain the original check. No weapon definition is mutated. This inventory UI repair remains active independently of the NPC medicine setting. |

Lower modes retain external retreat/rescue; disabling MI restores external decisions. Adapter signatures were inspected against the available 1.6 sources/assemblies. This establishes the source contract, not successful execution of the entire game's Harmony chain.

## Validation and acceptance

`NpcCombatMedicineTests` covers policy thresholds, quotas, fluid statistics, independent budgets, hostility/care flags, prisoner survival ownership, inherited item mass/bulk and mandatory medical-item classification. `EmergencyIntegrationTests` exercises the actual shared selector for NPC self-hemostasis, clinical epinephrine and bounded CE fallback. Existing emergency/physiology/localization and real-Harmony bleeding fixtures remain regression coverage. These fixtures do not execute real game movement, inventory toils or serialization.

`NiceInventoryTabGuardTests` reproduces the inspected integration's null-tag grenade check and applies the production guard through real Harmony. It verifies the exception is avoided, tagged grenade/rifle/nonweapon behavior is preserved, no definition data is mutated, and unpatching restores the original failing check. The full custom inventory tab still requires in-game acceptance.

`NpcMedicalReservationTests` source-links the production reservation/cleanup helper. Main-thread game boundary fixtures cover a patient already reserved by another doctor attempting self-care, competing doctors, partial preflight failure retaining unrelated reservations and untouched inventory, idempotent reservation at job start, and unused medicine restocking with capacity/overflow and item identity preserved. The complete CE scheduler and native tending toils still require in-game acceptance. No pawn-card drawing, footer alignment or shared GUI alignment state is modified by these fixes.

Validation on 2026-10-19: the prescribed Release build passed; all 193 runtime/localization tests, including localization coverage, and all 21 reservation/pickup/Harmony tests passed. The latter include real-detour tests of the production ingestion and inventory guards as well as source-linked reservation and device-transfer cases. The refreshed 235-file runtime package passed XML, load-layout and SHA256 validation. The packaged main assembly matches the final successful build. No game was launched or installed for these checks.

Manual acceptance must cover 1.6 and 1.7 independently: generate each tier at low/high points; verify medic selection/new-only training and capacity with blood versus hemogen; verify preexisting supplies, scavenging and no normal personal glitterworld medicine. Exercise all modes, nearby ally/neutral/player care flags and changing hostility, a confined prisoner and an existing breakout, safer positioning, failed retrieval/reservation, medical withdrawal and downed evacuation. Test performance presets/limits followed by indicated clinical epinephrine, sedative carry-only behavior, save/reload, toggling during treatment and carrying, and unchanged colonist First Aid. Repeat with each optional mod alone, Smart Medicine/ESP/NOLB together and CE combinations; exercise an unsupported signature's fallback. No completed in-game acceptance is claimed here.

## Job-progress audit

The 2026-10-19 audit covered NPC decisions, native tending/ingestion handoff, shared MI device jobs, inventory/ground retrieval, First Aid rescan queues, patient waiting, repositioning, withdrawal, evacuation and cleanup. Source inspection and bounded fixtures identify concrete failure paths; this is not a proof against every possible third-party scheduler or pathfinding interaction.

| Failure path | Implemented guard | Verification boundary |
|---|---|---|
| Self-care competes with another medic; CE starts an unreserved job | Exclusive patient checks for self and others; tactical reservation preflight; job-specific rollback | Source-linked contention/cleanup tests; complete game scheduler pending |
| Inventory medicine is staged on the ground or field tending inherits bed-only/native pickup assumptions | Direct one-dose inventory-to-carry transfer, standing-patient application and native finalization; no native collection loop | Production transfer/failed-transfer/restocking fixtures; full tending scheduler pending |
| Medic role is assigned to an overloaded doctor, or consumable extras fill space needed by devices | Fresh CE mass/bulk budget, capacity-aware carrier selection and core-first allocation | Production allocator/selection fixtures with both capacity dimensions and blood substitution; new live raids pending |
| A moving inventory holder is captured before the hauling toil assigns its target | Read the live holder target, verify its original inventory still owns the item, use a per-job toil list | Source reviewed; live holder movement/transfer and save/resume pending |
| A missing/contested device, unrelated cargo or failed pickup becomes the treatment target | Abort without retargeting; handle already-carried devices; count actual transferred units; exit resolved treatment before zero-count reservations | Production pickup/reservation toils exercised by boundary fixtures |
| A single application over-collects and searches for replacement supplies | Required count capped to one, including preflight; no replacement search after that application; NPC splinting uses single application | Source reviewed plus partial-pickup tests; actual job collection pending |
| A performance dose is rejected but native ingestion keeps retrying its unconsumed count | Clear only that MI job's requested count; leave dose and clinical allowance intact | Actual production Harmony patch tested against count-based ingestion replay |
| An ingestion outcome replaces the current job or incapacitates its user | Capture performance intent before ingestion; preserve cooldown accounting; medical finalization checks job identity before touching targets or finishing | Production ingestion detour test; shared finalizer source reviewed |
| Treatment cleanup cancels an unrelated wait or leaves a forced treatment pose behind | Release only the exact wait created by this application; restore its prior posture only when medically appropriate | Source reviewed; real wait/mental-state/save-resume acceptance pending |
| Zero, negative or non-finite work rates produce invalid or effectively endless waits | Reject those rates; recheck doctor capability during device/corpse jobs; retain finite slow-doctor timing | Numeric policy tests and existing emergency-selector regressions |
| Automatic CPR repeatedly targets a mechanical pumping failure it cannot reverse | Require restorable circulation for arrest-only automatic CPR; a separate choking obstruction remains treatable; manual CPR is unchanged | Shared-selector regression with irreversible pumping failure and coexisting choking |
| Failed First Aid repeatedly queues instant rescans in one game tick | One tick between scans; rejected jobs are not deliberately requeued | Source reviewed; actual native queue acceptance pending |
| Medical travel chases a moving pawn indefinitely, or protected repositioning takes an excessive detour | NPC movement deadline from saved native job age; exact path check and 600-tick reposition expiry | Deadline policy tests; actual path costs and dynamic obstacles pending |
| Repeated evacuation attempts cannot pick up a patient, or exit loses that patient | Carrying/capability/space prechecks; verify carried patient before exit; normal failure/drop cleanup | Source reviewed; native carrying/map-exit acceptance pending |

No new save fields, job identifiers, dependency, Harmony target or pawn-card drawing changes were introduced by the progress audit. The existing ingestion hook was moved to its own source file without changing its identity. Successful procedures can legitimately repeat for ongoing bleeding or resuscitation; finite slow treatment is not classified as a deadlock. Already-dropped supplies are not replenished or teleported back to a pawn.

## Mandatory maintenance for new drugs and devices

1. Classify every new MI medical definition explicitly: personal, medic, scavenged, surgery-only or carried-but-not-automated. The development-only definition coverage test must fail until the new item is deliberately reviewed; there is no runtime auto-classification.
2. Audit effective inherited mass and CE bulk against vanilla/CE comparators. Review actual capacity and existing-item accounting before changing kit quantities or balance.
3. Update appropriate technology profiles, fluid/scavenging categories, performance/clinical roles, dose limits, physiological contraindications and retrieval/reservation rules. New drugs do not automatically enter battlefield administration.
4. Extend focused contracts, optional adapters if their boundaries change, and the game acceptance matrix. Refresh this owner and the owning player page, recording the code-sync date.

Future faction-editor integration should author/select `NpcMedicalProfileDef` and `NpcMedicalProfileExtension` records through these same precedence rules. A faction editor UI is a follow-up, not a dependency of this module.
