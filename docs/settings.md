# Mod settings UI

Status: implemented. code-synced: 2026-10-20.

Player instructions live in the [manual introduction](wiki/README.md#about-more-injuries).

`MoreInjuriesMod.DoSettingsWindowContents` owns scrolling and measures the rendered listing each frame. The listing has room for all expanded groups to prevent automatic columns; its measured height becomes the next frame's scroll extent. Content uses local coordinates, and the scroll position is clamped after groups shrink.

`MoreInjuriesSettingsUi` owns the general and legacy feature controls. `AI/NpcCombatMedicine/NpcMedicalSettingsUi` owns NPC controls. Both use `SettingsUi` for expandable section buttons, menu choices and sliders. Sections start collapsed and retain their expansion state during the session. Expansion state is UI-only and never serialized. Button height accounts for translated text wrapping. Section headers, action buttons, menu buttons, individual menu choices, checkboxes, slider labels and slider tracks all expose localized hover explanations.

AI behavior and performance-drug provisioning use explicit choice menus. The selected label and tooltip are resolved without changing the stored integer; only a menu selection writes the value. Existing enum-like integer meanings remain unchanged. Gameplay policy is documented in [NPC combat medicine](npc-combat-medicine.md).

## Death policy and logging

`PreventDeathByLethalDamageThreshold` defaults to true and appears under General Settings as **Prevent death from cumulative injury damage**. `Patch_LethalDamageThreshold` skips only `Pawn_HealthTracker.ShouldBeDeadFromLethalDamageThreshold`, returning false for every pawn without a race, faction, DLC or delayed-vital-failure prerequisite. Disabling the setting restores the native check. Required-capacity death, destroyed vital parts, lethal hediffs, execution and death-on-downed remain separate routes. No injury severity or health-scale value is changed.

The physiology snapshot excludes the summed-injury limit from its cerebral structural ceiling while this setting is enabled, retaining the surviving-brain health limit. The setting participates in the prognosis cache stamp so changing it while paused refreshes both estimates.

`EnableLogging` is the master switch for all MI-owned messages, warnings, errors, configuration diagnostics and existing critical messages. The logger is silent before settings initialization. NPC decision, pain, attempt and generation diagnostics require both this switch and developer mode; their early guards avoid building diagnostic state or detailed supply summaries while logging is off. Optional integration warnings and transition errors use the same logger. `EnableVerboseLogging` requires the master switch too. Debug configuration validation still throws when appropriate; suppressing MI output does not suppress exceptions or logs produced by the game or other mods.

## Persistence contract

`MoreInjuriesSettings` and the settings source generator retain all existing property names, serialized names, types and defaults. In particular, `LungCollapseChanceOnThermobaricDamage` keeps its explicit `LungCollapseChanceOnDamage` storage name. The UI refactor requires no settings migration or save conversion. The cumulative-injury option adds one settings entry; existing settings files receive its true default when the entry is absent. Existing saves require no migration. Turning it off can make an already over-threshold pawn die on the next native health-state check.

`SettingsSliderValue.AfterEdit` preserves the original stored value when the displayed slider position has not changed. Clamping the displayed position does not clamp storage. Opening or expanding settings therefore preserves precise values such as the 0.0625 cardiac-arrest chance, values outside the UI range, and tick values displayed in hours or days. After an actual edit, each control applies its existing range, unit conversion and rounding convention. The advanced-hearing prerequisite is enforced only after an explicit hearing-toggle change, rather than clearing a preference on repaint.

## Validation

`SettingsSliderTests` in `MoreInjuries.LocalizationTests` exercises unchanged precise and out-of-range values, transformed displays, and edits in probability, whole-number, hour and day units. Run the localization project for keyed-string coverage and the slider regressions. Compile the main project against its pinned references to validate the game's UI API surface.

`LoggingAndDeathPolicyTests` source-links the production logger and runs the actual lethal-threshold prefix through Harmony against a counting health boundary. Cases cover default-on prevention, restoring the native threshold, all diagnostic levels with logging off in developer mode, verbose gating and the first report after logging is enabled. `MedicalGoodwillTests` source-links the production social outcome, verifies NPC and player treatment attribution and guards against direct game-log bypasses in MI source. The pinned game reference has no method bodies; native death rules, serialization and the full mod patch chain still require in-game acceptance.

In-game acceptance remains necessary: expand and collapse every group, scroll after collapsing a group near the bottom, hover each control and menu item, select each NPC mode and supply preset, and reopen settings after restarting the game with customized values. Check a narrow settings window and a loaded map's repair action. Do not invoke the repair action solely to inspect its tooltip.
