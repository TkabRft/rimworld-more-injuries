# Adrenaline Rush

<!-- @generate_breadcrumb_trail {"template": "_:file_folder: {0}_", "connector": " :arrow_right: "} -->
_:file_folder: [More Injuries User Manual](/docs/wiki/README.md) :arrow_right: [Injuries and Medical Conditions A-Z](/docs/wiki/injuries/README.md) :arrow_right: [Adrenaline Rush](/docs/wiki/injuries/adrenaline-rush.md)_
<!-- @end_generated_block -->

Adrenaline, also known as epinephrine, is a naturally occurring hormone that is released in response to stress or danger. It increases heart rate, blood pressure, and energy levels, preparing the body for a fight-or-flight response. In the game, an adrenaline rush can occur naturally in pawns as a result of combat and injury, or it can artificially be induced through the use of an [epinephrine injection](/docs/wiki/medical-devices.md#epinephrine-autoinjector).

> **In-Game Description**
> _"**Adrenaline rush** &mdash; Temporarily boosts movement and reduces pain. Slows shock and shock-driven acidosis while active, but does not replace lost blood. Excessive adrenaline can cause heart attack or stroke."_

```mermaid
---
config:
  flowchart:
    htmlLabels: true
---
flowchart LR
  painful_injury[painful injury] ==> adrenaline_rush[adrenaline rush]
  epinephrine_injection[Epinephrine injection] ==> adrenaline_rush
  adrenaline_rush ==> cardiac_arrest[cardiac arrest]
  adrenaline_rush ==> |overdose| cardiac_arrest
  adrenaline_rush ==> |overdose| hemorrhagic_stroke[hemorrhagic stroke]

  linkStyle 2 stroke: #549b68, stroke-dasharray: 9,5
  linkStyle 0,1,3,4 stroke: #b10000
  style adrenaline_rush stroke-width: 4px
```

*See the section on the [pathophysiological system](/docs/wiki/pathophysiological-system.md#pathophysiological-system) for more information on the graphical representation.*

**Causes**: Injuries or [epinephrine injections](/docs/wiki/medical-devices.md#epinephrine-autoinjector).

**Effects**: At lower levels, an adrenaline rush can provide a temporary boost to consciousness, moving, and pain tolerance. At higher levels, it can cause a reduction in manipulation and sight. In extreme cases of overdose, it can lead to anxiety, panic, nausea, as well as, coma, [cardiac arrest](/docs/wiki/injuries/cardiac-arrest.md#cardiac-arrest), and subsequent death. Overdose can also cause a [hemorrhagic stroke](/docs/wiki/injuries/hemorrhagic-stroke.md#hemorrhagic-stroke) while that setting is on. Turning the stroke setting off does not stop the other overdose effects. As adrenaline raises the heart rate and blood pressure, it can also reduce the effects of [hypovolemic shock](/docs/wiki/injuries/hypovolemic-shock.md#hypovolemic-shock) for a short period of time.  
In a similar way, epinephrine injections may be used to assist with [defibrillation](/docs/wiki/medical-devices.md#defibrillator) efforts and [CPR](/docs/wiki/medical-devices.md#cpr) in cases of [cardiac arrest](/docs/wiki/injuries/cardiac-arrest.md#cardiac-arrest). Drafted first aid injects epinephrine only while adrenaline mechanics are on; the [epinephrine injection](/docs/wiki/medical-devices.md#epinephrine-autoinjector), its recipe, and direct use stay available.

During [hypovolemic shock](/docs/wiki/injuries/hypovolemic-shock.md#hypovolemic-shock), adrenaline slows worsening and shock-driven acidosis buildup while active. It does not lower the blood-loss-dependent shock target. It also assists shock recovery after the volume deficit improves. One epinephrine autoinjector adds 75% adrenaline severity, which normally wears off over about three game hours; repeated doses retain their existing overdose risk.

**Treatment**: Adrenaline is naturally metabolized by the body over time and effects last between a few minutes to a few hours in severe cases. In cases of overdose, the pawn may require medical treatment to treat symptoms and secondary effects.

**Inspecting effects**: The linked adrenaline rush information card lists each stage, its severity threshold, its numerical modifiers, and the configured recovery rate.

<!-- @generate_link_to_top {"template": "---\n_[back to the top]({1})_"} -->
---
_[back to the top](#adrenaline-rush)_
<!-- @end_generated_block -->
