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

- Biotech (DLC)
- Combat Extended
- Dub's Bad Hygiene

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