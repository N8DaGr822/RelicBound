# Combat reactions and protective mastery

Source: [resolver](../src/Relicbound.Simulation/Combat/CombatResolver.cs),
[event processor](../src/Relicbound.Simulation/Combat/CombatEventProcessor.cs), and
[combatant](../src/Relicbound.Simulation/Entities/Combatant.cs).

## Reaction definitions and ordering

`ReactionDefinition` contains a nonblank ID, a trigger DamageType, a DamageEffect,
and positive MaxActivations (default 1). The current damage types are Physical
and Lightning. Definitions live in Core; event processing lives in Simulation.

Entity-aware resolution first queues each primary damage contribution, then each
selected chain target's matching damage contributions, then any ally barrier.
All seed events enter the queue before any reactions. Processing is FIFO, with
reaction definitions considered in declaration order for each event. A reaction
inherits tick, attacker, and target from its parent; it does not choose new targets.

Activation counts are per reaction ID across the entire action, **not per enemy**.
Reaction IDs must be unique in the expanded reaction list. A default reaction
cannot repeatedly activate itself. Explicit higher activation limits permit
repetition, still subject to the global budget.

The budget is 128 total enqueued events, including seed events. A caller of the
event processor may choose a smaller positive budget, but cannot exceed 128.
Exceeding it throws before combat state is committed. Candidate enumerations are
trusted finite sequences; the event budget is not a timeout for arbitrary input
enumerators. Chain target count is independently limited to 32.

Example tested cycle:

```text
Sequence  Parent  Source  Damage
0         null    strike  40 Physical
1         0       spark    2 Lightning
2         1       echo     1 Physical
```

With each reaction limited to one activation, the cycle stops at 43 calculated
damage. Explicitly repeating versions hit the global budget and reject the action
without changing health, resources, cooldowns, or last-action tick.

## Structured results

| Field | Meaning |
| --- | --- |
| `CombatEvent.Tick` | Authoritative logical tick |
| `Sequence` | Zero-based queue sequence within the action |
| `ParentSequence` | Triggering event sequence; null for seeds |
| `SourceId` | Ability, modifier, or reaction responsible |
| `AttackerId`, `TargetId` | Runtime participant IDs |
| `DamageType` | Physical/Lightning for damage; null for a barrier |
| `Amount` | Planned contribution, not necessarily actual health lost |
| `Reason` | `primary-hit`, `chain-hit`, `reaction-hit`, or `mastery-barrier` |

Action results are primary first, then chain targets, then the optional protected
ally. Their Events collections contain only events for that target. Use event
Sequence to reconstruct full-action ordering across results.

`CombatResult.TotalDamage` is calculated damage. `CombatActionResult.AppliedDamage`
is health lost after barrier absorption and overkill clamping. BarrierBefore/After
separately describe shielding. Event amounts can exceed actual health loss or the
barrier amount successfully added at its cap.

The pure damage-plan overload collects reactions but does not execute them.
Entity-aware resolution is required to see expanded reaction damage. The console
runner's `--events` flag prints JSON event lines mixed with its human-readable
scenario output; it is not a pure JSONL export or a complete progression log.

## Storm Guardian versus Tempest

| Selected mastery | Result with Lightning Infusion and Stormheart |
| --- | --- |
| None | Primary takes 40 physical + 12 lightning; up to two additional enemies take 12 lightning |
| Tempest | Same primary hit; up to three additional enemies take 12 lightning |
| Storm Guardian | Same primary hit; no chain hits; distinct living ally gains a 20-point barrier |

The protective transformation requires a valid chain with matching damage, and
cannot combine with chain extensions. Only one protective transformation may
apply. The ally must differ from attacker and primary target and cannot appear
among enemy candidates. Invalid prerequisites fail before any combat commit.

Barriers stack up to the ally's MaxHealth. Incoming damage consumes barrier first,
then health. A 20-point barrier absorbs 20 of a 25-damage hit, leaving five health
lost. Barriers do not heal, have no expiry, and do not trigger damage reactions.

When configured, protective mastery unlocks under the same discovery, qualifying
chain-attack, and trial requirements as offensive mastery. It must still be
selected. Barrier grants do not count as damaging chain attacks for progression.

## Coverage and remaining work

[ReactionCombatTests](../tests/Relicbound.Simulation.Tests/ReactionCombatTests.cs)
cover actual cycles, global budgets, ordering, repeatable JSON, duplicate IDs,
and atomic failure. [ProtectiveMasteryTests](../tests/Relicbound.Simulation.Tests/ProtectiveMasteryTests.cs)
cover substitution, absorption, caps, locked selections, and invalid allies/relics.
General status-effect scheduling, resistance, critical strikes, arbitrary target
selection by reactions, and complete progression/encounter logs remain unimplemented.
