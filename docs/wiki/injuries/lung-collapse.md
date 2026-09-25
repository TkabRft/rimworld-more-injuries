# Lung Collapse

Status: `implemented`. code-synced: 2026-09-26.

<!-- @generate_breadcrumb_trail {"template": "_:file_folder: {0}_", "connector": " :arrow_right: "} -->
_:file_folder: [More Injuries User Manual](/docs/wiki/README.md) :arrow_right: [Injuries and Medical Conditions A-Z](/docs/wiki/injuries/README.md) :arrow_right: [Lung Collapse](/docs/wiki/injuries/lung-collapse.md)_
<!-- @end_generated_block -->

When a creature is exposed to a sudden change in pressure, such as caused by *thermobaric weapons and other high-explosive devices*, the lung tissue may rupture, causing air to leak into the chest cavity and compress the lung, leading to a life-threatening condition known as a lung collapse. The same mechanism can also be caused by *perforating injuries to the chest*, creating a direct passage from the external environment into the pleural space through the chest wall.

> **In-Game Description**
> _"**Lung collapse** &mdash; A buildup of air inside the chest cavity creates pressure against the lung, impairing normal breathing. The collapse stays at the severity it began at. It does not worsen on its own, but further injury to that lung can make it worse.  
> Below 45% severity, a decent tend lets the body reabsorb the trapped air over several days, unless that lung is still bleeding. At or above 45% severity, surrounding structures shift and blood flow to the heart is impaired. This is a tension pneumothorax. Tending will not repair it, and it must be surgically repaired. If left untreated, it can lead to cardiac arrest and death."_

```mermaid
---
config:
  flowchart:
    htmlLabels: true
---
flowchart LR
  external_factors[external factors] ==> lung_collapse[lung collapse]
  lung_collapse ==> | obstructive shock | cardiac_arrest[cardiac arrest]

  linkStyle 0,1 stroke: #b10000
  style lung_collapse stroke-width: 4px
```

*See the section on the [pathophysiological system](/docs/wiki/pathophysiological-system.md#pathophysiological-system) for more information on the graphical representation.*

**Causes**: Perforating injuries to the chest wall or exposure to a sudden change in pressure, such as caused by thermobaric weapons and other high-explosive devices.

**Effects**: Impaired breathing and chest pain at the severity the collapse began at. It does not worsen on its own. Further injury to that lung can raise the severity. At or above 45% severity it becomes a tension pneumothorax, which can lead to obstructive shock, [cardiac arrest](/docs/wiki/injuries/cardiac-arrest.md#cardiac-arrest), and death if not surgically treated. Cardiac arrest can still occur over time while the collapse stays at a high severity.

**Treatment**: Below 45% severity, tend the collapse with decent medicine. The body reabsorbs the trapped air over several days while the tend holds, unless that lung is still bleeding. Re-tend as needed until it resolves. [Thoracotomy](/docs/wiki/surgeries.md#thoracotomy) or [video-assisted thoracoscopic surgery](/docs/wiki/surgeries.md#video-assisted-thoracoscopic-surgery) can still be used, but is not required. At or above 45% severity, tending does not repair it. Surgery is required. Resuscitate the patient if they go into [cardiac arrest](/docs/wiki/injuries/cardiac-arrest.md#cardiac-arrest).

<!-- @generate_link_to_top {"template": "---\n_[back to the top]({1})_"} -->
---
_[back to the top](#lung-collapse)_
<!-- @end_generated_block -->
