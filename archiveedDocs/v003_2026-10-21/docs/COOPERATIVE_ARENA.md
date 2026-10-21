# Cooperative arena prototype

Related references: [architecture](ARCHITECTURE.md), [HTTP API](SERVER_API.md),
[developer runbook](DEVELOPER_RUNBOOK.md), and [current verification audit](COMPLETION_AUDIT.md).

## Verified .NET host

Run from the repository root:

```powershell
dotnet run --project src/Relicbound.Server --configuration Release --no-launch-profile -- --urls http://127.0.0.1:5188
```

The server owns one in-memory arena with up to four temporary players. Its
20 Hz clock advances the same deterministic GameWorld used by tests. HTTP polling
is the initial transport; interpolation, prediction, and production networking
are not implemented. Run locally. Restart the server to reset the encounter.

- `POST /api/sessions` returns a random session token and server-owned player ID.
- `GET /api/world` returns a detached snapshot.
- `POST /api/commands` accepts a WorldCommand.
- `DELETE /api/session` removes the player.
- Except session creation and `/health`, requests require `Authorization: Bearer TOKEN`.

Commands have strictly increasing positive sequence numbers. An identical retry
of the most recent command returns its cached outcome without executing again.
Older commands and conflicting retries are rejected. Failed gameplay commands
also consume their sequence number. This is bounded retry handling, not a durable
command history. Idle sessions expire after two minutes when another session
operation runs. Arena death does not automatically disconnect a client.

The arena lock serializes commands and ticks. Replaying the same ordered commands
and ticks reproduces snapshots; scheduling of concurrent HTTP arrivals is not itself
deterministic. The pure resolver still requires its caller to serialize access.

## Gameplay

Start at (-4, -4), beside the cyan shrine at (-4, -3). Interact to earn Lightning
Infusion, then select it in the loadout. The prototype supplies Stormheart when
infusion is selected; this is starter equipment, not inventory ownership.

Movement uses integer cardinal steps, once per tick. Dodge costs 10 resource,
moves two cells, and grants three ticks of invulnerability with a 20-tick cooldown.
Crushing Blow has range two, costs 10, and has a four-tick cooldown. Enemy AI moves
every five ticks and attacks adjacent players on a ten-tick cooldown.

Freeze has range five, costs 10, and lasts 40 ticks. Another player's physical hit
shatters the frozen enemy for 20 additional physical damage. The freezing player's
own attack does not shatter. A successful shatter consumes the freeze.

Three qualifying chain attacks plus guardian defeat unlock both Tempest and Storm
Guardian. Select one mastery. Tempest extends chains; Storm Guardian replaces them
with a 20-point barrier on a nearby living ally. Players who join after guardian
defeat cannot complete that arena's trial. Restart for another expedition.

Choose Ruin or Silence at the shrine, once per shared expedition. Ruin increases
guardian attack damage; Silence gives it a 40-point barrier and disables player
resource regeneration. These are small prototype branch effects, not the full
expedition system. Living characters normally recover one resource every ten ticks.

## Unity source: runtime verification pending

Unity Hub is present, but the user confirmed the Editor is not installed. The
client source has not been compiled by Unity or visually tested. It is an import
kit, not a complete generated Unity project.

1. Install a Unity 6 Editor through Hub and complete the appropriate license setup.
2. Create a Universal 3D project, preferably under `client/Relicbound.Playable`.
3. From the repository root, import the two source files:

   ```powershell
   .\client\Relicbound.Unity\Import-Client.ps1 -UnityProject .\client\Relicbound.Playable
   ```

4. Save an empty scene and press Play. The client creates the camera, arena,
   shrine, actors, and prototype controls automatically.
5. Start the .NET host, click Connect, and use the onscreen controls.
6. Include that scene in a desktop build and run it beside the Editor for a
   two-client test. Both use `http://127.0.0.1:5188`.

The import copies Contracts source instead of referencing a .NET 10 assembly.
Re-run the script when Contracts change; imported files should not be edited
independently. No Unity dependencies enter Core or Simulation.

The source uses Unity's [runtime initialization callback](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/runtimeinitializeonloadmethodattribute)
to bootstrap presentation. No custom input package or gameplay engine runs in
the client. Desktop localhost HTTP is the target; WebGL, remote hosting, TLS,
account login, durable reconnect, and deployment remain separate work.

## Verification

- Release solution build: zero warnings/errors.
- 144 xUnit cases pass, including retry idempotency, invalid command atomicity,
  discovery isolation, cooperative versus self freeze, snapshot isolation,
  shared branch selection, deterministic replay, and session capacity.
- Live HTTP smoke check passed for two clients, shared state, isolated discoveries,
  missing-token rejection, and disconnected-token rejection.
- Unity compilation/playtesting and SQL Server integration have not run.
