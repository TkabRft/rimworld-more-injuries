# Medical Devices and Procedures

<!-- @generate_breadcrumb_trail {"template": "_:file_folder: {0}_", "connector": " :arrow_right: "} -->
_:file_folder: [More Injuries User Manual](/docs/wiki/README.md) :arrow_right: [Medical Devices and Procedures](/docs/wiki/medical-devices.md)_
<!-- @end_generated_block -->

In order to effectively treat the new injuries and medical conditions introduced by More Injuries, several new medical devices and treatment options have been added to the game.

NPC combat medicine is enabled by default in the mod options. Combatants, armed visitors, defenders and reinforcements can bring medical supplies and use them on themselves or nearby patients. Prisoners can also preserve their own health, including during an existing breakout; this does not start a break, open their confinement or grant access through locked doors. Colonists and colony slaves keep their usual player-controlled behavior.

The default **Treatment-aware tactics** mode lets NPCs seek nearby safer treatment positions and carry endangered casualties to nearby ground for care. **Medical actions only** still permits ordinary travel to patients and supplies, but adds no tactical repositioning, casualty extraction, withdrawal or evacuation. **Full survival** also allows individual medical withdrawal and off-map evacuation of nearby downed faction members; it does not order the whole group to retreat. Fighters look for patients within 12 cells and medics within 24. Life-threatening emergencies come first, followed by ties favoring their own group, allies/player colonists, then neutrals. Other factions receive emergency assistance rather than routine upkeep. Current hostility matters, so breakout opponents are not treated as friends.

In every mode, eligible helpers can give available safe pain relief to a pawn downed by pain. Higher-priority emergencies still come first, and pain relief cannot guarantee recovery from other causes of collapse. In Full survival, feasible care is considered before off-map evacuation. Nearby enemies, recent hits, active danger or a route that cannot be assessed as safe defer that evacuation. This restriction does not prevent an injured pawn from withdrawing individually under fire. Local extraction in Treatment-aware tactics and Full survival remains a separate way to move a casualty out of danger before treatment.

In **Full survival**, ordinary assault and siege raiders also consider their downed faction members before stealing loot or leaving. They can look beyond the usual nearby-patient range, provided the approach is within their medical travel limit. Available safe care comes first; otherwise a capable survivor can carry a casualty along a checked safe exit route. Several survivors can help different patients. A survivor carrying loot puts it down only after a feasible rescue is found; equipment and inventory are kept.

Rescue effort is bounded. Survivors may briefly wait for danger to settle or another helper to finish, but only from a position assessed as safe. If rescue stalls, remains unsafe or exceeds its limit, they can leave; a patient already being carried may finish a safe journey. Panic fleeing, emergency withdrawal, existing pawn transport, quests and player orders retain control. **Medical actions only** and **Treatment-aware tactics** keep their existing departure behavior. Visitors, traders, caravans and other departure types are outside this rescue-before-loot behavior.

Helpers weigh a quick application against moving the casualty to cover and waiting. Partial cover can be enough for worthwhile brief aid, but immediate threats, known explosives, fire and unsafe drugs still prevent it. After a local extraction, a capable carrier reassesses treatment; another nearby capable helper may provide it when the carrier cannot. A short round permits one effective pain-relief dose and one bleeding-control application, with critical bleeding taking precedence when necessary. Helpers reassess after each application rather than assuming the patient can stand.

A pawn downed by pain does not need 80% consciousness to receive morphine. An eligible dose may reduce consciousness if the pawn remains awake and the projected pain relief can help; other injuries may still prevent recovery. Active pawns receive a different assessment because sedation can reduce their existing ability to fight. Blood loss, shock, breathing, circulation, combinations and overdose remain separate safety checks. Drug effects and potency are unchanged.

Supplies may come from the helper, the patient, eligible nearby allies or reachable ground items. After moving a casualty, helpers can also check near the original position for supplies left behind. This works with retained inventories, dropped inventories and partial retention, including Non uno Pinata configurations. Forbidden, inaccessible or unavailable items are not automatically made usable.

NPC medical work yields to a known nearby explosive so ordinary danger avoidance can take over. This does not guarantee escape from every grenade. Lowering the mode or disabling the module prevents new pickups that require the previous mode; a patient already being carried can finish a safe transport, or be preserved through normal interruption cleanup.

NPCs treating each other, including giving blood or saline transfusions, do not earn goodwill for your colony. Medical goodwill changes apply when a member of your player faction provides the treatment; goodwill already awarded is not reversed.

Open **NPC combat medicine** in the mod options to expand equipment, care and drug settings. Own-faction, friendly, neutral and **Treat player colonists** assistance all default on; colonists' medical-care restrictions still apply. Supply, medic and performance-drug prevalence can each vary from 0–2×, scavenging from 0–10%, and optional training can raise newly generated medics' Medical skill. Training is off by default. Existing pawns immediately use available supplies, but loading a save or toggling options never gives them free equipment. Disabling stops new decisions and provisioning while a current application or carrying operation may finish.

New groups receive technology-appropriate kits: early factions mostly carry bandages and herbal medicine; Industrial and later factions can add hemostats, tourniquets, industrial medicine, fluids and epinephrine. Medics carry more supplies, splints and, at higher tiers, resuscitation equipment. Selection favors medically capable pawns with room for the core kit, then their existing Medical skills. If nobody can carry the full core, the best partial carrier is selected. Higher-point groups are better supplied, subject to available carrying capacity. Normal personal kits contain no glitterworld medicine: it is normally reserved for Ultra medics, with a small scavenging chance for others. Each fluid selection has a 75% saline / 25% blood chance; blood uses hemogen when active Biotech and the blood integration setting select it. Capacity can reduce the final kit: core lifesaving treatments and devices come first, followed by one clinical dose, remaining essential consumables, additional clinical reserves, then boosters and sedatives. Weapons, armor and ammunition are kept.

Medic selection does not give a pawn a visible medic title or automatically improve Medical skill. At default settings, a 2,000-point Empire group selects about 13% of eligible combatants as medics, rounded to the nearest pawn: a group of 1-3 can have none, 4-11 requests one, and 12-19 requests two. There must be enough pawns capable of doctoring. The optional training setting improves newly generated medics' skill.

NPCs wait for a patient's current treatment to finish rather than starting competing self-care or another tending job. Field tending can treat a standing NPC without a bed. The doctor takes one medicine dose directly from an accessible inventory, keeps the rest packed, and uses normal tending effects. When the job ends, unused medicine still being carried returns to inventory if capacity permits; it is not automatically reclaimed after being dropped on the ground.

NPCs abandon medical travel that is still unfinished after 2,500 ticks, and safer-position moves are limited to 600 ticks. Medical application time still depends on the doctor's abilities. NPC device care, including splinting, reassesses after each application instead of collecting supplies for a longer course. Missing supplies, failed pickups and loss of the doctor's required capabilities stop the affected job. Treatment cleanup leaves replacement orders alone; a rejected performance drug is not retried indefinitely. Evacuation requires the pawn to be able to carry the patient and to have free carrying hands.

NPCs recognize choking on blood from its early stages, even when the patient has no open bleeding or wounds that ordinary tending can treat. A capable pawn with an accessible suction device can clear its own airway or help a nearby patient; CPR requires another capable pawn. Blood loss remains relevant after bleeding stops, and dangerous hemodilution also receives attention. Safe transfusions can be self-administered when supplies and the pawn's abilities permit them. Shock, heart attacks and other life-threatening conditions receive priority, although conditions requiring surgery still need surgery.

Once nearby threats are gone, NPCs can gradually remove a tourniquet after its wound has been treated and bleeding is controlled. They keep it in place when an untreated wound could bleed again as temporary suppression wears off.

Automatic care does not repeat CPR for cardiac arrest caused by a pumping failure that CPR cannot reverse. A separate choking obstruction can still receive airway care. A nonfunctional heart needs repair or replacement; manual CPR orders remain available.

MI includes a compatibility fix for an inventory-tab crash in Nice Inventory Tab CE Integration. This fix stays active even when NPC combat medicine is disabled.

Performance drugs default to technology-scaled choices, one dose per engagement, a 20,000-tick minimum interval, and use by 100% of eligible carriers. Settings also offer **Prefer cheap morphine** and **Existing provisioning only**, a 0–100% use chance, 0–3 doses per engagement and a 0–60,000-tick interval. An engagement ends after 2,500 ticks without a relevant threat; the minimum interval still applies afterward. Morphine is a cheaper loadout alternative to yayo or go-juice, with different effects: it can suppress pain but also causes sedation, addiction and respiratory risks. NPCs do not treat it as an interchangeable stimulant and avoid automatic use when their physiology is unsafe. Existing drugs are retained, and normal drug effects, tolerance and genes still apply.

Newly equipped Industrial, Spacer and Ultra pawns have separate clinical reserves and a chance of booster supplies. These are baseline inventory targets before the supply multiplier and the increase for higher-point groups:

| Technology | Ordinary pain-relief reserve | Medic pain-relief reserve | Booster reserve, when selected |
|---|---:|---:|---:|
| Industrial | 1 | 2 | 1 |
| Spacer | 1 | 4 | 1 |
| Ultra | 2 | 6 | 2 |

