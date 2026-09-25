# Concussion and trauma stroke

Status: `added, not verified`. code-synced: 2026-09-26.

Owner for the shared head-trauma path that applies `Concussion` and `HemorrhagicStroke`. Player pages: `docs/wiki/injuries/concussion.md`, `docs/wiki/injuries/hemorrhagic-stroke.md`.

## Runtime

`HeadInjuryWorker` is an `IPostPostApplyDamageHandler`. It uses `dinfo.HitPart` and `dinfo.Amount`. It does not use coverage-weighted `PostTakeDamage`. `MoreInjuryComp` is unchanged, so off-map hits are skipped when `Map` is null.

`IsEnabled` is true when either `EnableConcussion` or `EnableHemorrhagicStroke` is on and def `HeadTraumaProperties` loaded.

No brain (`GetBrain()` null): skip. Hediffs stay on the brain. Reuse an existing hediff of that def if present; otherwise create it on the brain. Do not create a second copy.

## Trauma score

```
trauma = dinfo.Amount * damageTypePercent * partMultiplier * (10 / part.hitPoints)
```

If `part.hitPoints <= 0`, HP scale is `1`. Reference hit points are `10`. Trauma is not clamped to `1` before the threshold divide.

Each enabled outcome:

```
added = clamp01(trauma / threshold) * chance
```

Skip that outcome when `added < 0.01`. Otherwise add `added` and cap severity at `1`. No `Rand.Chance`.

Settings keys and defaults: `ConcussionThreshold` `6`, `ConcussionChance` `0.75`, `HemorrhagicStrokeThreshold` `15`, `HemorrhagicStrokeChance` `0.25`. A hit at or above a threshold adds exactly that outcome's chance. The same hit can apply both.

## HeadTraumaProperties

defName `HeadTraumaProperties`. Type `MoreInjuries.HealthConditions.HeadInjury.HeadTraumaPropertiesDef`. Looked up with `DefDatabase.GetNamed`. Missing def: config error, worker disabled. Extra defs are ignored.

Lookups are built once in `ResolveReferences`. Duplicate `bodyPart` or `damageDefName` rows: last wins.

Patch xpaths:

```
Defs/MoreInjuries.HealthConditions.HeadInjury.HeadTraumaPropertiesDef[defName="HeadTraumaProperties"]/partMultipliers
Defs/MoreInjuries.HealthConditions.HeadInjury.HeadTraumaPropertiesDef[defName="HeadTraumaProperties"]/damageTypePercents
Defs/MoreInjuries.HealthConditions.HeadInjury.HeadTraumaPropertiesDef[defName="HeadTraumaProperties"]/headRoots
Defs/MoreInjuries.HealthConditions.HeadInjury.HeadTraumaPropertiesDef[defName="HeadTraumaProperties"]/headGroups
```

### Head-part matching

A part is a head part if:

1. its def is in `partMultipliers`, or
2. it or an ancestor def is in `headRoots`, or
3. it or an ancestor has a group in `headGroups`.

Mapped multiplier wins. Otherwise `defaultPartMultiplier` (`1.0`). Multiplier `0` is the patch escape hatch: the part still matches, but trauma is `0`.

Shipped `headRoots`: `Head`, `Skull`. Shipped `headGroups`: `FullHead`. A patched `partMultipliers` row is enough without `FullHead`.

Shipped human multipliers: `Brain` `3.0`, `Skull` `1.5`, `Head` `0.5`, `Ear` / `Eye` / `Jaw` / `Nose` `1.5`. Unlisted head descendants use `1.0`.

### Damage types

`damageTypePercents` store `damageDefName` strings, not `DamageDef` refs. Unknown names use `defaultDamageTypePercent` `0.08`. A listed percent of `0` disables that type.

## Stroke causes outside this worker

Unchanged: hydrostatic shock (`HydrostaticShockWorker`), adrenaline overdose, and coagulopathy.
