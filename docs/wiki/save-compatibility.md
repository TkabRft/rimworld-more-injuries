# Save compatibility

<!-- @generate_breadcrumb_trail {"template": "_:file_folder: {0}_", "connector": " :arrow_right: "} -->
_:file_folder: [More Injuries User Manual](/docs/wiki/README.md) :arrow_right: [Save compatibility](/docs/wiki/save-compatibility.md)_
<!-- @end_generated_block -->

Adding More Injuries to a colony that was saved without the mod does not delete wounds, missing body parts, scheduled operations, or immunity, and it does not revive dead pawns, just because the mod has no saved job data for those pawns.

On that first load, a bionic that was installed on a body part its recipe does not allow may be moved onto an allowed part that is still present. If no allowed part is present, that bionic is removed. This does not repair every old save. Wounds, missing parts, and dead pawns are left as they were.

A later save made with the mod keeps that job data. Loading it does not run the bionic move again.

These limits are what the load step does. They are not a guarantee that every older colony will look correct in game. Keep a copy of the save before adding the mod.

## Death Rattle transition

Keep a backup of the original save. Load it with More Injuries and Death Rattle Continued both active and **Delayed vital failure enabled**. More Injuries takes over the overlapping emergency rules and converts supported Death Rattle conditions and operations, including off-map patients and bodies. Matching failures keep the greater severity; dead pawns stay dead.

Wait for the successful-transition notice, then:

1. Save under a **new name**.
2. Quit the game and disable Death Rattle.
3. Restart and load the new copy with More Injuries active.

More Injuries does not save for you or edit your mod list. The completed save is checked for remaining Death Rattle references before its dependency entry is removed. If a warning reports unsupported conditions, dependent mods, references or a save-validation failure, **keep Death Rattle enabled** and retain the original backup. A successful load-time notice is not a substitute for completing the new save without a warning.

No pulse becomes [cardiac arrest](/docs/wiki/injuries/cardiac-arrest.md#cardiac-arrest); oxygen deprivation becomes [respiratory failure](/docs/wiki/injuries/respiratory-failure.md#respiratory-failure). Liver, kidney and intestinal failures become their corresponding More Injuries failures. Natural [coma](/docs/wiki/injuries/coma.md#coma), [diffuse brain damage](/docs/wiki/injuries/brain-damage.md#diffuse-brain-damage) and [artificial coma](/docs/wiki/injuries/artificial-coma.md#artificial-coma) keep their severity and affected body part. Existing More Injuries disabilities are preserved.

Converted coma-induction operations require five industrial or glitterworld medicine. Old herbal-only selections cannot bypass this restriction. A blocked transition does not authorize removing Death Rattle.

<!-- @generate_link_to_top {"template": "---\n_[back to the top]({1})_"} -->
---
_[back to the top](#save-compatibility)_
<!-- @end_generated_block -->
