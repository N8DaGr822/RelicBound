# RELICBOUND

An engine-independent C# combat prototype. Start with the headless simulation,
then connect a Unity client once the mechanics are proven.

Start with the [documentation index](docs/README.md) for architecture, API contracts,
gameplay rules, setup, verification, and the remaining roadmap. The
[completion audit](docs/COMPLETION_AUDIT.md) separates tested behavior from unverified
Unity source and future features.

Documentation versions: [current version](docs/VERSION.md) ·
[archived versions](archiveedDocs/README.md).

## Getting started

Install the .NET 10 SDK and open `Relicbound.slnx`, or run from this directory:

```shell
dotnet restore Relicbound.slnx
dotnet build Relicbound.slnx --configuration Release --no-restore
dotnet test Relicbound.slnx --configuration Release --no-build --no-restore
dotnet run --project src/Relicbound.Sandbox
```

Combat Slice 01 composes Crushing Blow (40 physical damage) with Lightning
Infusion (12 lightning damage) for 52 total damage. Sandbox prints the ordered
contributions and their source IDs. Six xUnit tests cover base damage,
composition, repeatability, duplicate modifier rejection, definition collection
copying, and zero-damage rejection.

Milestone 1.2 adds combatants and health application through a resolver overload.
Sandbox now applies the composed attack to a 100-health target, leaving 48 health.
The original damage-only resolver remains available. Action tests cover health
bounds, invalid attacks without mutation, repeatability, and result snapshots.

Milestone 1.3 adds Stormheart (two lightning chain targets) and Tempest (one
additional target). Sandbox demonstrates the combined Stormbreaker Strike:
52 damage to the primary enemy and 12 lightning damage to each of three others.
The same Crushing Blow definition is used throughout.

Milestone 1.4 separates personal discovery/mastery unlocks, selected techniques,
and the equipped relic. The Sandbox compares two characters using Stormheart:
one chains to two enemies, while the character who earned and selected Tempest
chains to three. Locked selections are rejected before health changes.

Milestone 1.5 adds gameplay-earned progression and deterministic combat pacing.
Runic Shrine grants Lightning Infusion; three successful lightning-chain attacks
plus Storm Trial completion earn Tempest. Crushing Blow costs 10 resource and
has a two-tick cooldown. Sandbox demonstrates training, cooldown rejection,
the trial guardian, earned mastery, and insufficient-resource rejection.

Milestone 1.6 adds tracked encounter objectives. Runic Shrine requires an
interaction; Storm Trial completes only when its bound guardian is defeated.
The Sandbox now uses encounter sessions to award progression.

The cooperative arena adds server-owned movement, dodge, enemy AI, resource
regeneration, and cross-player freeze/shatter. Its host serializes commands and
ticks; direct resolver callers still need to serialize access to combatants.
Inventory ownership and persistence are not implemented.

See [cooperative arena setup](docs/COOPERATIVE_ARENA.md) for the .NET server and
Unity source import kit. The server passed live HTTP checks; Unity compilation
and playtesting remain pending installation of the Editor.

## Project boundaries

| Project | Target | Responsibility |
| --- | --- | --- |
| Relicbound.Core | .NET Standard 2.1 | Shared domain concepts and gameplay rules |
| Relicbound.Contracts | .NET Standard 2.1 | Snapshot and command DTOs shared with Unity |
| Relicbound.Simulation | .NET 10 | Authoritative state changes and combat event processing; references Core and Contracts |
| Relicbound.Sandbox | .NET 10 | Console scenario runner; references Simulation |
| Relicbound.Server | .NET 10 | Session endpoints and authoritative arena clock |
| Relicbound.Simulation.Tests | .NET 10 | Automated simulation tests; references Simulation |
| RelicBound | .NET 10 | Existing ASP.NET Core starter app, preserved without gameplay dependencies |

Nullable analysis is enabled in every project. `.editorconfig` provides basic
formatting conventions without reformatting existing code. The GitHub Actions
workflow builds and runs tests on pushes and pull requests once this folder is
hosted in a GitHub repository with Actions enabled.

## Working plan

- [Engineering kickoff v0.2](docs/ENGINEERING_KICKOFF.md): original reference, preserved unchanged.
- [Implementation progress](docs/PROGRESS.md): current status and working decisions.
- [Completion audit](docs/COMPLETION_AUDIT.md): verified prototype behavior and remaining original roadmap criteria.
- [First combat scenario](docs/FIRST_COMBAT_SCENARIO.md): Combat Slice 01 behavior and acceptance criteria.
- [Entities and health](docs/COMBAT_ENTITIES_AND_HEALTH.md): Milestone 1.2 rules and Sandbox output.
- [Relic and mastery composition](docs/RELIC_AND_MASTERY_COMPOSITION.md): Milestone 1.3 rules and limits.
- [Character unlocks and loadouts](docs/CHARACTER_UNLOCKS_AND_LOADOUTS.md): Milestone 1.4 validation rules.
- [Gameplay progression and combat pacing](docs/GAMEPLAY_PROGRESSION_AND_PACING.md): Milestone 1.5 earning conditions, deterministic time, and current Sandbox sequence.
- [Encounter objectives](docs/ENCOUNTER_OBJECTIVES.md): Milestone 1.6 interaction and defeat objectives, participant binding, and completion rules.
