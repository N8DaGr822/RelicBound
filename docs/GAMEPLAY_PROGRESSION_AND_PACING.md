# Milestone 1.5: Gameplay progression and combat pacing

Historical slice reference. Deferred-feature statements and test counts describe
this milestone; see the [current audit](COMPLETION_AUDIT.md) and
[documentation index](README.md) for later additions.

## Prototype content rules

| Rule | Initial value |
| --- | --- |
| Discovery encounter | Runic Shrine (`runic-shrine`) grants Lightning Infusion |
| Combat mastery requirement | Three successful lightning-chain attacks |
| Encounter mastery requirement | Storm Trial (`storm-trial`) completed |
| Mastery reward | Tempest, when BOTH mastery requirements are satisfied |
| Crushing Blow resource cost | 10 |
| Crushing Blow cooldown | 2 simulation ticks |
| Default combatant resource capacity | 100, starting full |

These are placeholder choices. `ProgressionRules` and `AbilityDefinition` hold
the values; the execution code does not hard-code these content IDs or numbers.
One progression path is configured per resolver in this slice.

## Progression execution

`CompleteEncounter(character, encounterId)` is called by the trusted simulation
host after it determines that an encounter succeeded. Unknown encounters and dead
characters are rejected. Completion returns true the first time and false on a
repeat; repeating completion never adds mastery attack credit.

Runic Shrine grants the configured discovery. Storm Trial records the encounter
requirement. Requirements may be completed in either order. The resolver checks
eligibility after encounter rewards and qualifying attacks.

Only successful `ResolveLoadout` execution earns attack credit. It must select
the required discovery, produce a chain of the configured damage type, and
actually damage at least one additional enemy. Multiple chain hits count once.
Primary-only hits, raw-modifier execution, and rejected commands earn no credit.
Credit is stored by mastery ID on the character and saturates at `int.MaxValue`.
Earning mastery does not automatically select it.

Encounter history, credit, and unlocks stay with the character when relics are
reassigned. Direct unlock methods remain trusted setup/admin primitives.

## Deterministic pacing

The new timed overloads of `ResolveLoadout` and entity-aware `Resolve` accept
an explicit nonnegative `long tick`. No timers, wall clock, sleeps, or frame time
participate in resolution. A tick has no fixed real-time duration yet.

- Cooldowns are stored per character and ordinal ability ID.
- An action at tick T with cooldown C is available again at T + C, inclusive.
- Successful action ticks may stay equal for different ready abilities but cannot
  move backwards for that attacker.
- Negative ticks and T + C overflow are rejected.
- Cost is charged once per successful action, never per secondary hit.
- All participant, loadout, effect, target enumeration, resource, and timing
  validation finishes before health/resource/cooldown mutation.
- Rejected commands do not advance the attacker's last successful tick, consume
  resource, start cooldowns, award credit, or change target health.

Action snapshots include `Tick`. The primary snapshot also records
`ResourceSpent` and `AbilityReadyTick`; secondary hits spend zero resource and
have no separate readiness timestamp.

`RestoreResource(amount)` is an explicit trusted host operation, clamps to maximum
capacity without overflow, rejects negative amounts, and does not reset cooldowns.
There is no automatic regeneration or rest mechanic yet.

## Compatibility

Existing ability constructors default to zero cost and zero cooldown. Existing
combatant constructors default to 100 resource; an overload accepts a capacity
of zero or greater. Existing untimed resolver overloads execute at tick zero.
Use the timed overloads for actors whose simulation time has advanced.

The pure `Resolve(ability, modifiers)` API only calculates effects; it neither
checks nor spends resources and does not grant progression. Entity-aware raw
modifier APIs enforce pacing but still bypass unlock validation and award no
mastery credit.

## Sandbox sequence

1. Both characters complete Runic Shrine and equip Stormheart.
2. The veteran makes successful chained attacks at ticks 0, 2, and 4.
3. An attempted attack at tick 1 is rejected with health and resources unchanged.
4. Three attacks alone do not grant Tempest.
5. At tick 6 the veteran defeats the trial guardian. The scenario host reports
   Storm Trial completion, satisfying the remaining mastery requirement.
6. At tick 8 both characters attack fresh enemies. The adventurer chains to two;
   the veteran selects earned Tempest and chains to three.
7. A character with only five resource attempts Crushing Blow and is rejected
   without damage or resource expenditure.

## Remaining boundaries

Encounter completion is a host-authorized event, not proof supplied by a client.
Milestone 1.6 adds tracked interaction and defeat objectives through encounter
sessions, and the Sandbox uses those sessions for rewards. See
[encounter objectives](ENCOUNTER_OBJECTIVES.md). A quest system and world remain
outside this implementation.
Catalogs, ability definitions, equipment assignments, and grants remain trusted.

The host must supply one coherent timeline for the world. The engine checks
monotonic successful ticks per attacker, not global ordering across all actors.
There is no network command deduplication, synchronization, persistence, or
concurrent execution support.
