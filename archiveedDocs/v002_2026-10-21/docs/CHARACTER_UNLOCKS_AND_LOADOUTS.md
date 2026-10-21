# Milestone 1.4: Character unlocks and validated loadouts

Historical slice reference. Deferred-feature statements and test counts describe
this milestone; see the [current audit](COMPLETION_AUDIT.md) and
[documentation index](README.md) for later additions.

## State and definitions

- `ModifierSource` classifies immutable modifier definitions as Discovery,
  Relic, or Mastery. Existing constructors retain Unspecified for compatibility
  with lower-level simulation calls.
- `AbilityLoadout` holds optional discovery and mastery selection IDs. This
  prototype has one slot of each kind. Null means no selection; blank IDs
  are rejected.
- Each `Combatant` owns separate discovery/mastery unlock sets and one
  `EquippedRelicId`. IDs use ordinal, case-sensitive comparison.
- `UnlockDiscovery` and `UnlockMastery` are idempotent trusted grant operations.
  They do not select effects. `EquipRelic(null)` unequips the relic.
- Equipping or unequipping never grants, removes, or transfers personal unlocks.

## Validated execution

Construct `CombatResolver` with trusted modifier definitions. The resolver copies
the catalog, rejects duplicate IDs and null/unclassified entries, and keeps the
immutable definitions. Changing the caller's catalog array cannot replace them.

Use `ResolveLoadout(attacker, target, ability, loadout, chainCandidates)` for the
validated character path:

1. Resolve the selected discovery ID from the catalog, verify its category,
   and require the attacker's discovery unlock.
2. Resolve the attacker's equipped relic ID and verify its category.
3. Resolve the selected mastery ID, verify its category, and require the
   attacker's mastery unlock.
4. Pass those effects in discovery/relic/mastery order to the existing resolver.
5. Validate participants and effect prerequisites, plan hits, and only then
   change health.

Unknown or wrong-category IDs are rejected. A selection cannot supply arbitrary
effect definitions. An unlocked effect stays inactive unless selected, and an
unequipped relic contributes nothing.

Existing prerequisites still apply: Stormheart needs selected lightning damage,
and selected Tempest needs a matching chain effect. Owning the corresponding
unlock without selecting it does not satisfy these requirements. Invalid
loadouts do not change health or progression.

## Sandbox comparison

Both characters have earned Lightning Infusion and equipped Stormheart:

| Character | Tempest unlock | Selected mastery | Additional enemies hit |
| --- | --- | --- | --- |
| Test Adventurer | No | None | 2 |
| Tempest Veteran | Yes | Tempest | 3 |

The adventurer first attempts to select locked Tempest. The attempt is rejected
and target health remains 100. Each valid scenario then starts with fresh enemies:
the primary loses 52 health and chain targets lose 12 each. Skeleton Knight
remains at 100 for the adventurer and ends at 88 for the veteran.

## Boundaries

This is an in-memory gameplay validation path, not a network authorization layer.
The simulation host must own the combatants, grant unlocks from trusted gameplay,
assign equipped relics, and supply trusted ability/catalog definitions and enemy
candidates. No API accepts these as authenticated client claims.

Grant methods currently accept stable IDs without checking the catalog; selected
IDs are checked at resolution. Relic assignment is not proof of item possession.
Inventory instances, trade transactions, equipment ownership, discovery encounters,
mastery earning conditions, persistence, and account identity are still deferred.

Update: Milestone 1.5 adds an encounter-completion reward path and automatic
combat credit through validated loadout execution. See
[gameplay progression and pacing](GAMEPLAY_PROGRESSION_AND_PACING.md).
Direct grant APIs remain available for trusted host/test setup.

The original raw-modifier `Resolve` APIs intentionally remain available for
low-level tests and tools; they do not enforce unlocks. Player-facing hosts should
use `ResolveLoadout`, not treat raw-modifier execution as a validated command.
Mutable combatants still require sequential simulation access.