Baseline clinical reserves use morphine. The default booster choices shift from mostly yayo toward go-juice as technology rises; **Prefer cheap morphine** supplies morphine for that separate booster reserve. Existing supplies count toward their targets, and an existing booster does not cancel a medic's clinical reserve. Carrying more doses does not increase the number NPCs may take per engagement. No separate pawn-quality ranking is added.

With **Det?s Battle Meds** enabled, early personal kits may carry one herbal powder; Neolithic and Medieval medics start with one and two respectively. Advanced medics replace half their scaled clinical reserve with industrial stimpacks, or glitter stimpacks at Ultra technology. A one-dose advanced personal reserve has an equal chance of morphine or its tier's stimpack; the baseline Ultra personal reserve has one of each. The MBH shot receives a 20% share of advanced booster selections. Capacity can reduce every grant.

NPCs use Det?s powder and stimpacks when one dose offers effective pain relief, reduced active bleeding, or improved natural healing of an existing healable wound. Wound treatment remains useful after tending and without pain. Repeating an equivalent active effect is skipped; emergency procedures remain first. A capable NPC can take its own dose, and a nearby doctor can help another patient. MBH is a combat booster and does not replace clinical epinephrine or resuscitation. Det?s original effects and overdose risks remain in force. This integration adds no new player administration commands.

**Clinical pain care** is enabled by default. NPCs can use available morphine, yayo, go-juice or supported Det?s Battle Meds analgesics before pain incapacitates them, and a capable nearby helper can assist a pawn downed by pain even without bleeding or tendable wounds. **Preventive pain trigger** defaults to 50% of each patient's own pain-shock threshold and is adjustable from 0–100%. Pain immunity needs no treatment. Pain collapse is an emergency under the existing friendly, neutral and player-care settings; other causes of collapse still need their own care, and pain relief may not get a seriously injured pawn standing. Existing life-threatening emergencies come first.

Clinical pain care checks whether one more dose will actually reduce pain. Different active drugs can multiply their pain relief, but repeating the same drug without improving its current analgesic effect is skipped. There is no pain-dose timer: existing effects and safety determine when another dose is useful. NPCs reject unsafe sedation, overdose exposure and depressant combinations. Morphine remains a cheaper loadout option with distinct addiction, breathing and circulation risks; significant blood loss, shock or already depressed vital functions can make it unsuitable even when it is available. Carrying a painkiller does not guarantee that taking it is safe.

The pawn doing the action must be willing: a drug-averse actor refuses self-use and administration to others. A willing helper can treat a drug-averse recipient, who retains normal mood and drug consequences. Medical-care restrictions still apply, with the existing prisoner self-care exception. Self-dosing does not require doctoring. A dose already in the pawn's pocket can be used during combat; collecting supplies and helping someone else still require suitable access and exposure. Supplies are searched within 12 cells, and unused selected drugs are kept when carrying capacity permits. Turning pain care off cancels consumption of a pending clinical pain dose. Colonists keep their normal player-directed behavior.

Clinical analgesia does not spend combat-boost allowance or the emergency epinephrine interval. Clinical epinephrine for shock or resuscitation retains its separate repeat interval, default 600 ticks and adjustable from 0–2,500. Taking a performance drug does not spend this clinical allowance; drug-specific safety limits still apply. Disabling combat boosters preserves clinical supplies. Disabling clinical pain care prevents new pain-only reserves, while Det?s wound treatments remain available for healing or bleeding. **Existing provisioning only** prevents all new drug grants, including scavenging; NPCs can still safely use existing supplies. Low-tier medics may carry chloroform and higher-tier medics ketamine, but NPCs do not automatically administer either on the battlefield. Surgery-only problems still require surgery. With compatible optional mods, MI coordinates medical decisions automatically; the lower modes retain external retreat/rescue behavior.

In **Treatment-aware tactics** and **Full survival**, nearby NPCs can carry downed patients out of danger before ordinary treatment. Any physically capable combatant can help carry; only a medically capable pawn can treat afterward. The search stays local: destinations are within 12 cells of the patient, with a short movement budget. Beds are not required, and local extraction does not mean leaving the map. Medical actions only keeps its current movement behavior.

Rescuers consider gunfire, recent explosions, fire and harmful temperatures for both themselves and their patient. Essential brief stabilization can come first when transport would delay lifesaving care. At a safer position, a capable carrier gets the first opportunity to treat; otherwise nearby medical pawns can help. Existing faction-care settings, hostility, medical restrictions and physical access still apply. Prisoners do not gain escape opportunities.

When proven shelter is unavailable, an NPC can attempt a less certain, least-dangerous relocation. Such attempts are limited to prevent repeated carrying between similar positions. An already best available location does not cause movement merely for its own sake. Reaching uncertain shelter does not make unsafe treatment permissible. New danger can prompt reassessment.

Combat Extended cover/hunkering and Celsius thermal shelter can interrupt carrying while keeping the selected patient attached and allowing native job resumption when the route remains valid. Incapacity, explicit orders and unrelated job changes can still abort transport safely. Turning the module off allows a current safe carry to finish. This behavior does not alter player-controlled colonist AI, inventory quantities or drug effects. Combined in-game acceptance, including RimWorld 1.7, remains pending.

NPCs give incoming fire time to settle before ordinary medical trips. Ranged or explosive injuries to the helper or patient, including mortar hits from far away, start a 600-tick quiet period. Safe self-care from pocket supplies and immediately adjacent lifesaving care can still be possible. With Combat Extended, taking cover and hunkering take priority in every AI mode. Normal movement and suppression reactions remain available.

A treatment attempt keeps its selected patient and supply through one application. If retrieval or treatment repeatedly fails without improvement, that treatment pauses for increasingly longer retries, up to 2,500 ticks. Switching between equivalent medicine stacks on the floor does not restart the timer. Meaningful changes, such as a moved patient or a newly available pocket supply, allow reconsideration. Other nearby patients and independent treatments can still receive care. Unused selected supplies, including devices, return to the helper's inventory when space permits; excess items may still be dropped normally. Successful pain relief keeps its effect-based redosing rules.

Smarter Raider AI keeps its combat and avoidance behavior. Its inspected version has no identified direct medical cancellation path; combined in-game testing is still needed.

Testing this NPC pain behavior in game with vanilla and Combat Extended is still pending, including RimWorld 1.7.

