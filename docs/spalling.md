# Spalling defaults and Combat Extended integration

code-synced: 2026-10-19

## Current implementation

Status: `implemented`. `EnableSpalling` now defaults to false. The existing generated setting name and persisted value are retained, so explicitly stored enabled/disabled values remain effective. The serializer omits values equal to the old default: a missing key therefore follows the new false default, even if an older installation previously ran with the unstored true default. No migration can distinguish an omitted default from a choice that was never serialized; users can enable the setting again. Enabling the setting still runs the existing `SpallingInjuryWorker`; armor thresholds, chance settings, fragment injuries and identifiers are unchanged. See the [player guide](wiki/injuries/spalling-injury.md).

## Combat Extended work

Status: `deferred` for proper CE armor integration. The current implementation uses torso-covering apparel, ordinary sharp-armor comparisons, armor condition and impact/damage heuristics, plus a low-damage CE cutoff. It does not consume CE's actual penetration, deflection or per-part armor resolution. This remains an approximation, not a verified implementation of CE armor mechanics.

A future integration should evaluate CE's actual resolved armor/penetration outcome, preserve its damage handling, define how fragment injuries coexist with CE secondary damage, and validate representative armor/projectile combinations. That work requires a separate design and is not part of the emergency-care patch. No new CE spalling hook, dependency or balance change is introduced here.

## Validation

The Release build and focused checks pass. Tests verify the false default under the unchanged setting name and that the runtime worker still obeys the saved setting. Existing preferences are not forcibly rewritten. Package XML/layout/hash validation passes. In-game acceptance should confirm the displayed default, an existing enabled preference and normal enabled/disabled behavior. No game was launched.
