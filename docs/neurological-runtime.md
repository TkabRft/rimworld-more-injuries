# Neurological framework

Status: `implemented`. code-synced: 2026-10-18.

Source: BrainDamage neurological profile/config, registry and source comps. XML profiles configure weighted outcomes and stable damage/cause mappings; zero weight excludes an outcome. Existing named disabilities remain, with diffuse damage added at -5/15/30/50/75 brain-efficiency points, retaining impairment and 10% consciousness at severity 1.

Direct brain injury has chance 0.25 × clamp01(damage / maximum part health). Concussion gets one 5% roll when crossing 70% per episode, excluding a duplicate roll for a direct brain hit. Systemic complication intervals, severity ranges, settings and separate personality gating remain. Animals receive diffuse/motor outcomes. Source profile and episode state use neurologicalSourceProfile and severeConcussionOutcomeRolled.

Registry-based regeneration runs in the controller's existing scan/schedule, replacing duplicate recovery in the old worker. Regeneration 100 clears full lasting severity in four days after the associated injury/scar is gone. Native transformation cleanup applies; prior skill redistribution is not reversed. There are no new per-tick lists, history buffers or global scanners. Indexed profile selection avoids warmed allocations.

The player [brain damage guide](wiki/injuries/brain-damage.md) owns symptoms and treatment. Clinical injury scaling remains owned by [vital failure](vital-failure.md). Compilation and the existing focused suite are checked at this topic boundary; full new production-linked scenarios are added with transition validation.
