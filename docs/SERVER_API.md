# Local arena HTTP API

Source: [Program.cs](../src/Relicbound.Server/Program.cs),
[ArenaSessions](../src/Relicbound.Server/ArenaSessions.cs),
[GameWorld](../src/Relicbound.Simulation/World/GameWorld.cs), and
[DTO definitions](../src/Relicbound.Contracts/WorldMessages.cs).

This is an in-memory desktop/local prototype API, not a production account or
inventory service. Use `http://127.0.0.1:5188` with the launch command in the
[arena guide](COOPERATIVE_ARENA.md). JSON uses public fields with PascalCase names.
Send `Content-Type: application/json` for commands.

## Endpoints

| Method and route | Authorization | Successful body/status | Expected rejection |
| --- | --- | --- | --- |
| `GET /health` | None | 200, `{"Status":"ready"}` | Health is process liveness, not database/client readiness |
| `POST /api/sessions` | None | 200, `SessionReply` | 409 when four players already occupy the arena |
| `GET /api/world` | Bearer token | 200, `WorldSnapshot` | 401 for missing, unknown, expired, or removed token |
| `POST /api/commands` | Bearer token | 200, `CommandReply` | 401 for invalid session; gameplay rejection is 200 with Success=false |
| `DELETE /api/session` | Bearer token | 204, no body | 401 when session is not valid |

Malformed request JSON or binding failures can be rejected by ASP.NET Core before
the handler. These are transport failures, not CommandReply gameplay outcomes.
There is no versioned error-code contract; Message is diagnostic text.

SessionReply contains `PlayerId` and `Token`. Tokens are 32 cryptographically
random bytes encoded as hex and are matched exactly. Send `Authorization: Bearer
TOKEN`. Never infer authority from a supplied player ID: commands have no attacker
field, and the server derives identity from the session.

Successful authenticated lookups refresh LastSeen. Entries inactive for more than
two minutes are removed during subsequent session operations; no independent
expiry timer runs. A dead player still occupies a slot until removal/expiry.
Tokens and characters do not survive process restart, and reconnect creates a
new identity. No account login, durable resume, rate limiting, or TLS deployment
configuration is included.

## Command envelope

```json
{
  "Sequence": 1,
  "Kind": "move",
  "X": 1,
  "Z": 0,
  "TargetId": "",
  "AllyId": "",
  "DiscoveryId": "",
  "MasteryId": ""
}
```

Sequence is a positive 64-bit integer, strictly increasing per session. Gaps are
allowed. The most recent identical retry returns its cached result without
re-executing; equality includes all command fields, even irrelevant ones. A
conflicting reuse or older sequence fails. Normal gameplay failures also consume
their sequence. Send a higher number for a corrected command. There is no retry
history beyond the most recent accepted sequence/outcome.

CommandReply contains Sequence, Success, and Message. Example success:
`{"Sequence":1,"Success":true,"Message":"Accepted."}`.

| Kind | Relevant fields | Behavior and prerequisites |
| --- | --- | --- |
| `move` | X, Z | Exactly one cardinal direction; one move/dodge per tick; coordinates within -10..10 |
| `dodge` | X, Z | Same direction validation; moves two cells, costs 10, cooldown 20 ticks, invulnerability 3 ticks |
| `interact` | None | Manhattan distance at most two from shrine (-4,-3); earns infusion once |
| `loadout` | DiscoveryId, MasteryId | Empty clears a slot; known IDs must be personally unlocked; mastery requires infusion |
| `branch` | TargetId | `ruin` or `silence`; chosen once per arena within shrine interaction range |
| `freeze` | TargetId | Living enemy within Manhattan distance five; costs 10, cooldown 20, duration 40; already-frozen targets reject |
| `attack` | TargetId, optional AllyId | Crushing Blow on living enemy within distance two; costs 10, cooldown four; supplied ally must be a player within distance four |

Known loadout IDs: `lightning-infusion`, `tempest`, `storm-guardian`. Selecting
infusion equips the prototype's supplied Stormheart; clearing infusion unequips
it. This is not item ownership. A protective attack additionally requires a
distinct living ally. Commands from dead players reject, including loadout edits.
Movement is grid-based and does not implement actor collision or pathfinding.

Chain candidates are living eligible enemies selected from those within four
cells of the primary target, ordered by distance then ordinal ID. It is a
primary-centered selection, not a spatial hop-by-hop chain. The world supplies
candidate entities and definitions; clients cannot override them.

## Snapshot contract

| Object | Fields and interpretation |
| --- | --- |
| WorldSnapshot | Tick (long), Branch (empty/ruin/silence), Actors array, Events array |
| ActorSnapshot | Id, IsPlayer, X, Z, Health, MaxHealth, Barrier, Resource, Frozen, Dodging, InfusionUnlocked, TempestUnlocked, GuardianUnlocked, MasteryProgress |
| EventSnapshot | ActionId, Tick, Sequence, ParentSequence, SourceId, TargetId, Amount, Reason |

Actors include dead enemies/players with zero health until removed by their
lifecycle rules. Actors are sorted by ordinal ID. Flags and values are snapshots,
not permission to issue arbitrary state updates. Selected loadout and detailed
cooldown readiness are not currently included.

Events retain only the latest 128 combat events across the arena. A poll can
repeat events, or miss events when the buffer rolls over. Deduplicate by
`(ActionId, Sequence)`, not Sequence alone. ParentSequence=-1 represents a seed
(the in-process CombatEvent uses null). Player ActionId is `playerId:commandSequence`;
AI uses `ai:enemyId:tick`. The wire event omits DamageType and AttackerId from the
in-process event. Amount is the planned contribution, not necessarily health loss.
This buffer is not a durable event log or a progression notification stream.

## PowerShell example

Run against a freshly started local server. This works with Windows PowerShell
without PowerShell 7-specific HTTP options. Do not print or commit tokens.

```powershell
$base = 'http://127.0.0.1:5188'
$session = Invoke-RestMethod "$base/api/sessions" -Method Post
$headers = @{ Authorization = "Bearer $($session.Token)" }
try {
    $body = @{ Sequence = 1; Kind = 'interact' } | ConvertTo-Json
    $reply = Invoke-RestMethod "$base/api/commands" -Method Post `
        -Headers $headers -ContentType 'application/json' -Body $body
    if (-not $reply.Success) { throw $reply.Message }

    $world = Invoke-RestMethod "$base/api/world" -Headers $headers
    $world.Actors | Where-Object Id -eq $session.PlayerId
}
finally {
    Invoke-RestMethod "$base/api/session" -Method Delete -Headers $headers
}
```

For two-client verification, create two sessions and query the same world with
both tokens. Interact using only the first; only that player's InfusionUnlocked
should become true. Missing and removed tokens should receive 401. The prior
implementation run performed this live smoke check. The automated
[ArenaHttpTests](../tests/Relicbound.Server.Tests/ArenaHttpTests.cs) now cover shared
state and token identity, authorization, retries, disconnect, expiry, concurrent
requests, and malformed JSON using an in-memory HTTP host. They do not replace
live network or Unity playtesting. Session time uses TimeProvider.System in
production and a manual provider in tests; the two-minute rule is unchanged.