<!-- @generate_toc {"source": "$self", "indent": 2} -->
- [Medical Devices and Procedures](#medical-devices-and-procedures)
  - [Airway Suction Device](#airway-suction-device)
  - [Bandage](#bandage)
  - [Blood Bag](#blood-bag)
  - [Chloroform-Soaked Cloth](#chloroform-soaked-cloth)
  - [CPR](#cpr)
  - [Defibrillator](#defibrillator)
  - [Epinephrine Autoinjector](#epinephrine-autoinjector)
  - [First Aid](#first-aid)
  - [Hemostatic Agent](#hemostatic-agent)
  - [Ketamine Autoinjector](#ketamine-autoinjector)
  - [Morphine Autoinjector](#morphine-autoinjector)
  - [Saline IV Bag](#saline-iv-bag)
  - [Splint](#splint)
  - [Thoracoscope](#thoracoscope)
  - [Tourniquet](#tourniquet)
  - [Artificial Coma](#artificial-coma)
  - [Death Estimate](#death-estimate)
<!-- @end_generated_block -->

## Airway Suction Device

<p>
  <img align="right" src="/Textures/Thing/ASD.png" height="96" alt="Airway Suction Device" />
  
> **In-Game Description**
> _"**Airway suction device** &mdash; Clears blood from a choking patient's airway. Success improves with the user's Medical skill."_

</p>

**Used for**: Clearing the airway of patients who are [choking on blood](/docs/wiki/injuries/choking.md#choking-on-blood) or other fluids to prevent asphyxiation. The airway suction device is a hand-operated device that uses a vacuum to remove fluids from the airway, allowing the patient to breathe freely and restoring normal respiration. It can be reused multiple times and is more effective than [CPR](#cpr) in clearing the airway of obstructions.

**Usage**:

1. Tell a *drafted* pawn capable of doctoring to use the airway suction device on a patient using the `Clear airways` option in the right-click context menu of the patient. The airway suction device must be accessible in a stockpile or the doctor's inventory. Self-treatment is not possible. The device will not be consumed during the treatment and can be reused multiple times.
2. Clearing the airway with an airway suction device is part of the [First Aid](#first-aid) order for *drafted* pawns.

A successful attempt clears the entire blood obstruction. At default settings, success is 25% at Medicine 0-2, 50% at Medicine 4, 75% at Medicine 6, and guaranteed at Medicine 8 or higher. One attempt takes about 10 real seconds at normal speed with 100% manipulation and medical tend speed, excluding travel. Both routine airway care and drafted first aid prefer a carried device, then search nearby within 12 cells of the patient. If the bounded search finds none, they use CPR. Manual orders can fetch a more distant device.

**Production**: At a machining table.  
**Production Skill Requirement**: `Crafting` at level 4  
**Research Requirements**: [Emergency medicine](/docs/wiki/research.md#emergency-medicine)  
**Ingredients**: 25x `Steel`, 20x `Chemfuel`, 1x `Component`, 5x `Plasteel`  
**Success Chance**: `<medicine skill>  / 8`, but at least the configured minimum success chance in the mod settings.  
**Mass**: 0.4 kg  
**Bulk (Combat Extended)**: 1.5

## Bandage

<p>
  <img align="right" src="/Textures/Thing/Bandage/bandage_b.png" height="64" alt="Bandage" />
  <img align="right" src="/Textures/Thing/Bandage/bandage_a.png" height="64" alt="Bandage" />

> **In-Game Description**
> _"**Bandage** &mdash; Temporarily reduces wound bleeding. Slower and less effective than a hemostatic agent; follow with proper treatment."_

</p>

**Used for**: Temporarily reducing the bleed rate of wounds to allow the patient to be stabilized by a doctor. Bandages are a simple and effective way to slow the progression of blood loss and prevent the patient from going into [hypovolemic shock](/docs/wiki/injuries/hypovolemic-shock.md#hypovolemic-shock) until more advanced medical treatment can be provided. This type of elastic bandage allows to reduce the bleed rate of the wound by up to 80% and lasts approximately 12 hours until fully soaked with blood. The reduction is strongest when the bandage is applied and weakens in small steps every 250 ticks, about 4.2 real seconds at normal speed, reaching no extra reduction when the timer ends. The remaining time is saved with the wound. Bandages are a good choice for treating minor to moderate bleeding wounds, but may not be as effective as [hemostatic agents](#hemostatic-agent) in stopping severe bleeding.

One application takes about 1.33 real seconds at normal speed with 100% manipulation and medical tend speed, excluding travel. Recipes, material costs and the 12-hour fading effect are unchanged.

**Usage**:

1. Tell a *drafted* pawn capable of doctoring to apply a bandage to a wound using one of the `Stabilize with bandages` options in the right-click context menu of the patient. The bandage must be accessible in a stockpile or the doctor's inventory. Self-treatment is possible.
2. Applying a bandage is part of the [First Aid](#first-aid) order for *drafted* pawns.

**Production**: At a crafting spot or tailor bench.  
**Production Skill Requirement**: None  
**Research Requirements**: [Basic anatomy](/docs/wiki/research.md#basic-anatomy)  
**Ingredients**: 1x `any textile`\
**Base market value**: $2 each\
**Crafting work**: 50 per bandage; 250 per five\
**Success Chance**: 100%  
**Mass**: 0.05 kg  
**Bulk (Combat Extended)**: 0.05

## Blood Bag

Blood bags are one of the two options for fluid resuscitation in patients suffering from severe blood loss and [hypovolemic shock](/docs/wiki/injuries/hypovolemic-shock.md#hypovolemic-shock), the other one being [saline IV infusions](#saline-iv-bag). While saline IV bags are generally easier to produce and store, blood bags provide the advantage of restoring actual blood components, making them the only safe option for treating patients with advanced [coagulopathy](/docs/wiki/injuries/coagulopathy.md#coagulopathy) or [acidosis](/docs/wiki/injuries/acidosis.md#acidosis).

<p>
  <img align="right" src="/Textures/Thing/BloodBag/blood_bag_b.png" height="64" alt="Blood Bag" />
  <img align="right" src="/Textures/Thing/BloodBag/blood_bag_a.png" height="64" alt="Blood Bag" />

> **In-Game Description**
> _"**Blood bag** &mdash; 750 ml transfusion that reduces blood loss by 35 percentage points and reduces hemodilution. Refrigerate or freeze for storage; collect more through blood donation."_

</p>

**Used for**: Restoring blood volume in patients suffering from severe blood loss and [hypovolemic shock](/docs/wiki/injuries/hypovolemic-shock.md#hypovolemic-shock). Restoring blood volume is essential in severe cases of [hypovolemic shock](/docs/wiki/injuries/hypovolemic-shock.md#hypovolemic-shock). It is recommended to keep a ready supply of blood bags in your hospital supply room for emergency situations. You may even want to keep a few blood bags in the inventory of your combat medics to perform immediate blood transfusions on the battlefield.   Shock now subsides gradually as the blood-volume deficit improves; a transfusion does not instantly remove existing hypoxic injuries.
Blood bags are also the only way to stabilize pawns with severe [hemodilution](/docs/wiki/injuries/hemodilution.md#hemodilution) or patients with advanced [acidosis](/docs/wiki/injuries/acidosis.md#acidosis) and [coagulopathy](/docs/wiki/injuries/coagulopathy.md#coagulopathy) to restore their blood volume and allow them to recover. Blood bags can be stored in a refrigerator or freezer to keep them fresh for longer periods of time.

With Dub's Bad Hygiene active, this infusion also restores thirst.

**Usage**:

1. Tell a *drafted* pawn capable of doctoring to transfuse blood from a blood bag to a patient using one of the `Transfuse blood` options in the right-click context menu of the patient. The blood bag must be accessible in a stockpile or the doctor's inventory. Self-treatment is not possible. Depending on the selected options (`stabilize` or `fully heal`), the patient's blood loss will be reduced by a certain amount, or the patient will be fully healed. Each blood bag is capable of restoring 35% blood volume.
2. Transfusing blood (`stabilize`) from a blood bag is part of the [First Aid](#first-aid) order for *drafted* pawns.
3. Alternatively, restoring blood volume to a stable condition is part of a new general, high-priority work type for all doctors. So, if you have a patient in a hospital bed with severe blood loss, any non-drafted doctor will automatically attempt to restore blood volume using a blood bag, in accordance with the configured work priorities.

> [!TIP]
> Performing a life-saving blood transfusion on members of other factions can help improve faction relations.

**Production**: 

1. Colonists, visitors, or prisoners can donate blood by scheduling the `Extract blood bag` procedure on the *operations* tab of the selected pawn. Colonists will donate blood willingly and know that they are helping to save lives of their fellow colonists. Prisoners and guests, on the other hand, may require a little more "convincing" to donate blood - at the cost of faction relations.
2. Downed enemies can be executed through the `Harvest blood (finish off)` option in the right-click context menu to extract blood bags from them (requires a medically skilled, *drafted* pawn capable of doctoring). This is a quick and effective way to obtain blood bags from enemies on the battlefield, but is considered a war crime and will have severe consequences for your faction relations.
3. Blood bags can be automatically and safely extracted from prisoners using the `blood bag farm` prisoner interaction option. It works analogous to the `hemogen farm` option from the Biotech DLC and allows to automatically schedule the extraction of blood bags from prisoners at regular intervals when it is safe to do so and does not pose a risk to the prisoner's health. It will not schedule a second extraction of the same procedure while one is already waiting. It is still considered an unethical and offensive practice and will have consequences for your faction relations. With Biotech, blood bag farm and hemogen farm cannot both stay enabled on the same prisoner.

**Research Requirements**: [Basic first aid](/docs/wiki/research.md#basic-first-aid)  
**Ingredients**: N/A  
**Success Chance**: 100%  
**Mass**: 0.75 kg  
**Bulk (Combat Extended)**: 1.5

Blood bags retain their 35-percentage-point blood-loss reduction. Their effect on existing hemodilution depends on the starting state: from 80% blood loss and 40% dilution, one bag leaves 45% blood loss and about 31% dilution. Restoring volume can stop shock-driven acid production before the shock entry has fully subsided.

## Chloroform-Soaked Cloth

<p>
  <img align="right" src="/Textures/Thing/Chloroform/chloroform_b.png" height="64" alt="Chloroform" />
  <img align="right" src="/Textures/Thing/Chloroform/chloroform_a.png" height="64" alt="Chloroform" />

> **In-Game Description**
> _"**Chloroform-soaked cloth** &mdash; Sedates a patient. Difficult to dose safely; excessive use risks organ damage, respiratory failure and cardiac arrest."_

</p>

**Used for**: Administering [chloroform](/docs/wiki/injuries/chloroform-buildup.md#chloroform-buildup) to induce sedation in patients and render them unconscious. While used extensively in the past, modern medicine has largely replaced chloroform with safer alternatives due to its difficulty in controlling dosage and high potential for side effects, such as [cardiac arrest](/docs/wiki/injuries/cardiac-arrest.md#cardiac-arrest), [hepatic and renal damage](/docs/wiki/injuries/chemical-damage.md#chemical-damage), and respiratory depression.

**Usage**:

1. Tell a *drafted* pawn capable of doctoring to use the chloroform-soaked cloth on a patient using the `Sedate (chloroform)` option in the right-click context menu. The chloroform must be accessible in a stockpile or the doctor's inventory. Self-treatment is possible.
2. Alternatively, since chloroform is considered a drug, it can be administered as part of the normal medical treatment using the *operations* tab of the patient or by telling the pawn to injest the drug directly by right-clicking on the drug and selecting `Inhale chloroform`.

**Production**: At a drug lab.  
**Production Skill Requirement**: `Crafting` at level 4, `Intellectual` at level 2  
**Research Requirements**: [Chloroform synthesis](/docs/wiki/research.md#chloroform-synthesis)  
**Ingredients**: 4x `Smokeleaf leaves`, 1x `Chemfuel`  
**Success Chance**: 100%  
**Mass**: 0.05 kg  
**Bulk (Combat Extended)**: 0.05

> [!TIP]
> Anesthetics offer a wide range of possibilities in RimWorld gameplay. From ensuring hostile pawns stay unconscious while rendering first aid before capturing them, safely "calming down" colonists on violent mental breaks, or even for self-sedation and "playing dead" as a last resort in combat when escape is not an option. Feel free to experiment with the new possibilites. Beware though that sedating someone without medical necessity is considered a hostile action.

## CPR

Resuscitation and airway-care options apply to living patients. A corpse retains its normal corpse interactions.

Cardiopulmonary resuscitation (CPR) is an emergency procedure that combines chest compressions with artificial ventilation to manually preserve brain function until further measures can be taken to restore spontaneous blood circulation and breathing in a person who is in [cardiac arrest](/docs/wiki/injuries/cardiac-arrest.md#cardiac-arrest), suffering from a [heart attack](https://rimworldwiki.com/wiki/Ailments#Heart_attack), or is [choking on blood](/docs/wiki/injuries/choking.md#choking-on-blood).

> **In-Game Description**
> _"**CPR** &mdash; Perform CPR to stabilize patients with cardiac or respiratory arrest."_

**Used for**: Stabilizing patients suffering from [cardiac arrest](/docs/wiki/injuries/cardiac-arrest.md#cardiac-arrest) (during `ventricular fibrillation` and `clinical death` stages), [heart attacks](https://rimworldwiki.com/wiki/Ailments#Heart_attack), and [choking on blood](/docs/wiki/injuries/choking.md#choking-on-blood).

**Usage**

1. Tell a *drafted* pawn capable of doctoring to perform CPR on a pawn suffering from [cardiac arrest](/docs/wiki/injuries/cardiac-arrest.md#cardiac-arrest) (during `ventricular fibrillation` and `clinical death` stages), [heart attack](https://rimworldwiki.com/wiki/Ailments#Heart_attack), or [choking on blood](/docs/wiki/injuries/choking.md#choking-on-blood) using the `Perform CPR` option in the right-click context menu. Self-treatment is not possible.
2. CPR is part of the [First Aid](#first-aid) order for *drafted* pawns.
3. Alternatively, resuscitating patients using CPR is now part of a new general, high-priority work type for all doctors. So, if you have a patient in a hospital bed with a condition that requires CPR, any non-drafted doctor will automatically attempt to resuscitate the patient using CPR or a [defibrillator](#defibrillator), in accordance with the configured work priorities. Automatic doctor CPR, airway management, and the other treatment work givers do not run when the patient's medical care is set to no medical care. No medicine still allows CPR, because CPR does not use medicine.

**Research Requirements**: [Cardiopulmonary Resuscitation (CPR)](/docs/wiki/research.md#cardiopulmonary-resuscitation-cpr)  
**Base skill curve**: $f_{\text{sigmoid}}($ `medicine skill> / 15` $)$ where $f_{\text{sigmoid}}$ is a diffused sigmoid function defined as $f_{\text{sigmoid}}(x) = \frac{1}{1 + e^{-10\cdot(x - 0.5)}} + z$ where $z$ is a random factor between $-0.1$ and $0.1$. See [Wolfram Alpha](https://www.wolframalpha.com/input?i=f%28x%29+%3D+1+%2F+%281+%2B+e%5E%28-10*%28x%2F15+-+0.5%29%29%29+%2B+z%2C+x+in+%5B0%2C+20%5D%2C+z+in%5B-0.1%2C0.1%5D) for a visualization of the function.

For blood choking, only **35% of the base curve above** is applied. Medicine 5 removes about 2-9 severity points per attempt, Medicine 10 removes 26-33, and Medicine 15 removes 31-38. Attempts take about six real seconds at normal speed and 100% manipulation/tend speed. Suction is the faster full-clearance option; CPR remains available without a device. Very poor attempts can worsen obstruction. Neither procedure treats the bleeding wound. This reduction does not affect cardiac resuscitation.

> [!IMPORTANT]
> Note that CPR needs to be unlocked through research before it can be used. Make sure to prioritize researching [Cardiopulmonary Resuscitation (CPR)](/docs/wiki/research.md#cardiopulmonary-resuscitation-cpr) as soon as possible to avoid losing valuable pawns to treatable conditions like [cardiac arrest](/docs/wiki/injuries/cardiac-arrest.md#cardiac-arrest), [heart attacks](https://rimworldwiki.com/wiki/Ailments#Heart_attack), or [respiratory arrest](/docs/wiki/injuries/choking.md#choking-on-blood).

> [!CAUTION]
> Depending on the randomized factor $z$, poor medical skill of the doctor may do more harm than good when performing CPR. It is recommended to have a decently skilled doctor perform CPR to ensure the best possible outcome.

> [!TIP]
> Administering [epinephrine](#epinephrine-autoinjector) to patients suffering from [cardiac arrest](/docs/wiki/injuries/cardiac-arrest.md#cardiac-arrest) can help to increase the chances of successful resuscitation and improve the patient's condition.

## Defibrillator

Defibrillation is a treatment for life-threatening conditions that affect the rhythm of the heart, such as [heart attacks](https://rimworldwiki.com/wiki/Ailments#Heart_attack) and the `ventricular fibrillation` stage of [cardiac arrest](/docs/wiki/injuries/cardiac-arrest.md#cardiac-arrest). It involves delivering a high-energy electric shock to the heart with a reusable device called a defibrillator to restore normal heart rhythm. It is a more specialized and faster treatment than [CPR](#cpr) in these cases, however, it requires a defibrillator to be available and accessible to the treating doctor.


<p>
  <img align="right" src="/Textures/Thing/Defibrillator/defibrillator_b.png" height="64" alt="Defibrillator" />
  <img align="right" src="/Textures/Thing/Defibrillator/defibrillator_a.png" height="64" alt="Defibrillator" />

> **In-Game Description**
> _"**Defibrillator** &mdash; Attempts to restart the heart during a heart attack or early cardiac arrest. Later arrest requires CPR; a nonfunctional heart requires repair or replacement. Reusable, but may break after use."_

</p>

**Used for**: Treating [heart attacks](https://rimworldwiki.com/wiki/Ailments#Heart_attack) and [cardiac arrest](/docs/wiki/injuries/cardiac-arrest.md#cardiac-arrest) during the `ventricular fibrillation` stage.

**Usage**

1. Tell a *drafted* pawn capable of doctoring to use the defibrillator on a pawn suffering from a [heart attack](https://rimworldwiki.com/wiki/Ailments#Heart_attack) or [`ventricular fibrillation`](/docs/wiki/injuries/cardiac-arrest.md#cardiac-arrest) using the `Defibrillate` option in the right-click context menu. The defibrillator must be accessible in a stockpile or the doctor's inventory. Self-treatment is not possible. In case of the `clinical death` stage of [cardiac arrest](/docs/wiki/injuries/cardiac-arrest.md#cardiac-arrest), only [CPR](#cpr) can save the patient.
2. Defibrillation is part of the [First Aid](#first-aid) order for *drafted* pawns.
3. Alternatively, resuscitating patients using a defibrillator is now part of a new general, high-priority work type for all doctors. So, if you have a patient in a hospital bed with a heart attack or ventricular fibrillation, any non-drafted doctor will automatically attempt to resuscitate the patient using a defibrillator or [CPR](#cpr), in accordance with the configured work priorities.

**Production**: At a machining table.  
**Production Skill Requirement**: `Crafting` at level 6  
**Research Requirements**: [Emergency medicine](/docs/wiki/research.md#emergency-medicine)  
**Ingredients**: 25x `Steel`, 20x `Chemfuel`, 4x `Component`, 25x `Plasteel`, 10x `Gold`, 100x `Silver`  
**Success Chance**: `<medicine skill>  / 8`, but at least the configured minimum success chance in the mod settings.  
**Chance to Break[^1]**: 10%  
**Mass**: 0.5 kg  
**Bulk (Combat Extended)**: 2

> [!TIP]
> Administering [epinephrine](#epinephrine-autoinjector) to patients suffering from [cardiac arrest](/docs/wiki/injuries/cardiac-arrest.md#cardiac-arrest) can help to increase the chances of successful resuscitation and improve the patient's condition.

## Epinephrine Autoinjector

<p>
  <img align="right" src="/Textures/Thing/Epinephrine/injector_yellow_b.png" height="64" alt="Epinephrine" />
  <img align="right" src="/Textures/Thing/Epinephrine/injector_yellow_a.png" height="64" alt="Epinephrine" />
  
> **In-Game Description**
> _"**Epinephrine autoinjector** &mdash; Injects adrenaline to temporarily boost movement, reduce pain and support shock treatment. Does not replace lost blood; excessive doses risk heart attack or stroke."_
  
</p>

**Used for**: Administering a large dose of [epinephrine (adrenaline)](/docs/wiki/injuries/adrenaline-rush.md#adrenaline-rush) to increase heart rate and blood pressure of the patient, which can help to slow the progression of [hypovolemic shock](/docs/wiki/injuries/hypovolemic-shock.md#hypovolemic-shock) for a short period of time until the patient can be fully stabilized using blood transfusion.  
When used in controlled doses, epinephrine can help with resuscitation efforts by improving blood flow to vital organs and increasing the chances of successful defibrillation in cases of [cardiac arrest](/docs/wiki/injuries/cardiac-arrest.md#cardiac-arrest).  
Alternatively, the pain relief from the adrenaline rush can help to wake up a patient who has lost consciousness due to severe pain or other conditions. There have also been reports of epinephrine being used as a combat stimulant to increase the combat effectiveness of soldiers in the field, however, there is a risk of [adrenaline overdose](/docs/wiki/injuries/adrenaline-rush.md#adrenaline-rush) if used excessively.

**Usage**:

1. Tell a *drafted* pawn capable of doctoring to use the epinephrine autoinjector on a patient using the `Inject epinephrine` option in the right-click context menu. The epinephrine autoinjector must be accessible in a stockpile or the doctor's inventory. Self-treatment is possible.
2. Administration of epinephrine during [First Aid](#first-aid) happens only while adrenaline mechanics are on. The injector can still be used from the right-click menu, crafted, and taken directly.
3. Alternatively, as epinephrine is considered a drug, it can be administered as part of the normal medical treatment using the *operations* tab of the patient or by telling the pawn to injest the drug directly by right-clicking on the drug and selecting `Use epinephrine autoinjector`.

**Production**: At a drug lab.  
**Production Skill Requirement**: `Crafting` at level 6, `Intellectual` at level 4  
**Research Requirements**: [Epinephrine synthesis](/docs/wiki/research.md#epinephrine-synthesis)  
**Ingredients**: 1x `Chemfuel`, 1x `Yayo`, 1x `Flake`  
**Success Chance**: 100%  
**Mass**: 0.05 kg  
**Bulk (Combat Extended)**: 0.05

## First Aid

First aid is the initial assistance or treatment given to a person who is injured or suddenly ill before full medical treatment can be provided. With More Injuries, `Provide first aid` is a new command that can be given to *drafted* pawns capable of doctoring to attempt to automatically stabilize patients with life-threatening conditions. It is an aggregated work type that includes application of [tourniquets](#tourniquet), controlling bleeding using [bandages](#bandage) or [hemostatic agents](#hemostatic-agent), the use of [CPR](#cpr), [defibrillation](#defibrillator), [airway suction](#airway-suction-device), fluid resuscitation with [saline IVs](#saline-iv-bag) and [blood bags](#blood-bag), and other emergency treatments.

> **In-Game Description**
> _"**Provide first aid** &mdash; Evaluating treatment options for &lt;patient name&gt;."_

**Used for**: Automatically stabilizing and treating patients with life-threatening conditions.

**Usage**

Tell a *drafted* pawn capable of doctoring to provide first aid to a patient using the `Provide first aid` option in the right-click context menu. The pawn compares feasible emergency treatments using the patient's current condition, treatment benefit, application time, retrieval effort, and supply cost. The options below are not a fixed sequence. Self-treatment is not possible.

1. If the patient is suffering from severe bleeding on the limbs, the doctor can apply a [tourniquet](#tourniquet) to the affected limb(s) to stop the bleeding. The tourniquets are applied in the order of severity and must be in the doctor's inventory. Otherwise, this step will be skipped.
2. The doctor can reduce external bleeding with [hemostatic agents](#hemostatic-agent) or [bandages](#bandage), balancing effectiveness against retrieval time. A hemostatic a few tiles away can beat a pocket bandage for a serious wound, while a distant hemostatic may lose to that bandage. The heaviest eligible bleeding wound is considered first; minor wounds favor bandages, with hemostatics available as a fallback. With Combat Extended, medicine-based stabilization is the bleeding-control fallback after feasible MI bleeding actions run out. A CE batch is not started solely to further suppress wounds already controlled by MI equipment.
3. The doctor prioritizes treating cardiac arrest through [defibrillation](#defibrillator) or [CPR](#cpr), if necessary. Depending on the current [adrenaline level](/docs/wiki/injuries/adrenaline-rush.md#adrenaline-rush) of the patient, the doctor may also administer an [epinephrine autoinjector](#epinephrine-autoinjector) to increase chances for successful defibrillation. That injection happens only while adrenaline mechanics are on. With the setting off, first aid still defibrillates or performs CPR.
4. An [airway suction device](#airway-suction-device) can clear blood choking; its benefit is weighed against application and retrieval time.
5. When a [defibrillator](#defibrillator) or [airway suction device](#airway-suction-device) is unavailable or impractical to retrieve, [CPR](#cpr) provides an equipment-free alternative for arrest or blood choking.
6. The doctor will determine if fluid resuscitation is necessary to restore blood volume in cases of severe blood loss and [hypovolemic shock](/docs/wiki/injuries/hypovolemic-shock.md#hypovolemic-shock). If so, they will prefer performing a [saline IV infusion](#saline-iv-bag) to restore blood volume, as it is easier to produce and store. If a saline IV bag is not available or would not be safe to perform due to [hemodilution](/docs/wiki/injuries/hemodilution.md#hemodilution) or pre-existing [coagulopathy](/docs/wiki/injuries/coagulopathy.md#coagulopathy) and [acidosis](/docs/wiki/injuries/acidosis.md#acidosis), the doctor will instead use a [blood bag](#blood-bag) to restore blood volume. If neither is available, the doctor will skip this step.
7. When no feasible emergency action remains, the doctor tends remaining treatable wounds on the ground, including those of a downed enemy you ordered them to help. No bed or capture is required. The doctor uses permitted medicine from their inventory, the patient's inventory or reachable nearby ground supplies, and reconsiders emergencies between tending applications. Standing patients skip this ordinary-tending step.
8. When bleeding has stopped, no higher-priority emergency action is feasible, and affected bleeding wounds are tended, permanent or gone, the doctor can safely remove previously applied [tourniquets](#tourniquet) from the patient to restore blood flow to the affected limb and prevent [gangrene](/docs/wiki/injuries/gangrene.md#gangrene) from developing.

After each step, the doctor re-evaluates the patient before continuing. Temporarily controlled bleeding does not by itself end tending on a downed patient with treatable wounds. Care restrictions, lost access or capability, recovery, death and cancellation can still stop the order. Unused selected medicine returns to inventory when space permits; unrelated carried items are kept. If no permitted medicine is available, tending can proceed without it unless medical care is disabled. First Aid does not automatically choose painkillers or combat drugs; give those orders separately. NPC medicine modes do not restrict your drafted colonist's First Aid order.

During arrest or blood choking with breathing at 20% or less, one immediately available tourniquet or hemostatic application taking at most 120 estimated ticks may precede resuscitation. Another requires an intervening resuscitation attempt. Retrieval of a defibrillator or suction device is allowed only within a 120-tick extra-travel budget when CPR is available; otherwise CPR starts.

> [!NOTE]
> Automatic saline treatment checks the current patient's condition, taking into account factors such as [coagulopathy](/docs/wiki/injuries/coagulopathy.md#coagulopathy) and [acidosis](/docs/wiki/injuries/acidosis.md#acidosis), which could lead to severe complications with [hemodilution](/docs/wiki/injuries/hemodilution.md#hemodilution) caused by saline infusions. You can customize the risk assessment process by adjusting the safety threshold (`Saline IV infusion safety threshold of independent coagulopathy`) in the mod settings, where values close to 0 will be highly restrictive, and values close to 1 will allow doctors to perform saline IV infusions, even in cases with high risk of complications.

> [!CAUTION]
> Treatment selection weighs current severity, benefit and practical costs, but changing health or travel conditions can make another choice preferable. Keep an eye on treatment progress and intervene manually when necessary.

Routine emergency doctor work shares the same weighted emergency choices for each selected patient. Ordinary tending remains separate native Doctor work. An unavailable treatment does not block another feasible option. First Aid reassesses after individual MI applications; a CE stabilization batch retains CE's normal treatment and medicine consumption. Research, medical-care restrictions, access and reservations still apply. CE medicine can come from the doctor, the patient's inventory, carried medicine, or a bounded nearby search within CE's configured medicine radius. Ordinary wound-tending order and CE's manual Stabilize command are unchanged.

For patients with an active **Superclotting** gene, First Aid and routine MI emergency care ignore individual wounds bleeding below 0.1/day and never automatically apply tourniquets. Bandages receive a stronger preference than hemostatic agents, while availability and travel still matter. Any feasible treatment for another emergency takes precedence over bleeding control, including during arrest or critical choking; the quick bleeding-control exception does not apply. Your explicit First Aid order still tends remaining treatable wounds while these patients are downed. Existing tourniquets can still be removed once the usual safety checks pass. With Combat Extended, minor wounds alone do not trigger stabilization; a native batch triggered by a larger wound can still include them.

A temporarily bloodless wound is not necessarily closed. Automatic tourniquet cleanup waits while affected bleeding wounds remain untreated, even if CE stabilization currently reduces their rate to zero.

## Hemostatic Agent

<p>
  <img align="right" src="/Textures/Thing/Hemostat/hemostat_b.png" height="64" alt="Hemostatic Agent" />
  <img align="right" src="/Textures/Thing/Hemostat/hemostat_a.png" height="64" alt="Hemostatic Agent" />

> **In-Game Description**
> _"**Hemostatic agent** &mdash; Quickly reduces severe wound bleeding. Temporary first aid; follow with proper treatment."_

</p>

**Used for**: Temporarily reducing the bleed rate of wounds to allow the patient to be stabilized by a doctor. Hemostatic agents are a fast-acting and effective way to stop severe bleeding and prevent the patient from going into [hypovolemic shock](/docs/wiki/injuries/hypovolemic-shock.md#hypovolemic-shock) until more advanced medical treatment can be provided. This chemical compound is designed to promote blood clotting and reduce the flow of blood from wounds, allowing to reduce the bleed rate of the wound by up to 95% and lasts approximately 8 hours until its active ingredients are depleted. The reduction is strongest when the agent is applied and weakens in small steps every 250 ticks, about 4.2 real seconds at normal speed, reaching no extra reduction when the timer ends. The remaining time is saved with the wound. Hemostatic agents are an excellent choice for treating severe bleeding wounds, but may be overkill for minor to moderate bleeding.

One application takes about 0.67 real seconds at normal speed with 100% manipulation and medical tend speed, excluding travel. Recipes, material costs and the eight-hour fading effect are unchanged.

**Usage**:

1. Tell a *drafted* pawn capable of doctoring to apply a hemostatic agent to a wound using one of the `Stabilize with hemostatic agent` options in the right-click context menu of the patient. The hemostatic agent must be accessible in a stockpile or the doctor's inventory. Self-treatment is possible.
2. Applying a hemostatic agent is part of the [First Aid](#first-aid) order for *drafted* pawns.

**Production**: At a drug lab.  
**Production Skill Requirement**: `Crafting` at level 4  
**Research Requirements**: [Advanced first aid](/docs/wiki/research.md#advanced-first-aid)  
**Ingredients**: 10x `fabric`, 5x `Chemfuel` per batch of five\
**Base market value**: $6 each\
**Crafting work**: 750 per batch of five\
**Success Chance**: 100%  
**Mass**: 0.03 kg  
**Bulk (Combat Extended)**: 0.03

> [!TIP]
> Hemostatic agents are a powerful tool to stop severe bleeding in an emergency, so make sure you have a few hemostatic agents ready in your hospital or carried by your combat medics to quickly treat severe injuries on the battlefield.

## Ketamine Autoinjector

<p>
  <img align="right" src="/Textures/Thing/Ketamine/injector_blue_dark_b.png" height="64" alt="Ketamine" />
  <img align="right" src="/Textures/Thing/Ketamine/injector_blue_dark_a.png" height="64" alt="Ketamine" />

> **In-Game Description**
> _"**Ketamine autoinjector** &mdash; Autoinjector for pain relief and rapid sedation. Repeated doses can cause dangerous drug buildup and overdose."_

</p>

**Used for**: Administering [ketamine](/docs/wiki/injuries/ketamine-buildup.md#ketamine-buildup) to induce sedation in patients and render them unconscious. Contrary to [chloroform](#chloroform-soaked-cloth), ketamine is a safer alternative for inducing sedation, as it is quicker to administer and easier to control dosage due to autoinjector technology. Since the drug also has a much lower potential for side effects at therapeutic doses, it has been widely used in emergency surgery in field conditions in war zones to sedate patients who are in severe pain or resisting treatment.

**Usage**:

1. Tell a *drafted* pawn capable of doctoring to use the ketamine autoinjector on a patient using the `Sedate (ketamine)` option in the right-click context menu. The ketamine must be accessible in a stockpile or the doctor's inventory. Self-treatment is possible.
2. Alternatively, since ketamine is considered a drug, it can be administered as part of the normal medical treatment using the *operations* tab of the patient or by telling the pawn to ingest the drug directly by right-clicking on the drug and selecting `Use ketamine autoinjector`.

Doctor use and direct use apply the same drug effects, tolerance buildup, addiction checks, and overdose rules. Both can cause a ketamine addiction and satisfy an existing one. Open the autoinjector information card to inspect its drug and addiction statistics. See [ketamine buildup](/docs/wiki/injuries/ketamine-buildup.md#ketamine-buildup).

**Production**: At a drug lab.  
**Production Skill Requirement**: `Crafting` at level 6, `Intellectual` at level 5  
**Research Requirements**: [Ketamine synthesis](/docs/wiki/research.md#ketamine-synthesis)  
**Ingredients**: 1x `Yayo`, 4x `Smokeleaf leaves`, 1x `Chemfuel`  
**Success Chance**: 100%  
**Mass**: 0.05 kg  
**Bulk (Combat Extended)**: 0.05

> [!TIP]
> Anesthetics offer a wide range of possibilities in RimWorld gameplay. From ensuring hostile pawns stay unconscious while rendering first aid before capturing them, safely "calming down" colonists on violent mental breaks, or even for self-sedation and "playing dead" as a last resort in combat when escape is not an option. Feel free to experiment with the new possibilites. Beware though that sedating someone without medical necessity is considered a hostile action.

## Morphine Autoinjector

<p>
  <img align="right" src="/Textures/Thing/Morphine/injector_red_b.png" height="64" alt="Morphine" />
  <img align="right" src="/Textures/Thing/Morphine/injector_red_a.png" height="64" alt="Morphine" />

> **In-Game Description**
> _"**Morphine autoinjector** &mdash; Rapid pain relief. Suppresses breathing and circulation, making it dangerous during blood loss or resuscitation. Avoid combining with other depressants such as alcohol."_

</p>

**Used for**: Administering [morphine](/docs/wiki/injuries/morphine-high.md#morphine-high) to manage severe pain in patients. Morphine is a potent opioid analgesic that can provide rapid pain relief, but it also carries a risk of respiratory depression and [cardiac arrest](/docs/wiki/injuries/cardiac-arrest.md#cardiac-arrest) in case of overdose or when mixed with other central nervous system (CNS) depressants, such as alcohol. Morphine causes [vasodilation](/docs/wiki/injuries/vasodilation.md#vasodilation) (widening of blood vessels), which can lead to a drop in blood pressure, decreased cardiac output [(1)](https://www.accessdata.fda.gov/drugsatfda_docs/label/2011/202515s000lbl.pdf#:~:text=The%20vasodilation%20produced%20by%20Morphine,in%20patients%20in%20circulatory%20shock), reduced hemorrhagic resistance, and increased bleed rates ([Watso et al. (2022)](https://pubmed.ncbi.nlm.nih.gov/35452317/)). Furthermore, morphine can impede resuscitation efforts [(2)](https://wms.org/magazine/magazine/1372/TCCC-Pain%20Management/default.aspx) in patients with severe [blood loss](/docs/wiki/injuries/hypovolemic-shock.md#hypovolemic-shock) or [cardiac arrest](/docs/wiki/injuries/cardiac-arrest.md#cardiac-arrest). Therefore, it is best reserved for controlled environments, such as hospitals or after bleeding has been stabilized, to ensure the patient's safety and prevent complications.

**Usage**:

1. Tell a *drafted* pawn capable of doctoring to use the morphine autoinjector on a patient using the `Inject morphine` option in the right-click context menu. The morphine must be accessible in a stockpile or the doctor's inventory. Self-treatment is possible.
2. Alternatively, since morphine is considered a drug, it can be administered as part of the normal medical treatment using the *operations* tab of the patient or by telling the pawn to ingest the drug directly by right-clicking on the drug and selecting `Use morphine autoinjector`.

Doctor use and direct use apply the same drug effects, tolerance buildup, addiction checks, and overdose rules. Both can cause a morphine addiction and satisfy an existing one. Open the autoinjector information card to inspect its drug and addiction statistics. See [morphine high](/docs/wiki/injuries/morphine-high.md#morphine-high).

**Production**: At a drug lab.  
**Production Skill Requirement**: `Crafting` at level 5, `Intellectual` at level 3  
**Research Requirements**: [Morphine synthesis](/docs/wiki/research.md#morphine-synthesis)  
**Ingredients**: 1x `Flake`, 4x `Smokeleaf leaves`, 1x `Chemfuel`  
**Success Chance**: 100%  
**Mass**: 0.05 kg  
**Bulk (Combat Extended)**: 0.05

## Saline IV Bag

Saline IV bags are one of the two options for fluid resuscitation in patients suffering from severe blood loss and [hypovolemic shock](/docs/wiki/injuries/hypovolemic-shock.md#hypovolemic-shock), the other one being [blood bags](#blood-bag). Although saline IV bags are generally easier to produce and store than [blood bags](#blood-bag), they do not restore actual blood components, making them not a viable option for treating patients with advanced [coagulopathy](/docs/wiki/injuries/coagulopathy.md#coagulopathy) or [acidosis](/docs/wiki/injuries/acidosis.md#acidosis).

<p>
  <img align="right" src="/Textures/Thing/SalineBag/saline_bag_b.png" height="64" alt="Saline IV Bag" />
  <img align="right" src="/Textures/Thing/SalineBag/saline_bag_a.png" height="64" alt="Saline IV Bag" />

> **In-Game Description**
> _"**Saline IV bag** &mdash; 250 ml infusion that reduces blood loss by 15 percentage points but increases hemodilution. Excessive use risks coagulopathy and hypoxia. Automatic treatment obeys safety limits; forced single infusions bypass them."_

</p>

**Used for**: Restoring blood volume in patients suffering from minor to moderate blood loss or [hypovolemic shock](/docs/wiki/injuries/hypovolemic-shock.md#hypovolemic-shock). Saline infusions should generally not be used for fluid resuscitation in cases of severe blood loss or shock, as they do not contain red blood cells or clotting factors. The resulting [hemodilution](/docs/wiki/injuries/hemodilution.md#hemodilution) can lead to complications such as [coagulopathy](/docs/wiki/injuries/coagulopathy.md#coagulopathy) and further bleeding (see [lethal triad of trauma](/docs/wiki/concepts.md#lethal-triad-of-trauma)). Therefore, saline IV bags should only be used in small amounts or in conjunction with [blood bags](#blood-bag) or other blood products to ensure the patient's safety and prevent complications. The mathematical model for calculating the dilution effect of saline infusions is described in more detail in the [hemodilution simulation](/docs/wiki/concepts.md#hemodilution-simulation) section.  
If Dub's Bad Hygiene is loaded, each saline infusion will restore hydration by 30%.

**Usage**:

1. Tell a *drafted* pawn capable of doctoring to perform a saline infusion on a patient using one of the `Infuse saline IV` options in the right-click context menu of the patient. The saline IV bag must be accessible in a stockpile or the doctor's inventory. Self-treatment is not possible. Depending on the selected options (`stabilize`, `max safe dose`, or `single`), the patient's blood loss will be compensated by a certain amount of saline solution, up to a maximum of 15% blood volume per infusion. The `stabilize` option will attempt to restore the patient's blood volume to a stable condition, while the `max safe dose` option will infuse the maximum amount of saline that is considered safe for the patient, both of these taking into account their current health condition and risk factors. The `single` option will bypass all safety checks and infuse a single saline IV bag of 250ml, regardless of the patient's condition. Use this option at your own discretion.
2. Performing an infusion (`stabilize`) with a bag of saline solution is part of the [First Aid](#first-aid) order for *drafted* pawns.
3. Alternatively, restoring blood volume to a stable condition is part of a new general, high-priority work type for all doctors. So, if you have a patient in a hospital bed with severe blood loss, any non-drafted doctor will automatically attempt to restore blood volume using a saline IV bag, in accordance with the configured work priorities and safety threshold.

> [!NOTE]
> Automatic saline treatment checks the current patient's condition, taking into account factors such as [coagulopathy](/docs/wiki/injuries/coagulopathy.md#coagulopathy) and [acidosis](/docs/wiki/injuries/acidosis.md#acidosis), which could lead to severe complications with [hemodilution](/docs/wiki/injuries/hemodilution.md#hemodilution) caused by saline infusions. You can customize the risk assessment process by adjusting the safety threshold (`Saline IV infusion safety threshold of independent coagulopathy`) in the mod settings, where values close to 0 will be highly restrictive, and values close to 1 will allow doctors to perform saline IV infusions, even in cases with high risk of complications.

> [!TIP]
> Performing a life-saving saline IV infusion on members of other factions can help improve faction relations &mdash; but only when done within the safety thresholds.

**Production**: At a drug lab.  
**Research Requirements**: [Emergency medicine](/docs/wiki/research.md#emergency-medicine)   
**Ingredients**: 1x `any stone block`, (2x `water`, if Dub's Bad Hygiene is loaded)\
**Base market value**: $2 each\
**Crafting work**: 200 per bag; 1,000 per five\
**Success Chance**: 100%  
**Mass**: 0.25 kg  
**Bulk (Combat Extended)**: 0.5

> [!NOTE]
> When Dub's Bad Hygiene is loaded, saline IV bags require water bottles as an additional ingredient. You can create water bottles by right-clicking on a water source (e.g., sink) and selecting the option stockpile X amount of water bottles. Pawns will not automatically bottle water as part of production bill for saline IV bags.

Saline retains its 15-percentage-point blood-loss reduction per bag. From 80% blood loss with no dilution, one bag produces about 11.1% hemodilution. Automatic full treatment can safely give five bags in that starting state; a sixth would reach 45% dilution and cross the usual 44.9% safety limit. Coagulopathy from other causes can still block automatic saline. The forced single-infusion option continues to bypass those checks.

## Splint

<p>
  <img align="right" src="/Textures/Thing/Splint/splint_b.png" height="64" alt="Splint" />
  <img align="right" src="/Textures/Thing/Splint/splint_a.png" height="64" alt="Splint" />
  
> **In-Game Description** 
> _"**Splint** &mdash; Stabilizes a fracture so it can heal gradually."_
  
</p>

**Used for**: Treating [bone fractures](/docs/wiki/injuries/fractures.md#bone-fracture), allowing them to heal over time (see [healing bone fracture](/docs/wiki/injuries/fractures.md#healing-bone-fracture)).

**Usage**

1. Tell a *drafted* pawn capable of doctoring to apply a splint to a pawn with a bone fracture using the `Splint fractures` option in the right-click context menu. The splint must be accessible in a stockpile or the doctor's inventory. Due to its simplicity, the procedure can be performed anywhere and even in combat situations, to quickly restore partial stability to the injured limb and allow the pawn to move again. However, this treatment method comes at the cost of treatment quality and a slightly longer time for the fracture to heal.
2. Use the *operations* tab of the injured pawn to schedule the application of a splint to a bone fracture in a controlled environment, such as a hospital bed, to ensure the highest treatment quality and fastest healing time.
3. Alternatively, splinting fractures is now part of a new general, low-priority work type for all doctors. So, if you have a patient in a hospital bed with a bone fracture, any non-drafted doctor will automatically attempt to splint the fracture, in accordance with the configured work priorities. Note, that this option must first be enabled in the mod settings. Doctors will not attempt to splint fractures if [osteosynthetic surgery](/docs/wiki/surgeries.md#osteosynthetic-surgery) is scheduled for the same limb.

> [!TIP]
> Alternatively to splinting the fracture, the bone can be [surgically repaired](/docs/wiki/surgeries.md#osteosynthetic-surgery) to realign and stabilize the bone, allowing for a quicker recovery.

**Production**: At a crafting spot or machining table.  
**Production Skill Requirement**: None  
**Research Requirements**: [Basic anatomy](/docs/wiki/research.md#basic-anatomy)  
**Ingredients**: 4x `Wood` or `Steel`, 1x `any textile`  
**Success Chance**: 100%  
**Mass**: 0.1 kg  
**Bulk (Combat Extended)**: 0.1

## Thoracoscope

<p>
  <img align="right" src="/Textures/Thing/Thoracoscope/thoracoscope_b.png" height="64" alt="Tourniquet" />
  <img align="right" src="/Textures/Thing/Thoracoscope/thoracoscope_a.png" height="64" alt="Tourniquet" />

> **In-Game Description**
> _"**Thoracoscope** &mdash; Surgical equipment required for advanced collapsed-lung repair."_

</p>

**Used for**: Performing [video-assisted thoracoscopic surgery](/docs/wiki/surgeries.md#video-assisted-thoracoscopic-surgery) to treat [lung collapse](/docs/wiki/injuries/lung-collapse.md#lung-collapse) with minimally invasive surgical techniques and a high success rate to repair the damage and restore the lung to full function.

**Usage**: As a required ingredient for the [video-assisted thoracoscopic surgery](/docs/wiki/surgeries.md#video-assisted-thoracoscopic-surgery) procedure, which can be found in the *operations* tab of the selected pawn.

**Production**: At a machining table.  
**Production Skill Requirement**: `Crafting` at level 8  
**Research Requirements**: [Advanced thoracic surgery](/docs/wiki/research.md#advanced-thoracic-surgery)  
**Ingredients**: 20x `Steel`, 20x `Chemfuel`, 4x `Component`, 50x `Plasteel`, 20x `Gold`, 250x `Silver`  
**Chance to Break[^1]**: 20%  
**Mass**: 0.75 kg  
**Bulk (Combat Extended)**: 2.5

## Tourniquet

<p>
  <img align="right" src="/Textures/Thing/Tourniquet/tourniquet_b.png" height="64" alt="Tourniquet" />
  <img align="right" src="/Textures/Thing/Tourniquet/tourniquet_a.png" height="64" alt="Tourniquet" />

> **In-Game Description**
> _"**Tourniquet** &mdash; Stops bleeding from a limb. Prolonged use risks gangrene and limb loss; remove once proper treatment is available."_

</p>

**Used for**: Stopping severe bleeding in the corresponding limb to prevent extreme blood loss, [hypovolemic shock](/docs/wiki/injuries/hypovolemic-shock.md#hypovolemic-shock), and death. When applied to a limb, a tourniquet will restrict blood flow to the tissue below the tourniquet, which can severely reduce the bleed rate of injuries in that limb up to 95% until the tourniquet is removed. While a tourniquet can save a life in an emergency, prolonged use can cause severe damage to the limb in the form of ischemia (starvation of oxygen) and necrosis (cell death), which can lead to a life-threatening condition known as [gangrene](/docs/wiki/injuries/gangrene.md#gangrene). Therefore, a tourniquet should be removed as soon as possible after the bleeding has been stopped to prevent further complications. When removing a tourniquet, it is recommended to do so gradually and under controlled conditions to prevent systemic [acidosis](/docs/wiki/injuries/acidosis.md#acidosis) from occurring due to large amounts of stored lactic acid being released into the bloodstream too quickly.

**Usage**:

<ol>
<li> 

Tell a conscious pawn capable of doctoring to apply a tourniquet to a limb using the `Apply tourniquet to <limb>` option in the right-click context menu of the patient to apply a tourniquet to the corresponding limb. The tourniquet must be accessible in a stockpile or the doctor's inventory. Self-treatment is possible. Conversely, choose one the `Remove tourniquet from <limb>` options (`safely`, `quickly`) to remove a previously applied tourniquet and restore blood flow to the limb. Releasing the tourniquet safely will gradually restore blood flow to the limb, allowing aggregated metabolic waste to be cleared and reducing the risk of complications. This will, however, take some time, especially when the tourniquet has been in place for an extended period.

</li>
<li>
<img align="right" src="/Textures/UI/tourniquet_gizmo.png" height="64" alt="Tourniquet" /> 

Alternatively, use the *tourniquet gizmo* (depicted on the right) to apply or remove a tourniquet from the currently selected pawn. The gizmo can be found in the bottom of the screen when a pawn is selected and is capable of applying or removing a tourniquet from themselves. You can choose to hide the gizmo in the mod settings if your UI is already cluttered with other gizmos and you don't want to see it all the time. In that case, you will have to rely on the right-click context menu to apply or remove a tourniquet.

</li>
<li>

Applying a tourniquet is part of the [First Aid](#first-aid) order for *drafted* pawns, if the aggregated bleeding severity of the limb is above the threshold configured in the mod settings.

</li>
</ol>

> [!TIP]
> As long as the pawn is conscious, you can always apply or remove tourniquets via the *tourniquet gizmo*, even when self-tend is disabled.

**Production**: At a crafting spot or tailor bench.  
**Production Skill Requirement**: None  
**Research Requirements**: [Basic first aid](/docs/wiki/research.md#basic-first-aid)  
**Ingredients**: 1x `Wood` or `Steel`, 4x `any textile` or `leather`  
**Success Chance**: 100%  
**Mass**: 0.08 kg  
**Bulk (Combat Extended)**: 0.08

> [!TIP]
> Tourniquets are a powerful tool to stop severe bleeding in an emergency, so make sure you have a few tourniquets ready in your hospital or carried by your combat medics to quickly treat severe injuries on the battlefield.

> [!TIP]
> Should the patient decease during treatment, the tourniquet can be [recovered](/docs/wiki/work-types.md#tourniquet-recovery) from the corpse to be reused on another patient. This is automatically done as a new low-prority work type for all non-drafted doctors.

> [!WARNING]
> While generally easy to handle, severly incompetent doctors (medicine and intellectual skill below 3) may have a hard time applying a tourniquet correctly, potentially even having the not-so-bright idea to apply it to the neck. It goes without saying that such misuses of a tourniquet are not recommended and may lead to the patient [choking to death](/docs/wiki/injuries/choking.md#choking-on-tourniquet).

The delayed vital failure module gives an emergency window for [cardiac arrest](/docs/wiki/injuries/cardiac-arrest.md#cardiac-arrest) and [respiratory failure](/docs/wiki/injuries/respiratory-failure.md#respiratory-failure). CPR and early defibrillation can restart a functioning pump, while airway treatment addresses choking. Neither procedure replaces a destroyed heart or lungs. Existing organ-replacement operations remain the treatment for complete organ loss.

Transfusions improve the perfusion-related source of [cerebral hypoxia](/docs/wiki/injuries/hypoxia.md#cerebral-hypoxia); blood also treats dilution. Resuscitation does not erase brain injury already sustained. The [artificial-coma operation](/docs/wiki/injuries/artificial-coma.md#artificial-coma) can reduce new acute hypoxic brain injury while the cause is treated. Ventilators and dialysis remain future work.

## Artificial Coma

[Artificial coma](/docs/wiki/injuries/artificial-coma.md#artificial-coma) is a scheduled [operation](/docs/wiki/surgeries.md#artificial-coma), using five industrial or glitterworld medicine. It halves new acute hypoxic brain injury and limits consciousness to 10%. It does not replace CPR, airway treatment, fluids or organ replacement. One normal Wake-up dose reverses the induced state, with ordinary drug effects and restrictions.

Supported owned fleshy nonhuman patients can receive manual CPR, defibrillation and airway rescue without the full human trauma system. Automatic treatment and manual orders still respect medical-care settings, native patient restrictions, ownership, forbidden status and access. Supporting an entity's physiology does not authorize treatment of every hostile or contained entity.

## Death Estimate

**Use physiology-aware death estimate** is enabled by default in mod settings. The health-tab footer shows current bleeding when positive and an approximate **Estimated death** time. When temporary bleeding control is active, it compares **Bleeding controlled** (current bandages, tourniquets and stabilization keep reducing bleeding by the same amount) with **No more treatment** (nobody provides further care, so temporary control weakens and wears off). Other health problems continue progressing in both estimates. When bleeding stops, its rate disappears. The footer remains for a projected fatal route or untreated wounds under temporary control; harmless residual blood loss alone does not keep it visible. Full blood loss is still assessed through the patient's current physiology.

Chemical dependency alone does not show a death timer before chemical coma. Once the dependency enters its life-threatening coma stage, the forecast includes its progression. Other injuries or illnesses can still produce a timer before then. This display rule does not delay actual dependency progression or remove the need for regular doses.

A compact `>24h` means no fatal event was projected within the main one-day horizon, rather than a guarantee of safety. Longer supported disease or organ-failure estimates can still appear. `Uncertain` means temporary suppression prevented reliable capture of the untreated bleeding rate.

Hover over it for the estimate's assumptions, likely cause of death and a short rescue hint matched to the patient's current problems, with a concise limitation when relevant. The tooltip keeps its hover identity while the game runs, so updated estimates do not restart its display delay. For example, blood loss calls for bleeding control and transfusion, while arrest calls for CPR or early defibrillation when the heart can still function. A failed heart needs its function restored. These hints identify potential treatments; success still depends on timing, medical care, research, equipment and the doctor.

The estimate updates only while that pawn's Health tab is open. Closing it or selecting another tab or pawn stops forecast calculations for that patient. Reopening uses current health; actual injuries and treatment continue while the tab is closed.

With Combat Extended, **No more treatment** accounts for stabilization wearing off and bleeding returning, including wounds that currently bleed at zero. **Bleeding controlled** keeps the current reduction instead. CE's normal batch can also stabilize wounds already covered by MI supplies when another eligible wound triggers it; stacked temporary reductions do not close those wounds. Internal wounds and missing parts retain CE's different stabilization effects. Stabilization buys time; fully tend the wounds and treat any remaining shock or oxygen-delivery problem. The tooltip reminds you when temporary stabilization is part of the forecast. The **No more treatment** estimate also follows staged weakening of MI bandages and hemostatic agents. Choking follows the source wound: bleeding returning as stabilization fades can worsen the airway outlook, while a conscious patient may cough during complete suppression. Stopping the bleeding does not instantly clear an existing obstruction.

The forecast accounts for natural blood and dilution recovery, current tending and expiry, adrenaline decay, artificial coma, changing condition stages and restored breathing. Supported diseases compete with immunity; known temperature, toxicity, vacuum and starvation trends are included under current conditions. It uses mean progression where appropriate, not a guaranteed death timer. It predicts no future treatments, random complications, scripted deaths or organ restoration. Unmodeled risks are reported, and a lack of a projected fatal trend does not imply safety. Known death-protection expiry is considered; protection of unknown duration receives a status instead of an invented deadline.

Turning the setting off restores the vanilla blood-loss-only countdown, including its zero-time display at full blood loss. The setting affects presentation only; doctor priorities and actual health progression stay the same. A health-tab replacement that skips vanilla drawing retains its own display.

The cerebral rescue window at complete unprotected oxygen deprivation is now about 1.5 game hours. Act promptly: first aid or transfusion after the terminal stage cannot undo death, and restoring circulation does not erase [lasting brain injury](/docs/wiki/injuries/brain-damage.md#lasting-injury-milestones).

<!-- @generate_link_to_top {"template": "---\n_[back to the top]({1})_"} -->
---
_[back to the top](#medical-devices-and-procedures)_
<!-- @end_generated_block -->

[^1]: Some items have the potential to be reusable and they will not necessarily be consumed during a surgery or procedure. However, there is a chance that the item will break after each use, either due to wear and tear or due to the nature of the procedure. The chance to break is indicated by the **Chance to Break** value in the item's description.
