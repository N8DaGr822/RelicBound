# Engineering Kickoff — RELICBOUND

**Version:** 0.2  
**Audience:** Initial developer(s) building the first C# prototype  
**Status:** Recommended starting implementation; not a locked technology specification

## 1. Engineering Goal

Build the smallest real gameplay system that proves this game's differentiator: **the same foundational action can become a meaningfully different playstyle depending on a world discovery, equipped relic, and personally earned mastery**. Prioritize a headless simulation before graphical polish, economy, persistent accounts, or blockchain integrations.

**First demonstrable outcome:** One simulated character uses Heavy Strike, adds Runic Infusion, equips Thunderbrand, and chooses a Stormbreaker mastery specialization. The attack behaves differently under each configuration, and automated tests prove the event processing terminates and resolves in a predictable order.

## 2. Proposed Repository / Solution Layout

```text
relicbound/
├── README.md
├── docs/
│   ├── GAME_DESIGN.md
│   ├── ENGINEERING_KICKOFF.md
│   └── DECISIONS.md
├── adr/
│   └── 0001-engine-independent-simulation.md
├── src/
│   ├── Relicbound.Core/           # domain models + combat / item rules
│   ├── Relicbound.Simulation/     # tick orchestration + combat event processing
│   ├── Relicbound.Server/         # future authoritative host + networking
│   ├── Relicbound.Contracts/      # optional serialized client/server messages
│   └── Relicbound.Tools/          # simple scenario runner / balance tool
├── tests/
│   ├── Relicbound.Core.Tests/
│   └── Relicbound.Simulation.Tests/
└── client/
    └── Relicbound.Unity/         # add after headless combat proves useful
```

You can initially create only `Core`, `Simulation`, `Simulation.Tests`, and a console `Tools` runner. Avoid scaffolding all projects and infrastructure before they serve a real feature. A repo-local `.editorconfig`, nullable reference types, and CI running `dotnet test` are sensible early defaults.

## 3. Dependency Direction

**Core** knows only combat concepts and rules. **Simulation** orchestrates turns/ticks and combat events and depends on Core. **Tools**, tests, and eventually **Server** depend on Core/Simulation. Unity uses contracts/adapters to issue input intents and render authoritative state. No combat rule should need `MonoBehaviour`, database contexts, HTTP APIs, wallet types, or a service locator.

Keep data structures serializable where practical, but do not optimize all domain models for a hypothetical network format on day one. Use stable IDs to relate item definitions, item instances, abilities, and combatants.

## 4. Starter Domain Vocabulary

| Concept | Purpose | Important invariant |
| --- | --- | --- |
| `AbilityDefinition` | Content-authored base ability | Stable ID and declared compatible triggers/tags |
| `EffectDefinition` | Individual triggered behavior | Explicit condition, cost, scope and limits |
| `DiscoveryUnlock` | World-earned ability or behavior | Must be earned; not inferred from item possession |
| `MasteryUnlock` | Learned technique variant | Player/character progression; never transfers with item |
| `AbilityLoadout` | Active equipped techniques | Validated slot counts, resources, compatibility |
| `ItemDefinition` | Shared content and possible affixes | Versionable and independent from ownership |
| `ItemInstance` | Individually generated equipment | Unique instance ID and authoritative owner |
| `CombatEvent` | A rule-system event | Provenance, trigger type, tick/order, bounded follow-ups |
| `CombatState` | Server-held state snapshot | No client can independently mutate authoritative state |
| `TradeOffer` | Requested item exchange | Exactly eligible item instances, atomically committed |

Treat these as vocabulary, not as an instruction to create ten types before writing one test. Introduce each abstraction when it lets you validate a game behavior.

## 5. Ability Resolution Pipeline

A practical candidate pipeline:

1. Validate player intent: controlled actor, alive/state restrictions, resources, cooldown, range, and target.
2. Resolve the base action: attack timing, target selection, hit/avoidance and impact.
3. Assemble currently active effect sources from discovery, mastery, and relics; reject invalid combinations.
4. Queue typed combat events such as `HitLanded`, `DodgeSucceeded`, `StatusApplied` and `ChargeConsumed`.
5. Process reactions in a documented stable order; enforce proc limits, recursion guards, and per-effect cooldowns.
6. Update combat state and append a compact reason-coded combat log that supports automated assertions.
7. Emit presentation events for rendering and later network replication.

**Never create effects by recursively calling arbitrary ability handlers without a budget.** A storm hit creating another storm hit can generate an accidental infinite proc graph. Start with a maximum effects-per-action bound and a rule that an effect cannot automatically retrigger itself unless explicitly allowed.

## 6. First Three Example Fixtures

### Fixture A — Basic ability

- Attacker uses `HeavyStrike` against one unshielded target.
- Expected: correct resource cost and a single controlled damage event.
- Test: same seeded setup produces the same event log.

### Fixture B — Hybrid transformation

- Attacker equips `RunicInfusion` discovery and `Thunderbrand` relic; a prior block has granted an electrical charge.
- Expected: Heavy Strike consumes charge and emits one permitted lightning discharge, not repeated infinite discharges.
- Test: unequipping either source changes effect behavior while preserving base action.

