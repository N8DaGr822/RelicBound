# Milestone 1.2: Combat entities and health

Historical slice reference. Deferred-feature statements and test counts describe
this milestone; see the [current audit](COMPLETION_AUDIT.md) and
[documentation index](README.md) for later additions.

## Behavior

`Combatant` lives in Simulation because health is runtime state. It has a stable
ID, positive maximum health, current health, and an `IsAlive` property. New
combatants start at full health. Callers cannot set health directly.

The combatant-aware `CombatResolver.Resolve` overload validates attacker and
target, calls the existing damage-only resolver, then applies the total damage.
Core definitions and the original resolver signature remain unchanged.

Rules for this slice:

- Both participants must be alive.
- Matching IDs are self-targeting, even for different object instances. IDs use
  ordinal, case-sensitive comparison, consistent with modifier IDs.
- Health never falls below zero. Long damage totals are clamped before integer
  conversion, so overkill cannot wrap health.
- Invalid participants, invalid definitions, duplicate modifiers, or a failing
  modifier enumeration throw before health changes.
- The attacker is unchanged; only the target loses health.
- `CombatActionResult` snapshots IDs and target health before/after, alongside
  the original immutable damage breakdown.
- Calculated damage and actual health lost remain separate: a 52-damage attack
  against 15 health calculates 52 but applies 15.

Contributions describe calculated damage. This slice applies their sum once and
does not dispatch per-contribution reactions or assign overkill to damage types.

## Sandbox output

```text
=== RELICBOUND Combat Sandbox ===
Attacker: test-adventurer
Target: skeleton-warrior
Ability: crushing-blow
  crushing-blow: 40 Physical
  lightning-infusion: 12 Lightning
Total damage: 52
Applied damage: 52
Target health: 100 -> 48
```

## Limits

The caller supplies combatants directly; there is no world registry, ownership
validation, networking, or command deduplication yet. Repeated valid calls are
separate attacks. IDs must be assigned consistently by the caller.

The simulation assumes sequential execution and trusted effect enumeration.
It does not synchronize concurrent attacks. Resource costs, healing, range,
armor, resistances, relic chains, mastery, and effect queues remain later work.

Update: Milestone 1.3 now implements relic/mastery chain composition. See
[composition rules](RELIC_AND_MASTERY_COMPOSITION.md) for the current Sandbox
results and the new multi-target overload. The health rules above still apply.
