# Implementation progress

The kickoff is a roadmap. Work through one demonstrable behavior at a time;
its proposed vocabulary is not a requirement to create every type up front.

This is a chronological record. For current status and next work, use the
[completion audit](COMPLETION_AUDIT.md) and [roadmap](ROADMAP.md); earlier slices
describe the implementation at that point in time.

## Epic 0: Repo and tests

- [x] Create Core, Simulation, Simulation.Tests, and a console runner.
- [x] Configure project references and nullable analysis.
- [x] Add basic formatting configuration and ignore generated build outputs.
- [x] Add a GitHub Actions build/test workflow.
- [ ] Verify the workflow on GitHub after repository hosting is configured.
- [x] Write proposed assertion-based expectations for the first tiny scenario.
- [x] Implement six executable tests for Combat Slice 01, which supersedes the
  initial Heavy Strike proposal.

## Working decisions

- Preserve `Relicbound.Sandbox` as the console runner; no separate Tools project.
- Keep Core on .NET Standard 2.1 and Simulation, Sandbox, and tests on .NET 10.
- Preserve the existing web starter. Its eventual role is not decided.
- The revised milestone supersedes the kickoff's example names with Crushing
  Blow, Lightning Infusion, Stormheart, and Tempest.
- Start with fixed integer damage contributions. Resource costs and tick-based
  cooldowns are implemented; randomness, hit chance, armor, and spatial rules
  are deferred.
- GitHub Actions is the initial CI configuration choice; no remote repository
  has been created or configured as part of this setup.

## Combat Slice 01

- [x] Add immutable ability, modifier, and damage definitions in Core.
- [x] Add a pure damage resolver and structured contributions in Simulation.
- [x] Reject duplicate modifier IDs using ordinal, case-sensitive comparison.
- [x] Add six xUnit tests and a Sandbox demonstration of 40 physical damage
  plus 12 lightning damage.

Definitions are data; execution belongs to the resolver. Contributions retain
base-effect order followed by the caller's modifier/effect order. Determinism
requires the same ordered inputs. No gameplay randomness is used.

## Historical planning checkpoint before the arena

The next slice has not been selected. In-memory encounter objectives and rewards,
combat-earned mastery credit, resource costs, and tick-based cooldowns are
implemented. World navigation and broader triggered effects remain future work.

The broader kickoff backlog remains open: this slice does not implement an
event queue, reaction budgets, multiplayer, or persistence.

## Milestone 1.2: Combat entities and health

- [x] Add combatants with immutable IDs, positive maximum health, and bounded
  simulation-owned health mutation.
- [x] Preserve the damage-only resolver and add a combatant-aware overload.
- [x] Reject null/dead participants and self-targeting by ordinal ID comparison.
- [x] Resolve all effects before health mutation; invalid modifiers do not
  partially damage a target.
- [x] Record target health before/after and actual damage separately from
  calculated damage.
- [x] Test lethal damage, overkill, large totals, repeated attacks, stable
  snapshots, deterministic outcomes, and invalid inputs.
- [x] Demonstrate 100 -> 48 target health in Sandbox.

See [entities and health](COMBAT_ENTITIES_AND_HEALTH.md). These mutable entities
are for sequential simulation execution, not a thread-safe multiplayer host.

## Milestone 1.3: Relic and mastery composition

- [x] Add immutable, typed chain definitions independent of ability names.
- [x] Compose Stormheart's two targets with Tempest's one additional target.
- [x] Require matching damage for chains and a matching chain for extensions.
- [x] Preserve existing constructor and resolver signatures.
- [x] Select distinct living targets in caller-provided order.
- [x] Reject duplicate activation and cap chains at 32 additional targets.
- [x] Apply secondary hits once without recursive activation.
- [x] Plan all damage before health mutation, including candidate enumeration.
- [x] Demonstrate 52 primary damage and three 12-damage lightning hits.

Release build: zero warnings/errors. All 47 test cases passed, including the
original 25 and 22 chain cases. See [composition rules](RELIC_AND_MASTERY_COMPOSITION.md).
These tests establish bounded one-pass chains, not a generalized event queue
or arbitrary cyclic proc-graph processing.

## Milestone 1.4: Character unlocks and validated loadouts

- [x] Classify trusted modifier definitions as Discovery, Relic, or Mastery.
- [x] Store discovery and mastery unlocks separately on each combatant.
- [x] Separate equipped relic state from immutable selected technique IDs.
- [x] Resolve selections through a copied, ordinal-ID definition catalog.
- [x] Validate source categories and personal unlocks before combat resolution.
- [x] Reuse existing effect prerequisite validation and damage application.
- [x] Preserve lower-level resolver APIs and existing definition constructors.
- [x] Demonstrate locked Tempest rejection, two chains without it, and three
  chains when earned and selected.
