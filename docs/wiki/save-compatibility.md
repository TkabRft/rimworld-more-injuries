# Save compatibility

<!-- @generate_breadcrumb_trail {"template": "_:file_folder: {0}_", "connector": " :arrow_right: "} -->
_:file_folder: [More Injuries User Manual](/docs/wiki/README.md) :arrow_right: [Save compatibility](/docs/wiki/save-compatibility.md)_
<!-- @end_generated_block -->

Adding More Injuries to a colony that was saved without the mod does not delete wounds, missing body parts, scheduled operations, or immunity, and it does not revive dead pawns, just because the mod has no saved job data for those pawns.

On that first load, a bionic that was installed on a body part its recipe does not allow may be moved onto an allowed part that is still present. If no allowed part is present, that bionic is removed. This does not repair every old save. Wounds, missing parts, and dead pawns are left as they were.

A later save made with the mod keeps that job data. Loading it does not run the bionic move again.

These limits are what the load step does. They are not a guarantee that every older colony will look correct in game. Keep a copy of the save before adding the mod.

<!-- @generate_link_to_top {"template": "---\n_[back to the top]({1})_"} -->
---
_[back to the top](#save-compatibility)_
<!-- @end_generated_block -->
