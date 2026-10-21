# Roadmap and decisions

Current as of October 21, 2026. The user authorized the broader roadmap through
Unity, multiplayer, persistence, loot, expeditions, trading, and a companion site.
The full roadmap is not complete.

## Decisions reflected in code

| Decision | Reason / consequence |
| --- | --- |
| Unity client, .NET simulation/server, future Blazor companion | Rendering stays separate from gameplay execution |
| SQL Server for planned persistence | User's existing SQL Server experience; supersedes the earlier PostgreSQL proposal; not implemented |
| Core and Contracts target .NET Standard 2.1 | Shared compatibility boundary; Simulation uses .NET 10 |
| Immutable definitions, execution in code | Compose attacks without a separate Stormbreaker implementation |
| Integer damage and logical ticks | Predictable arithmetic and repeatable ordered simulations |
| Character-local discovery/mastery | Equipment assignment does not grant or transfer unlocks |
| One discovery and one mastery selection | Small initial loadout model |
| Three damaging chain attacks plus trial | Explicit mastery earning fixture; both mastery alternatives share prerequisites |
| Event budget and activation limits | Cycles terminate or reject atomically |
| One serialized arena with temporary sessions | Establish authority/cooperation before production infrastructure |
| HTTP commands and polling | Initial transport; not a final latency/feel decision |
| Loadout swaps allowed while alive | No out-of-combat restriction exists |
| Preserve original web starter | Its future role is undecided; it is not the companion portal |

Numbers are experimental. Sandbox uses a two-tick Crushing Blow cooldown while
GameWorld uses four. These are separate fixtures, not a global content registry.

## Remaining phases and acceptance criteria

| Area | Current state | Work required before completion |
| --- | --- | --- |
| Headless combat | Implemented/tested | Broader progression/encounter event coverage |
| Unity presentation | Source import kit only | Editor compile, visual inspection, desktop build, two-client playtest and cleanup checks |
| Authoritative cooperation | Host/arena implemented; prior live HTTP check passed | Automated HTTP auth/retry/expiry/concurrency tests; latency/disconnect/match-lifecycle playtests |
| Persistent characters | Not implemented | Durable identity, save/load/versioning, restart recovery, failure semantics, SQL Server integration tests |
| Items and loot | Not implemented | Unique instance IDs, trusted power rolls, ownership, idempotent rewards, durable inventory |
| Expeditions | One arena with Ruin/Silence effects | Repeatable lifecycle, completion/failure/re-entry rules, loot connection, branch outcome tests |
| Barter | Not implemented | Tier/power eligibility, consent, ownership checks, atomic swap, idempotency, journal, concurrency/rollback tests |
| Blazor companion | Not implemented | First dashboard/profile/build-planner feature and secure account/API integration |
| Release/operations | Local build; CI configuration exists | Hosted CI proof, deployment, account/transport security, observability, backup/recovery, release acceptance |

Unity Editor was last confirmed absent; SQL Server installation status has not
been checked. These do not prevent all design or source work. Persistence, loot,
and trading are also unimplemented;
they are not merely waiting for tests.

## Proposed persistence and trading boundary

SQL Server is the selected persistence database as of October 21, 2026, based on
the user's existing experience. This supersedes the earlier PostgreSQL proposal;
the original kickoff and archived documentation remain historical references.
Authoritative ownership remains required. No schema, migrations, repository,
ORM/provider, or connection settings exist yet. Choose these during implementation
rather than describing a speculative schema as installed.

A relic definition describes mechanics. An item instance should carry stable
identity, rolls, ownership, and location. Server-derived intrinsic power should
remain separate from personal mastery or market price. The kickoff proposes
one-for-one, compatible-tier barter with:

```text
Max(A.Power, B.Power) > 0
AND Abs(A.Power - B.Power) / Max(A.Power, B.Power) <= Tolerance
```

Tolerance 0.15 is a proposed experiment, not an implemented/validated rule. A
transaction must revalidate both owners and eligibility, move both instances
atomically, and journal once. Tests must prove replay cannot execute twice and
concurrent trades cannot spend one item twice. Personal mastery must not transfer.
Monetary exchange, blockchain, wallets, and tokenomics are outside prototype scope.

## Decisions for later phases

- Durable account/character identity, reconnect, and any account-wide progression.
- Death/revival/reset and participation/reward rules for late joins.
- Final resource economy, cooldowns, equipment slots, and loadout-swap restrictions.
- Versioned content loading, save migrations, and definition ownership.
- Trade consent, expiry, cancellation, and compatible tier definitions.
- Companion site's first useful feature and whether to extend the starter app.
- Latency targets, transport/prediction, and deployment environment.

Revisit prototype decisions when requirements or playtest evidence justify it.
Preserve the [kickoff](ENGINEERING_KICKOFF.md) as historical input and update this
roadmap as decisions become tested behavior.