### Fixture C — Personal mastery

- Two otherwise identical attackers have the same relic.
- Attacker 1 uses offensive Stormbreaker specialization; attacker 2 uses protective Storm Guardian specialization.
- Expected: first produces a positional damage follow-up; second creates an ally barrier. Identical relic ownership is not enough to copy the other's mastery.

These three fixtures directly test our promise: *same equipment, different playstyles*.

## 7. First Backlog — In Priority Order

### Epic 0: Repo and tests

- [ ] Create minimal C# solution and projects (`Core`, `Simulation`, tests, console runner).
- [ ] Configure formatting, nullable analysis, project references, and CI test execution.
- [ ] Define the first tiny combat scenario and assertion-based expected results.

### Epic 1: Combat primitives

- [ ] Add combatant health/resources and stable identifiers.
- [ ] Implement input intent validation and a single base action.
- [ ] Implement event queue, stable ordering, effect processing cap and compact logs.
- [ ] Implement deterministic test fixtures with seeded random source when randomness is needed.

### Epic 2: Classless expression

- [ ] Add effect source categories: `Base`, `Discovery`, `Mastery`, `Relic`.
- [ ] Implement two combinable effects with explicit tags and prerequisites.
- [ ] Represent two hybrid loadouts using the same domain model.
- [ ] Add meaningful mastery branching and tests that traded relics cannot transfer it.

### Epic 3: Playable combat

- [ ] Integrate movement/attack/dodge in a simple isometric scene after domain model is stable.
- [ ] Add one monster AI behavior and clear combat feedback.
- [ ] Verify frame rendering remains decoupled from combat rule decisions.

### Epic 4: Authoritative cooperation

- [ ] Add server-held combat state and player intent messages.
- [ ] Support two clients and one cross-player elemental interaction.
- [ ] Test disconnect and repeated/untrusted commands.

### Epic 5: Loot, expeditions and barter

- [ ] Generate item instances with persistent unique IDs and intrinsic power budgets.
- [ ] Add one branching dungeon modifier and a boss response.
- [ ] Validate one-for-one compatible-tier trade with configurable tolerance.
- [ ] Apply the trade in one auditable transaction; verify rollback/concurrency behavior.

**Not in the first backlog:** monetary exchange, blockchain, player wallets, tokenomics, settlements, massive persistent worlds, live auction house, elaborate crafting tree, extensive item art catalogs.

## 8. Data and Balancing Strategy

Maintain **definition vs instance** separation. A relic definition describes the allowable effect budget and gameplay behavior. An item instance records its random rolls, individual ID, inventory location, and ownership. Do not use a market price or rarity alone as intrinsic power score.

Start with test content as simple data declarations in C# or local JSON. Introduce an editor/content pipeline once designers need to work outside the codebase. Use schema/version evolution from the beginning where records will be persisted; avoid building a generalized modding DSL before you know which effect patterns are common.

Power comparisons should be tier-first. For an initial scoring prototype, define a symmetrical difference test:

```text
Eligible = SameCompatibleTier(A, B)
        && Max(A.Power, B.Power) > 0
        && Abs(A.Power - B.Power) / Max(A.Power, B.Power) <= Tolerance
```

`Tolerance = 0.15` is only a **first experiment**. Guard against forged power values by deriving them server-side from trusted item data. Evaluate surprising synergies with combat tests and telemetry, not by assuming the numeric score predicts every build matchup.

## 9. Multiplayer and Inventory Threat Model

The client is untrusted. In multiplayer, the server alone determines actual cooldown state, hit detection outcome, loot generation, earned mastery credit, item ownership, and trade validity. Replay/instrumentation needs to be efficient enough for later cheat investigations.

For item trading, plan a server-side transaction that locks or conditionally updates both item instances, checks they are still controlled by each party, validates eligibility, moves them atomically, and journals the result once. Repeated submission of the same trade transaction ID must not execute twice. Never take "I own this item" from client input as proof.

Start with safe authoritative correctness; optimize network latency or use client prediction only after measuring the effect on gameplay feel. The chosen multiplayer stack and frame/tick specifics are undecided.

## 10. Engineering Definition of Done for v0.0.1

- [ ] `dotnet test` passes from a clean checkout.
- [ ] Headless scenario runner shows ability behavior with and without Runic Infusion.
- [ ] Mastery specialization produces a distinct outcome using the same relic.
- [ ] A test demonstrates effect processing terminates under cyclic proc conditions.
- [ ] No Unity, SQL, payment, or blockchain dependencies are referenced by combat rules.
- [ ] Combat actions and state transitions are inspectable in structured logs.

**Recommended first commit after scaffolding:** `feat(simulation): resolve base attack and bounded effect events`.

## 11. Questions to Decide After First Combat Proof

Should equipment define the initial resource system or should disciplines share a resource budget? Are mastery unlocks character-scoped or account-scoped? Should the game allow mid-expedition loadout swaps? How visible should hidden technique prerequisites be? Which encounters are required to validate item diversity? What client/netcode combination provides responsive action gameplay without compromising authority?

Do not block the first headless prototype on these answers. Record decisions when a working encounter gives evidence.
