# Developer runbook

## Prerequisites

Use the .NET 10 SDK and `Relicbound.slnx`. Commands run from the repository root
in PowerShell. NuGet access is needed for the initial restore. Unity and PostgreSQL
are not required to build or test the current .NET solution.

Unity Hub was found in the earlier environment inspection. The user last confirmed
Unity Editor and PostgreSQL were not installed; this documentation pass did not
recheck installations. There is no database connection or migration to run yet.

## Build and verify

Run individually and stop if a command fails:

```powershell
dotnet restore Relicbound.slnx
dotnet build Relicbound.slnx --configuration Release --no-restore
dotnet test Relicbound.slnx --configuration Release --no-build --no-restore
dotnet run --project src/Relicbound.Sandbox --configuration Release --no-build --no-restore
```

Expected Sandbox behavior: 40 base damage, 52 infused damage, two versus three
chain targets, objective-gated mastery, rejected cooldown/resource/locked-loadout
actions, and a 20-point barrier absorbing part of a 25-damage hit.

```powershell
dotnet run --project src/Relicbound.Sandbox --configuration Release --no-build --no-restore -- --events
dotnet test tests/Relicbound.Simulation.Tests --configuration Release --no-restore --filter FullyQualifiedName~GameWorldTests
```

The first command includes JSON events for comparative attacks mixed with readable
scenario text; it is not a pure JSONL export. The second runs a focused test suite.

## Start and stop the arena

```powershell
dotnet run --project src/Relicbound.Server --configuration Release --no-build --no-restore --no-launch-profile -- --urls http://127.0.0.1:5188
```

Keep the terminal running. Ctrl+C stops the host gracefully. Restarting resets all
sessions, actors, unlocks, and branch selection. `--no-launch-profile` avoids a
generated launch profile overriding the intended setup. This server is separate
from the preserved `RelicBound` web starter.

In another terminal:

```powershell
Invoke-RestMethod http://127.0.0.1:5188/health
```

Use the [API example](SERVER_API.md#powershell-example) or follow the
[Unity import steps](COOPERATIVE_ARENA.md#unity-source-runtime-verification-pending).
The importer copies two files into `Assets/Relicbound` of an existing Unity
project; repeated imports replace those copies. It does not create a project,
install an Editor/packages, or activate a license. Do not edit imported copies
independently of their maintained repository sources.

## Test map

| Test source | Main behavior |
| --- | --- |
| CombatResolverTests | Composition, immutability, definition validation |
| CombatActionTests | Entity damage, bounds, invalid actions |
| ChainCombatTests | Candidates, prerequisites, chain limits |
| LoadoutCombatTests | Unlocks, selected effects, trusted catalog |
| GameplayProgressionTests | Qualifying attacks and encounter-gated mastery |
| CombatPacingTests | Resources, cooldowns, logical time |
| EncounterSessionTests | Bound participants/rosters, objectives, idempotency |
| ReactionCombatTests | Cycles, FIFO order, event budget, atomic failure |
| ProtectiveMasteryTests | Barrier substitution, absorption, prerequisites, cap |
| GameWorldTests | Retries, isolation, cooperative shatter, branches, determinism, capacity |

Tests are under [Relicbound.Simulation.Tests](../tests/Relicbound.Simulation.Tests).
There is no automated HTTP integration suite or Unity test suite. Passing xUnit
does not verify Unity rendering, session expiry timing, PostgreSQL transactions,
or full gameplay balance. CI restores/builds/tests the .NET solution on Ubuntu;
it does not run Unity, PostgreSQL, Sandbox, or the live HTTP smoke scenario.
Hosted CI has not been verified.

## Troubleshooting

| Symptom | Check / expected behavior |
| --- | --- |
| Unsupported target framework | Check `dotnet --list-sdks`; solution targets .NET 10 |
| Missing package/assets | Restore first; `--no-restore` assumes prior restore |
| NuGet TLS/network failure | Check network, proxy trust, and sandbox access; do not disable certificate validation |
| Connection refused | Start Relicbound.Server at the client's URL; verify `/health` |
| Port occupied | Choose another local port and update client URL; do not stop an unrelated process |
| Session creation returns 409 | Four players occupy the arena; disconnect a client or restart the local host |
| Request returns 401 | Missing/expired token, disconnected session, or restarted host; create a new session |
| HTTP 200 but no action | Inspect CommandReply.Success and Message; gameplay failures use 200 |
| Stale/conflicting sequence | Retry only the identical latest command; use a higher number for new intent |
| Attack/loadout rejected | Check life, range, resource, cooldown, unlocks, and ally prerequisites |
| Mastery locked | Earn three damaging chain attacks AND guardian trial completion after discovery |
| Guardian dead before joining | Late joiners cannot complete its trial; restart for a fresh expedition |
| Character dead / arena cleared | No revive/reset command exists; restart for a fresh arena |
| Unity duplicate types | Import Contracts once; do not also reference a duplicate DLL/source copy |

## Handoff

Preserve the original web starter and engine-independent domain. Follow nearby
implementation/test patterns and keep feature changes narrow. Each feature should
have a demonstration, meaningful tests, and updated documentation. Record exact
verification and limitations in the [audit](COMPLETION_AUDIT.md), and unfinished
acceptance criteria in the [roadmap](ROADMAP.md). Never substitute a build for a
platform-specific playtest. Keep credentials and session tokens out of source.
