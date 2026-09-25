# More Injuries — feature router

Read this file first, then load only the owner docs for the current task. Do not preload `docs/wiki/`.

Package `Th3Fr3d.ExtendedInjuries`. Namespace `MoreInjuries`. Supported versions: 1.5 and 1.6. Required dependency: `brrainz.harmony`.

## Baseline

- Editable content is the mod root: `About/`, `Defs/`, `Patches/`, `Languages/`, `Textures/`, `Sounds/`, `Source/`.
- `LoadFolders.xml` also loads a `1.5/` or `1.6/` folder when present. Do not invent those folders.
- `oldversions/` and `steam/` are not source. `Source/MoreInjuries/MoreInjuries/_build/deploy.ps1` installs into the game `Mods/MoreInjuries` folder and deletes that folder first. Do not run it unless the user asks for an install.
- Game assembly ref: `Krafs.Rimworld.Ref` `1.6.4633` (Combat Extended main project pin, not the compat pin `1.6.4850`). Harmony `2.3.6` with `ExcludeAssets="runtime"` so `0Harmony.dll` is not copied to the mod output. Project target: `net48` on `MoreInjuries.csproj` and `MoreInjuries.Tests.csproj`. WikiGen and LocalizationTests are `net10.0`. The source generator stays `netstandard2.0`. Latest C#, nullable enabled.
- Modern C# used on that target is shimmed under `Source/MoreInjuries/MoreInjuries/Roslyn`. `net48` can see APIs the game's Mono profile does not have. Do not add `net48`-only calls. Do not bump `TargetFramework` further to make a language feature compile.
- Other workspace mods are read-only references. Combat Extended in this workspace is a reference for compatibility, not a second target.

## Where a feature lives

| Task | Read | Then search |
|---|---|---|
| One injury or condition | matching page under `docs/wiki/injuries/` | `Source/MoreInjuries/MoreInjuries/HealthConditions/` and `Defs/HediffDefs/` |
| concussion / trauma stroke | `docs/concussion.md` | `HealthConditions/HeadInjury/` and `Defs/Internal/HeadTraumaProperties.xml` |
| Cascading conditions | `docs/wiki/pathophysiological-system.md`, `docs/wiki/concepts.md` | `HealthConditions/Secondary/` |
| Devices, drugs, CPR, transfusions | `docs/wiki/medical-devices.md` | `Defs/ThingDefs/Medicine/`, `AI/`, `HealthConditions/Drugs/`, `HealthConditions/HeavyBleeding/` |
| Body parts | `docs/wiki/body-parts.md` | `Defs/BodyPartDefs/`, `Patches/Patch_BodyParts.xml` |
| Research | `docs/wiki/research.md` | `Defs/Research/` |
| Surgeries | `docs/wiki/surgeries.md` | `Defs/RecipeDefs/Surgeries/`, `Patches/Patch_Surgeries.xml` |
| Work types | `docs/wiki/work-types.md` | `Defs/WorkGiverDefs.xml`, `AI/WorkGivers/` |
| Mod settings | `docs/coding-style.md` only if adding a setting | `Languages/English/Keyed/Lang_Settings.xml`, `Roslyn/Metadata/Settings/` |
| Compatibility | `COMPATIBILITY.md` | `Patches/Mod_*.xml`, `Patches/Dlc_*.xml`, `Integrations/` |
| Localization | `Languages/TRANSLATING.md` | `Languages/`, `MoreInjuries.LocalizationTests` |
| Save upgrade | none until blocked | `Versioning/` |
| Style | `docs/coding-style.md` | neighboring file in the same feature folder |
| Build or install | `INSTALL.md` | this file's Validation section |

Player-facing index: `docs/wiki/README.md`. Open it for a status question about what the manual claims. Do not open it, plus every injury page, for a code change.

Condition code is one folder per feature under `HealthConditions/`: `Acidosis`, `AdrenalineRush`, `BrainDamage`, `CardiacArrest`, `Choking`, `Drugs`, `EmpShutdown`, `Fractures`, `Gangrene`, `HeadInjury`, `HearingLoss`, `HeavyBleeding`, `Hemodilution`, `HydrostaticShock`, `HypovolemicShock`, `Hypoxia`, `InhalationInjury`, `IntestinalSpill`, `LungCollapse`, `MechaniteTherapy`, `Paralysis`, `Secondary`, `SpallingInjury`.

