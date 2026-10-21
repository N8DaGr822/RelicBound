# Combat Slice 01: Crushing Blow + Lightning Infusion

This implemented scenario supersedes the earlier proposed Heavy Strike fixture.
The damage values are prototype values, not final balance decisions.

Historical slice reference. Deferred-feature statements and test counts describe
this milestone; see the [current audit](COMPLETION_AUDIT.md) and
[documentation index](README.md) for later additions.

## Purpose

Compose an independent ability and modifier without changing either definition.
Return structured damage contributions that can be inspected without a renderer.

## Definitions

| Source ID | Definition | Damage |
| --- | --- | --- |
| `crushing-blow` | Base ability | 40 physical |
| `lightning-infusion` | Ability modifier | 12 lightning |

Definitions copy their input effect collections and expose read-only snapshots.
Effects carry a validated damage type and a positive integer amount.

## Automated assertions

1. Crushing Blow alone produces one contribution and 40 total physical damage.
2. Lightning Infusion adds 12 lightning damage, preserving both source IDs and
   producing 52 total damage in base-then-modifier order.
3. Reusing the same ordered inputs produces equivalent results.
4. Applying a modifier ID twice throws instead of stacking it.
5. Replacing an element in the original ability effect array does not change
   the constructed ability's damage.
6. Constructing a zero-damage effect throws.

The six supplied tests cover these cases; they do not exhaust every constructor
validation path or establish correctness for future health/target rules.

## Sandbox output

```text
=== RELICBOUND Combat Sandbox ===
Ability: crushing-blow
  crushing-blow: 40 Physical
  lightning-infusion: 12 Lightning
Total damage: 52
```

## Boundaries

The resolver calculates contributions without applying them to world state.
It does not validate discovery ownership, choose targets, deduct health or
resources, apply armor/resistances, calculate timing, or trigger other effects.
There is no recursive activation path in this slice.

Stormheart chain targets and Tempest mastery will be added after entities and
health. They are not part of the current 52-damage demonstration.

Milestone 1.2 now adds health application through a separate resolver overload.
The damage-only behavior above is preserved; the current Sandbox output and
health rules are documented in [entities and health](COMBAT_ENTITIES_AND_HEALTH.md).