- [x] Verify relic reassignment does not transfer unlocks, earned but unselected
  effects stay inactive, and invalid loadouts leave health unchanged.

Release build: zero warnings/errors. All 67 test cases passed, including the
previous 47 and 20 loadout cases. See [unlock/loadout rules](CHARACTER_UNLOCKS_AND_LOADOUTS.md).
Equipment assignment and unlock grants are trusted in-memory operations; this
does not implement item ownership, trading, or world-earned progression triggers.

## Milestone 1.5: Gameplay progression and combat pacing

- [x] Add immutable configurable progression rules for discovery encounters,
  mastery encounters, damage type, and qualifying-attack threshold.
- [x] Grant discovery through idempotent encounter completion.
- [x] Require both three qualifying attacks and Storm Trial completion for Tempest.
- [x] Credit only successful validated attacks that damage an additional enemy,
  once per attack rather than once per chain hit.
- [x] Keep encounter history and mastery credit personal to the character.
- [x] Add configurable ability resource costs and cooldown ticks.
- [x] Track bounded resources and cooldown readiness by character/ability ID.
- [x] Reject insufficient resources, active cooldowns, backwards ticks, and
  overflowing timestamps before changing combat state.
- [x] Preserve zero-cost/zero-cooldown defaults for existing ability definitions.
- [x] Demonstrate progression, an encounter guardian defeat, earned mastery,
  cooldown rejection, and resource rejection in Sandbox.

The earning conditions (three attacks AND the trial), cost of 10, and cooldown
of two ticks are explicit prototype decisions, not final balance values.
See [progression and pacing](GAMEPLAY_PROGRESSION_AND_PACING.md) for host
responsibilities and API compatibility details.

Verification: Release build completed with zero warnings/errors, all 107 test
cases passed, and the Sandbox ran the full earning and pacing demonstration.

## Milestone 1.6: Tracked encounter objectives

- [x] Define immutable interaction and defeat-all-enemies objectives in Core.
- [x] Bind each session to one runtime participant and an exact, living enemy roster.
- [x] Require interaction before awarding the shrine discovery.
- [x] Require all bound enemies to be defeated before awarding trial completion.
- [x] Reject other participants, including separate objects with the same ID.
- [x] Preserve personal reward idempotency and session completion idempotency.
- [x] Leave sessions incomplete when progression awarding fails.
- [x] Replace Sandbox's direct encounter awards with tracked session operations.

The combat resolver and progression pipeline remain unchanged. See
[encounter objectives](ENCOUNTER_OBJECTIVES.md) for session semantics and limits.

Verification: all 124 test cases passed, the Release build had zero warnings or
errors, and Sandbox demonstrated objective-gated and repeated completion.

## Reactions, protective mastery, and cooperative arena

The user authorized continuing through the broader roadmap, including Unity,
multiplayer, persistence, loot, expeditions, and trading. This scope is no longer
pending clarification.

- [x] Deterministic FIFO combat reactions, parent event sequences, reason codes,
  per-reaction activation limits, and an atomic 128-event processing budget.
- [x] Actual cyclic reaction tests and Storm Guardian ally barriers.
- [x] Shared .NET Standard contracts and an authoritative, serialized arena.
- [x] Movement, dodge, enemy AI, cooperative freeze/shatter, and branch effects.
- [x] ASP.NET Core session endpoints with server-issued identity and command retries.
- [x] Live two-client HTTP verification and 144 passing xUnit cases.
- [x] Unity source import kit with camera, primitive actors, and onscreen commands.
- [ ] Unity Editor compilation, visual verification, and two-client playtest.
- [ ] PostgreSQL persistence and durable character identity.
- [ ] Owned item instances, loot awards, complete expeditions, and atomic trading.
- [ ] Blazor companion features and production deployment.

Unity Editor and PostgreSQL are not installed, as confirmed by the user.
The Unity source is unverified until imported into an Editor project; the full
roadmap is not complete. See [cooperative arena](COOPERATIVE_ARENA.md).

## Documentation consolidation — October 21, 2026

- Added an index, architecture/state-ownership guide, current HTTP API reference,
  reaction/barrier rules, developer runbook, and remaining roadmap.
- Reconciled the audit to current code and separated earlier evidence from checks
  performed in this pass. Original kickoff remains unchanged.
- Rebuilt Release with zero warnings/errors; all 144 tests passed with zero skips.
- Documentation-only changes; no runtime or test behavior changed.