If the wiki page has no matching folder, search the `defName` in `Defs/` and `Patches/` before concluding the feature is XML-only. Known XML-only or patch-owned examples include hypothermia (`Patches/Patch_Hypothermia.xml`) and vasodilation (`Defs/HediffDefs/Hediffs_Vasodilation.xml`). Confirm; do not treat this sentence as a complete list.

## Authority

- Latest explicit user decision overrides docs.
- Intended player behavior: the matching `docs/wiki/` page.
- Exact def names, field names, and runtime behavior: current XML and C#.
- **If a wiki page says a mechanic is missing and current source implements it, the page is stale.** Do not report the mechanic as absent.
- `COMPATIBILITY.md` is a support summary, not a list of every patch. Patches also exist for Combat Extended, Biotech, Humanoid Alien Races, and RBSE.

## Context discipline

Normal task:

1. this file;
2. the one owner doc from the table;
3. a targeted search in that feature's folder and def file.

Expand only when blocked.

- Do not inventory unrelated conditions.
- Do not reopen Combat Extended, Rimsenal, or other workspace mods to reconfirm a fact already stated in `COMPATIBILITY.md` or the owning wiki page.
- A compatibility change searches the other mod only for the exact def, type, or method being patched.
- Verify uncertain RimWorld APIs against `Krafs.Rimworld.Ref` `1.6.4633`, not against another mod's version and not against the Combat Extended compat pin `1.6.4850`.
- A pure data change does not need `HealthConditions/` unless a worker, comp, or job is involved. A pure C# change does not need the wiki unless player-visible behavior, ids, or recipes change.

## Project invariants

- Follow `docs/coding-style.md`. XML-backed fields stay camelCase. Save keys stay string literals. `DefOf` fields stay public and PascalCase.
- Do not rename a `defName`, job def, or `Scribe` key without a migration in `Versioning/` and a user decision.
- Do not add a temporary recipe, def alias, or setting for a disposable test save.
- Player-visible text goes through `Languages/`. English is the source language.
- Optional mods stay optional. Biotech, Combat Extended, Dub's Bad Hygiene, Humanoid Alien Races, and RBSE integrations must not run unless that mod is active. Bad Hygiene C# is behind `MOD_BAD_HYGIENE`.
- Do not add a dependency, `loadAfter` entry, or Harmony target without approval.
- Do not change `packageId`, namespace, supported versions, the RimWorld ref, or the Harmony package unless requested.
- Wiki blocks marked `@generate_toc`, `@generate_breadcrumb_trail`, or `@generate_link_to_top` are owned by `MoreInjuries.WikiGen`. Edit headings and prose. Do not hand-maintain the generated block. Regenerate only when headings or the page tree changed.

## Documentation updates

Root `AGENTS.md` owns the workspace rule. This mod follows it.

After a mechanic change:

1. update the owning wiki page from **current source**;
2. mark it `added, not verified`;
3. update this file only if the feature's owner path or validation command changed;
4. update `COMPATIBILITY.md` only if support or conflict status changed;
5. remove the mechanic from any "missing" or "deferred" list once it exists;
6. set `code-synced: YYYY-MM-DD` on the owner page.

Statuses: `implemented` | `added, not verified` | `partial` | `missing` | `deferred`.

Do not copy mechanic numbers into this router. Internal refactors with unchanged behavior need no doc churn.

## Validation

From the workspace root, `Custom\Scripts\Build.ps1 -Target MoreInjuries`. That runs `dotnet build Source\MoreInjuries\MoreInjuries\MoreInjuries.csproj -c Release` from this mod root. Do not run raw `dotnet build`. Do not use Debug (it requires `BadHygiene.dll`). Do not run `deploy.ps1`.

- Tests, only if behavior under test changed: `dotnet test Source\MoreInjuries\MoreInjuries.sln -c Test`
- Localization, only if keyed strings or def injections changed: `dotnet test Source\MoreInjuries\MoreInjuries.LocalizationTests\MoreInjuries.LocalizationTests.csproj`
- XML-only edits: no build. Check the edited def name is referenced consistently.

`Debug` enables `MOD_BAD_HYGIENE`. `Test` defines `TEST` and `DEBUG`. Do not flip those to make a failure disappear. Release is the agent build.

On failure the script prints at most 5 distinct diagnostics. Use its `Read:` command for a bounded excerpt of `Custom\.build-logs\MoreInjuries.log`. Do not pass `-v d` or `-v diag` unless those five are not enough. Do not launch RimWorld. Compilation is not play acceptance; record the in-game check the change still needs.

Validation notes code-synced: 2026-09-25.
