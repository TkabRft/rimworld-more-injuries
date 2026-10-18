# Death Rattle transition

Status: `implemented` for the audited Death Rattle Continued 1.6 layout. code-synced: 2026-10-18.

Player instructions belong in [save compatibility](wiki/save-compatibility.md). Related owners: [vital failure](vital-failure.md), [neurology](neurological-runtime.md), and [remaining expansion](medical-expansion-todo.md).

## Priority and optional loading

Package `Troopersmith1.DeathRattle` is optional; no Death Rattle assembly reference or dependency was added. Its incompatibility declaration is removed. The bridge marks XML operations returned by that package's `ModContentPack.Patches` and skips only those instances in `PatchOperation.Apply`, before they rewrite shared capacity/heart-attack defs. Other mods' operations remain effective. The source defs remain loaded for deserialization.

After defs resolve, the bridge explicitly completes the audited `DeathRattle.HarmonyPatches` and `DeathRattle.PostDefFixer` initializers, then removes patches owned by `cat2002.deathrattle` and replaces that package's injected procedures. This does not depend on startup class visitation order. Unknown runtime layouts fail priority preflight and do not receive a successful handoff message. Other Harmony owners and mod settings are untouched.

## Conversion

Pawn PostLoadInit registers loaded pawns regardless of tracking components. This includes serialized off-map, dead and nested-holder pawns. The game component preflights all registered conditions and bills after reference resolution, before gameplay resumes.

| Legacy identifier | Destination |
|---|---|
| `ClinicalDeathNoHeartbeat` | `CardiacArrest` |
| `ClinicalDeathAsphyxiation` | `MI_RespiratoryFailure` |
| `LiverFailure` / `KidneyFailure` | `MI_LiverFailure` / `MI_RenalFailure` |
| `IntestinalFailure` / `Coma` | `MI_MetabolicFailure` / `MI_Coma` |
| `BrainDamage` | `MI_DiffuseBrainDamage` |
| `ArtificialComa` | `MI_ArtificialComa` |

Recognition requires source-package ownership. Severity, affected part, age, native load ID and applicable tending are preserved. Existing matching destinations retain the greater severity; merged-reference IDs are rewritten during save validation. The controller then reconciles restored functions, without granting progression ticks. No personality effects are rerolled, and dead pawns are not resurrected.

Supported surgery bills map to the owned coma and optional EPOE neuro-cure procedures. Coma ingredient selections retain narrower player choices but lose disallowed herbal ingredients. Registered doctors' affected active jobs are stopped for native cleanup and rescheduling. Existing failures on excluded nonfleshy pawns, unsupported classes/components, out-of-range severity, prohibited coma reversal and unknown bills prevent successful migration. Snapshots restore condition lists, severity and bill selections if conversion throws.

The module must be enabled and no active mod may declare a hard dependency on Death Rattle. Unsupported serialized extensions are reported rather than silently dropped. Death Rattle's colony/animal settings do not override More Injuries eligibility.

## Save handoff

New game-component keys are `deathRattleMigrationVersion` and `deathRattleTransitionNoticeShown`. Existing MI keys remain unchanged. A successful loaded-game conversion sends one localized letter per saved game: save a NEW copy, quit, disable Death Rattle, restart and load the new copy. No automatic save, mod-list edit or installed-file change occurs.

The guarded native `ScribeSaver.InitSaving`/`FinalizeSaving` hooks capture only game saves. After native saving completes, the bridge loads the completed XML with DTD/external resolution disabled, remaps merged condition references and checks for residual source identifiers or Death Rattle classes. Only clean copies lose Death Rattle's aligned ID/name/Steam-ID metadata entries. A same-directory temporary file and atomic replacement preserve the original complete native save if validation or replacement fails. Failure sends an explicit keep-Death-Rattle notice.

Readiness is deliberately conservative: ambiguous residual source-identifier values block the removal report. Third-party saved references and modified Death Rattle layouts may require additional adapters. Do not remove MI from the migrated save.

## Validation and acceptance

The final Release build and all 115 focused tests pass. Production-linked migration tests cover every standard condition mapping, duplicate merge/reference handling, idempotence, rollback, prohibited states, native-add failure, queued medicine filters and active-job rescheduling. XML fixtures cover aligned save metadata, merged references and residual identifiers. Pinned-reference metadata verifies the hook targets. Localization, runtime XML type coverage and the 230-file package hash/layout checks pass.

The native serializer, live Harmony ordering and real surgery/transport systems require manual game acceptance: load a backed-up affected save with both mods in both load orders; inspect map/caravan/contained/dead pawns and queued bills; save a new copy; restart without Death Rattle; verify no missing defs/classes/references, repeated notification or duplicated progression. Installation and game launch are not validation steps performed by the development workflow.
