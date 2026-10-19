# Mod settings UI

Status: implemented. code-synced: 2026-10-19.

Player instructions live in the [manual introduction](wiki/README.md#about-more-injuries).

`MoreInjuriesMod.DoSettingsWindowContents` owns scrolling and measures the rendered listing each frame. The listing has room for all expanded groups to prevent automatic columns; its measured height becomes the next frame's scroll extent. Content uses local coordinates, and the scroll position is clamped after groups shrink.

`MoreInjuriesSettingsUi` owns the general and legacy feature controls. `AI/NpcCombatMedicine/NpcMedicalSettingsUi` owns NPC controls. Both use `SettingsUi` for expandable section buttons, menu choices and sliders. Sections start collapsed and retain their expansion state during the session. Expansion state is UI-only and never serialized. Button height accounts for translated text wrapping. Section headers, action buttons, menu buttons, individual menu choices, checkboxes, slider labels and slider tracks all expose localized hover explanations.

AI behavior and performance-drug provisioning use explicit choice menus. The selected label and tooltip are resolved without changing the stored integer; only a menu selection writes the value. Existing enum-like integer meanings remain unchanged. Gameplay policy is documented in [NPC combat medicine](npc-combat-medicine.md).

## Persistence contract

`MoreInjuriesSettings` and the settings source generator retain all existing property names, serialized names, types and defaults. In particular, `LungCollapseChanceOnThermobaricDamage` keeps its explicit `LungCollapseChanceOnDamage` storage name. The UI refactor requires no settings migration or save conversion.

`SettingsSliderValue.AfterEdit` preserves the original stored value when the displayed slider position has not changed. Clamping the displayed position does not clamp storage. Opening or expanding settings therefore preserves precise values such as the 0.0625 cardiac-arrest chance, values outside the UI range, and tick values displayed in hours or days. After an actual edit, each control applies its existing range, unit conversion and rounding convention. The advanced-hearing prerequisite is enforced only after an explicit hearing-toggle change, rather than clearing a preference on repaint.

## Validation

`SettingsSliderTests` in `MoreInjuries.LocalizationTests` exercises unchanged precise and out-of-range values, transformed displays, and edits in probability, whole-number, hour and day units. Run the localization project for keyed-string coverage and the slider regressions. Compile the main project against its pinned references to validate the game's UI API surface.

In-game acceptance remains necessary: expand and collapse every group, scroll after collapsing a group near the bottom, hover each control and menu item, select each NPC mode and supply preset, and reopen settings after restarting the game with customized values. Check a narrow settings window and a loaded map's repair action. Do not invoke the repair action solely to inspect its tooltip.
