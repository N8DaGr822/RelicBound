# Milestone 1.6: Tracked encounter objectives

Historical slice reference. Deferred-feature statements and test counts describe
this milestone; see the [current audit](COMPLETION_AUDIT.md) and
[documentation index](README.md) for later additions.

## Definitions and runtime state

`EncounterDefinition` in Core contains an ID, an objective, and an immutable copy
of enemy IDs. Interaction encounters have no enemies. DefeatAllEnemies encounters
require at least one distinct, nonblank enemy ID.

`EncounterSession` in Simulation binds that definition to one live participant,
the exact enemy roster, and the existing combat resolver. All enemies must be
alive at creation, distinct by ordinal ID, and different from the participant.
The roster collection is copied, but its combatants remain live objects so normal
combat updates their health. Empty or substituted rosters cannot satisfy a trial.

## Completion

- `Interact(participant)` completes an interaction objective and invokes the
  existing progression reward path. It rejects combat objectives.
- `TryComplete(participant)` completes a combat objective only when every bound
  enemy is dead. It returns false while enemies live or interaction is still needed.
- Both methods require the exact bound participant object, not merely a matching
  ID. A dead participant cannot receive a new completion reward.
- Completion returns true when this session first completes; later calls return
  false. Personal encounter rewards remain idempotent across separate sessions.
- A reward failure leaves `IsCompleted` false. Objective checks themselves do
  not alter health, resources, cooldowns, or mastery credit.
- Defeating an unrelated combatant, even an ID alias of a guardian, does not
  satisfy the bound enemy's objective.

The session records completion only after the existing progression method
succeeds. It does not implement a database transaction or concurrent execution.

## Sandbox demonstration

The shrine cannot complete before interaction. Each participant interacts and
earns Lightning Infusion. After training, the veteran starts Storm Trial:

```text
Storm Trial completion with guardian alive: False
Storm Trial completion after guardian defeat: True
Repeated Storm Trial completion: False
Storm Trial completed; Tempest earned: True
```

The existing combat, mastery prerequisites, resources, cooldowns, and comparison
between the two characters remain in place.

## Limits

Definitions, sessions, and combatants are created by the trusted host. Calling
an interaction is not yet gated by distance, movement, scene visibility, or network
identity. Enemy defeat is based on current health; there is no kill attribution.
The host must call `TryComplete` after combat; there is no background event loop.

The original `CombatResolver.CompleteEncounter` remains a lower-level trusted
reward operation for compatibility. Encounter-aware gameplay should use sessions
to validate objectives first. This is a single-participant encounter layer, not
a full quest system, world controller, multiplayer coordinator, or persistence
model. There is no restart, revival, or terminal failure state in this slice.
