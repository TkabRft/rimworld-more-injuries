# Medical expansion follow-up

code-synced: 2026-10-18

The [vital failure module](vital-failure.md) implements the current emergency physiology scope. These items are deliberately deferred; they are not claims that the implemented module is missing.

| Work | Status | Required follow-up |
|---|---|---|
| Ordinary animals | `deferred` | Design eligibility, animal anatomy, treatment availability, balance, and performance before expanding the current humanlike/HAR/Anomaly scope. |
| Death Rattle save migration | `deferred` | Inventory active source conditions, brain damage, coma, settings, references and recipes; map them without silently dropping severity or disability. No migration exists in this release. |
| Safe Death Rattle removal pipeline | `deferred` | Provide a staged, validated transition with backups, an explicit readiness report, and post-removal checks. Do not advise removing Death Rattle from an existing affected save as though this were supported. |
| Elective induced coma | `deferred` | Design deliberate induction, ongoing care, duration, reversal, and compatibility with the existing drug system. Pathological coma already exists. |
| Ventilation and dialysis | `deferred` | Design equipment, research, jobs, consumables/power, support interruption, and balance. Current rescue uses existing medical tools and organ replacement. |

The present replacement requires separate mod loadouts. Combined operation with Death Rattle is intentionally unsupported; its package is listed as incompatible. New module condition identifiers must remain stable for future migration work.
