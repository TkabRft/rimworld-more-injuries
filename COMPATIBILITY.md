# Known Issues and Incompatibilities

This section lists known issues and incompatibilities with other mods that have been identified during testing and development of More Injuries. If you encounter any issues or incompatibilities with other mods, please report them on the [GitHub issue tracker](https://github.com/frederik-hoeft/rimworld-more-injuries/issues) with a minimal, reproducible example. - meaning the minimum set of mods in a new save game that still reproduces the issue. To allow for a quick resolution of the issue, please provide as much information as possible, including the exact steps to reproduce the issue, the expected behavior, and the actual behavior observed.

> [!IMPORTANT]
> When reporting an issue, please make sure to include the following information:
> - The minimal set of mods required to reproduce the issue. This list can be obtained by starting a new game with only the required mods enabled and adding the mods back one by one until the issue reappears. This process may be sped up by using any helpful information from the game logs.
> - The exact steps necessary to reproduce the issue *in a new save game*. This information is crucial to help the developers understand the issue and find a solution.
> - The expected behavior and the actual behavior observed. This information helps the developers to understand the issue and verify that the issue has been resolved.
> - Any stack traces, error messages, or other relevant information from the game logs that may help to identify the cause of the issue. If possible, please load the [Visual Exceptions](https://steamcommunity.com/sharedfiles/filedetails/?id=2538411704) mod which will help indentify integration issues with other mods.

> [!CAUTION]
> Before reporting exceptions or errors from the debug console, please make sure that at least one line of the stack trace mentions `MoreInjuries` or that you are able to prove that the issue is caused by More Injuries. Otherwise, the issue may not be related to More Injuries and is unlikely to be resolved by the developers for lack of avenues to investigate the issue.

## Compatibility with Other Mods

If a mod is not explicitly listed here, it is assumed to be compatible with More Injuries.

### Officially Supported Mods

The following mods are officially supported by More Injuries and even integrate directly with its features:

- Biotech (DLC): MI emergency care recognizes active Superclotting. It skips minor bleeding and automatic tourniquets, favors bandages, and treats feasible non-bleeding emergencies first. First Aid also skips ordinary tending for these patients; normal Doctor tending and safe tourniquet removal remain available. See [First Aid](docs/wiki/medical-devices.md#first-aid).
- Combat Extended: MI corrects duplicated stabilization on its injury and missing-part implementations so CE's bleeding multiplier applies once. Prognosis separates current bleeding control held constant from no further tending with CE decay and staged MI hemostasis, including the choking source wound. Suppressed wounds without a reliable baseline receive an uncertain estimate. Drafted First Aid and routine MI emergency care share weighted feasible treatment selection, retaining MI bleeding-control preference before CE's native stabilization fallback with permitted medicine. CE batches can overlap MI-controlled wounds when another eligible wound requires care. See [Death Estimate](docs/wiki/medical-devices.md#death-estimate) and [First Aid](docs/wiki/medical-devices.md#first-aid). Ordinary tending priorities and CE's manual command are retained. Spalling is disabled by default; proper CE armor integration is deferred. Live saves, actual jobs and the full game patch chain still require in-game acceptance.
- Dub's Bad Hygiene

### NPC combat medicine adapters

The default-enabled [NPC combat medicine module](docs/npc-combat-medicine.md) adds optional, capability-specific integration. These adapters are implemented and their signatures have been checked against the available 1.6 sources/assemblies. Full in-game acceptance, including the complete Harmony chain and 1.7, remains pending; compilation and fixture tests do not establish live compatibility.

| Optional mod | Implemented boundary |
|---|---|
| Combat Extended (`CETeam.CombatExtended`) | Kit planning refreshes completed gear/inventory mass and bulk before choosing medic carriers. Core treatment/device allocation precedes extra consumables; actual grants recheck fit and update the cache. Its tactical tending preserves active treatment and patients already receiving care; MI tactical jobs pass reservation preflight before CE starts them. Failed candidates release only their own reservations and are skipped quietly. Otherwise CE retains control. Shared CE stabilization uses the bounded NPC medicine resolver. |
| Smart Medicine (`Memegoddess.SmartMedicine`) | Managed NPC medicine/job selection is bounded. MI tending transfers one reserved inventory dose directly to carrying, supports standing NPC patients, and calls native finalization without entering the globally rewritten medicine-pickup sequence. The existing adapter guards its starting-time drop/count rewrite for MI jobs. Colonist settings, inventory features and unrelated tending remain available. |
| Enemy Self Preservation (`Mlie.EnemySelfPreservation`) | Only Full survival suppresses its damage-response withdrawal for eligible MI-managed individuals. Lower modes retain its behavior. |
| No One Left Behind (`NinaGoblin.NoOneLeftBehind`) | Only Full survival suppresses its rescue job giver when MI owns that pawn's evacuation decisions. Lower modes retain its behavior. |

Recognition is automatic. Disabling the module restores external decisions. No optional mod is globally unpatched, reconfigured or required, and no installed files are modified. Runtime binding verifies declaring assemblies and exact signatures; an unsupported hook leaves the affected external behavior intact and emits a diagnostic. If CE's inventory interface is unsupported, new MI kit grants stop rather than assuming available bulk. Prisoners only enter MI withdrawal/evacuation ownership during an existing breakout, respecting ordinary access rules.

Nice Inventory Tab CE Integration (`Andromeda.NITICE`): MI also guards the recognized `NITIntegrationCE.MakeItemSlot_Patch.IsGrenade(ThingDef)` helper. The inspected 1.6 DLL dereferences `weaponTags` for every weapon; a missing list throws while filling the CE inventory tab. The guard returns false only when the definition or tag list is absent, and leaves tagged weapon/grenade handling unchanged. It edits no definitions or installed files, adds no dependency and remains active when NPC medicine is disabled. A real-Harmony regression reproduces and guards the failure; opening the complete custom tab in-game remains the acceptance check.

### Compatible Mods

These mods are known to be compatible with More Injuries, but they aren't directly integrated into More Injuries, and there may not necessarily be cross-interactions between features. We won't list every compatible mod here, but rather focus on those frequently asked about:

- Diseases+

### Partial Support

- Humanoid Alien Races: The integration adds injury tracking to fleshy races, including races that omit `isFlesh` inside a compatibility block. Explicit false values remain excluded. Mixed comp lists and already-present injury comps are handled without duplicate insertion. Custom alien anatomy and individual race mods still need game acceptance.
- Anomaly (DLC): Bleeding fleshy entities, including fleshbeasts, participate in hypovolemic shock and its trauma complications, and respect the direct blood-loss death prevention setting. Ordinary animals, mechanical entities, and entities with a zero race bleed rate are not added by this integration. This paragraph describes trauma eligibility. The separate vital-failure module now also covers appropriate fleshy animals, and emergency medical-device scanning includes eligible owned nonhuman patients. Regeneration clears fractures without a splint, and fades spinal paralysis and permanent brain damage other than a personality shift after the injury on that body part is gone. Chronophagy, unnatural healing, healer mech serum, luciferium, and biosculpter cycles are unchanged.

### Incompatible Mods

No additional incompatible mod is declared by this expansion. Death Rattle uses the transition workflow below.

### Death Rattle transition

- Death Rattle Continued (`Troopersmith1.DeathRattle`): MI takes priority during the supported 1.6 transition. Its XML operations and Harmony owner are neutralized while source defs remain available for loading. Follow the one-time in-game message: keep a backup, save a new copy, quit, disable Death Rattle and restart. Failed migration/save validation means keep Death Rattle enabled. Unknown saved extensions or dependent mods can block handoff. See [vital failure](docs/vital-failure.md), the [transition contract](docs/death-rattle-transition.md) and [expansion work](docs/medical-expansion-todo.md).
- Odyssey: vanilla vacuum resistance/exposure remains authoritative. Breathless and protective equipment do not grant immunity to internal pumping failure. No parallel vacuum damage source is added.
- Anomaly: mutant air-breathing, medical/drug restrictions, transformation cleanup, regeneration and shambler settings are respected. Native scripted deaths and death protection remain effective.

The implementation and source-linked tests target pinned RimWorld 1.6 references. Live migration, custom races and DLC scenarios require the documented game acceptance checks.
