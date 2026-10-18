# Medical expansion follow-up

code-synced: 2026-10-18.

The [vital failure module](vital-failure.md), [neurological framework](neurological-runtime.md) and [Death Rattle transition](death-rattle-transition.md) own implemented behavior.

| Work | Status | Current behavior or remaining work |
|---|---|---|
| Fleshy animal vital failure | `implemented` | Anatomy-based organ failure and scaled cerebral injury; eligible emergency treatment access. |
| Full animal injury/trauma framework | `deferred` | Species-appropriate injury workers, hemorrhagic shock/acidosis, transfusion balance, surgical coverage and population-scale game profiling. Ordinary animal hemorrhage retains its existing policy. |
| Death Rattle save migration/removal | `implemented` | Standard conditions and supported bills migrate with MI priority, one-time instructions and completed-save validation. Unknown layouts/components/references can block handoff and need targeted adapters. |
| Elective induced coma | `implemented` | Industrial/glitterworld medicine, half cerebral production, prolonged-coma risk and native Wake-up reversal. |
| Mechanical physiology | `deferred` | Separate machine capacities, failures and repair treatment; no blood/oxygen assumptions. |
| Ventilation and dialysis | `deferred` | Equipment, research, consumables, power, interruption and balance. |

Expansion points are deliberately separate: vital eligibility and mutable physiology, cached body-tag anatomy, configurable neurological profiles/condition metadata, emergency patient selection and optional migration adapters. Add species capabilities through these boundaries rather than injecting the complete human injury pipeline into every pawn.
