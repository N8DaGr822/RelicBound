# Milestone 1.3: Relic and mastery composition

Historical slice reference. Deferred-feature statements and test counts describe
this milestone; see the [current audit](COMPLETION_AUDIT.md) and
[documentation index](README.md) for later additions.

## Definitions remain data

The existing Crushing Blow and Lightning Infusion definitions are unchanged.
`AbilityModifierDefinition` now also accepts an optional immutable `ChainEffect`:
damage type, additional target count, and whether it requires an existing chain.
The original two-argument constructor remains available and still requires damage
effects. A chain-only modifier uses an empty damage list plus a chain definition.

| Component | Definition |
| --- | --- |
| Crushing Blow | 40 physical damage |
| Lightning Infusion | Adds 12 lightning damage |
| Stormheart | Starts a lightning chain to two additional targets |
| Tempest | Extends an existing lightning chain by one target |

The resolver checks effect data, not these IDs. Stormbreaker Strike is the display
name for this composition; it is not a separate ability implementation.

## Resolution rules

- Resolve damage and assemble the chain plan before mutating any combatant.
- One chain-starting modifier is supported per action. Additional chain starters
  are rejected rather than executing the same chain more than once.
- A chain requires damage of its declared type. An extension requires a chain
  of the same type, regardless of modifier ordering.
- Duplicate modifier IDs are rejected using the existing ordinal comparison.
- Total additional targets cannot exceed `CombatResolver.MaxChainTargets` (32).
  Over-budget configurations are rejected, not silently truncated.
- Candidate order is the deterministic targeting order. Skip the attacker,
  primary target, dead candidates, and already selected IDs. Distinct instances
  sharing an ID are treated as one identity; the first eligible one is used.
- Fewer available enemies means fewer hits. No chain candidates means only
  the primary target is hit, including through the original four-argument API.
- Secondary damage copies only the matching damage contributions and retains
  their source IDs. Each secondary action also records the chain starter in
  `TriggerSourceId`.
- Secondary hits have no chain plan and never call the resolver again.
  Repeated/cyclic candidate references cannot cause repeat hits or recursion.
- Killing the primary target does not cancel already planned chains.
- Null candidates or failures during candidate enumeration abort before health
  changes. Candidate sequences must be finite and trusted.

The five-argument `Resolve` overload returns a read-only list of action snapshots,
primary first, followed by chain targets. Calculated and applied damage remain
separate, including for overkill.

## Sandbox outcome

| Enemy | Damage | Health |
| --- | --- | --- |
| Skeleton Warrior | 40 physical + 12 lightning | 100 -> 48 |
| Skeleton Archer | 12 lightning | 100 -> 88 |
| Skeleton Mage | 12 lightning | 100 -> 88 |
| Skeleton Knight | 12 lightning | 100 -> 88 |

Tests also demonstrate removing Tempest produces two secondary hits and removing
both chain modifiers preserves the infused primary attack without secondary hits.
Stormheart without lightning damage and Tempest without a chain are invalid.

## Limits and next decisions

The lower-level `Resolve` APIs accept active modifiers and eligible enemies.
Milestone 1.4 adds `ResolveLoadout` for discovery and personal mastery checks;
see [character unlocks and loadouts](CHARACTER_UNLOCKS_AND_LOADOUTS.md).
Persistent equipment ownership, faction filtering, distance rules, and spatial
target searches remain unimplemented.

There is no generalized reaction queue in this slice. Safety comes from a bounded,
one-pass plan that cannot activate itself. A future system supporting arbitrary
triggered reactions will need explicit event budgets and provenance.

State mutation still assumes sequential simulation execution. There is no
concurrency control, networking, persistence, or rendering dependency.
