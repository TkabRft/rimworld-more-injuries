
# Gangrene

<!-- @generate_breadcrumb_trail {"template": "_:file_folder: {0}_", "connector": " :arrow_right: "} -->
_:file_folder: [More Injuries User Manual](/docs/wiki/README.md) :arrow_right: [Injuries and Medical Conditions A-Z](/docs/wiki/injuries/README.md) :arrow_right: [Gangrene](/docs/wiki/injuries/gangrene.md)_
<!-- @end_generated_block -->

Gangrene is a type of tissue death caused by a lack of blood supply. Symptoms may include a change in skin color to red or black, numbness, swelling, pain, skin breakdown, and coolness. Depending on the presence of infection, gangrene may be classified as dry or wet.

## Dry Gangrene

> **In-Game Description**
> _"**Gangrene (dry)** &mdash; This tissue is irreversibly damaged. Amputate the affected part; untreated dry gangrene can become infected wet gangrene."_

```mermaid
---
config:
  flowchart:
    htmlLabels: true
---
flowchart LR
  tourniquet[prolonged tourniquet application] ==> dry_gangrene["gangrene (dry)"]
  dry_gangrene ==> | infection develops | wet_gangrene["gangrene (wet)"]
  dry_gangrene ==> | autoamputation | amputation[amputation]

  linkStyle 0,1,2 stroke: #b10000
  style dry_gangrene stroke-width: 4px
```

*See the section on the [pathophysiological system](/docs/wiki/pathophysiological-system.md#pathophysiological-system) for more information on the graphical representation.*

**Causes**: A lack of blood supply to a body part, often due to a [tourniquet](/docs/wiki/medical-devices.md#tourniquet) that has been applied for too long or other conditions that restrict blood flow.

**Effects**: The affected body part will become dry, shrunken, and dark reddish-black, and may eventually fall off if not removed surgically. Dry gangrene may progress to [wet gangrene](#wet-gangrene) if an infection develops in the dead tissues.

**Treatment**: The affected body part is irreversibly damaged and should be amputated to prevent life-threatening infection.

## Wet Gangrene

> **In-Game Description**
> _"**Gangrene (wet)** &mdash; Infected dead tissue threatens the patient with fatal sepsis. Amputate the affected part urgently."_

**Causes**: A lack of blood supply to a body part that caused the tissue to die and become infected with bacteria, often due to a [tourniquet](/docs/wiki/medical-devices.md#tourniquet) that has been applied for too long or due to [dry gangrene](#dry-gangrene) that has become infected.

**Effects**: The affected body part will become saturated with stagnant blood, promoting the rapid growth of bacteria. The toxic products formed by bacteria are absorbed, causing systemic manifestation of sepsis and eventually death.

```mermaid
---
config:
  flowchart:
    htmlLabels: true
---
flowchart LR
  tourniquet[prolonged tourniquet application] ==> | infection develops | wet_gangrene["gangrene (wet)"]
  dry_gangrene["gangrene (dry)"] ==> | infection develops | wet_gangrene
  wet_gangrene ==> | severity = 1 | death[death]

  linkStyle 0,1,2 stroke: #b10000
  style wet_gangrene stroke-width: 4px
```

*See the section on the [pathophysiological system](/docs/wiki/pathophysiological-system.md#pathophysiological-system) for more information on the graphical representation.*

**Treatment**: The affected body part should be amputated as soon as possible to save the patient's life.

<!-- @generate_link_to_top {"template": "---\n_[back to the top]({1})_"} -->
---
_[back to the top](#gangrene)_
<!-- @end_generated_block -->
