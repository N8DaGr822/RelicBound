# RELICBOUND engineering documentation

Reviewed against repository source on **October 21, 2026**. This index describes
the current implementation; the kickoff and milestone documents also preserve
earlier decisions. Placeholder combat values are not final balance decisions.

## Start here

| Need | Document |
| --- | --- |
| Build, test, diagnose, and hand off the project | [Developer runbook](DEVELOPER_RUNBOOK.md) |
| Understand project boundaries and state ownership | [Architecture](ARCHITECTURE.md) |
| Run the server and import the Unity client | [Cooperative arena](COOPERATIVE_ARENA.md) |
| Implement a client or inspect commands and responses | [Server API](SERVER_API.md) |
| Understand reactions, event ordering, and protective mastery | [Reactions and barriers](REACTIONS_AND_BARRIERS.md) |
| See evidence of completion and verification limits | [Completion audit](COMPLETION_AUDIT.md) |
| Continue the authorized broader work | [Roadmap and decisions](ROADMAP.md) |
| Follow implementation history | [Progress log](PROGRESS.md) |

## Combat references

These describe successive implementation slices. Their test counts and statements
about deferred features apply to those slices; use the audit for current status.

| Slice | Reference |
| --- | --- |
| Base attack and infusion | [First combat scenario](FIRST_COMBAT_SCENARIO.md) |
| Health and action snapshots | [Entities and health](COMBAT_ENTITIES_AND_HEALTH.md) |
| Offensive relic/mastery composition | [Relic and mastery composition](RELIC_AND_MASTERY_COMPOSITION.md) |
| Personal unlocks and selected loadouts | [Character unlocks and loadouts](CHARACTER_UNLOCKS_AND_LOADOUTS.md) |
| Earned progression, costs, and cooldowns | [Progression and pacing](GAMEPLAY_PROGRESSION_AND_PACING.md) |
| Interaction and defeat objectives | [Encounter objectives](ENCOUNTER_OBJECTIVES.md) |

The [original engineering kickoff](ENGINEERING_KICKOFF.md) is preserved unchanged
as a design reference. Its unchecked backlog is not a live completion tracker.

## Documentation maintenance

For a behavior change, update its feature reference and relevant API examples.
Record meaningful outcomes in PROGRESS, and put current verification evidence in
COMPLETION_AUDIT. Record new design decisions and unfinished acceptance criteria in
ROADMAP. Keep planned behavior explicitly separate from implemented behavior.
Do not mark a platform verified merely because its source was written.
