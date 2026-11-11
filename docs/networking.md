# Networking

HyperRTS multiplayer is server-authoritative Netcode for Entities. The server runs the simulation. Clients send
commands and receive only the state their team can see. Single player keeps one local world and none of this runs.
For the worlds table and which systems run where, see [architecture](architecture.md#worlds).

## Host

A host is one Netcode single-world host: it serves remote clients and is the local player's client. It holds every
ghost, so its player sees state as single player does: commands go straight onto the player entity and cues play from
the world's own queue.

- Its player's connection is Netcode's `LocalConnection`, which has no `NetworkStreamConnection`. `ServerJoinSystem`
  seats it from the world's `JoinPreference` and `JoinToken`, without a request.
- Systems marked `[RemoteClientOnly]` mirror a remote server (command sending, cue and scenery receiving, prefab
  reference resolving, prediction toggling), so the host world leaves them out.
- Cue, scenery and fog relevancy senders only serve connections with a `NetworkStreamConnection`.

## Starting a match

`NetworkSession` replaces the local world with a host, server or client world, then loads a scene so its SubScene
streams into it.

| Call | Starts |
| --- | --- |
| `StartHost(port)` | A host world: server and local player in one world |
| `StartServer(port)` | A dedicated server with no local player |
| `StartClient(address, port)` | A client joining a server |
| `StartRelayHost`, `StartRelayClient` | The same over a [relay](#relay) |
| `StartLocal(scene)` | A fresh single-player world with the given scene |
| `Stop()` | Back to single player |

A start returns `false` when it can't begin (a bad address, a port in use) and leaves the running worlds untouched.

Each start takes an optional `SessionScene`: `ReloadActive` (the default, for a map played from its own scene),
`Load(path)`, or `Keep`, which loads nothing so a lobby can connect while its menu stays up. Every peer then calls
`LoadScene(path)` to stream the map in, and `SceneLoad` reports progress.

Every peer passes the same `MatchSetup` in its `SessionScene` (`SessionScene.Load(path).WithSetup(setup)`); the session writes it into each world it creates, or into the running worlds for `LoadScene`, before the map loads.
It configures slots, teams, AI, resources, fog and weather. See [Match](simulation/core.md#match). In Play mode, start sessions from **HyperRTS ▸ Tools ▸ Play**.

`NetworkSession.Status` is `Idle`, `Connecting`, `Connected` or `Disconnected`. Poll it from a MonoBehaviour
`Update`, outside any world update, so the reaction can stop the session. After a failure the status stays
`Disconnected`, with Netcode's `DisconnectReason`, until the game calls `Stop()` or starts a new session.

## Command line and dedicated server

`NetworkLauncher` reads these flags in a player build after the first scene loads. The Editor ignores them.

| Flag | Meaning |
| --- | --- |
| `-server`, `-host`, `-connect <address>` | The session to start. One at most |
| `-port <n>` | Port to listen on or connect to. Default 7979 |
| `-scene <path>` | Map to load. It replaces the path of `NetworkLauncher.DefaultScene` and keeps its setup; with neither set, the first scene loads again |
| `-observe` | Client only: join as an observer |
| `-exit-on-end` | Server only: quit when the match ends |
| `-time-limit <minutes>` | Server only: quit after this much server time |
| `-report <path>` | Server only, with one of the two above: write a JSON cost report on quit |

A bad flag or a failed start logs the reason and exits with 1. A typical dedicated server:

```text
-batchmode -nographics -server -scene <map> -exit-on-end -report <path>
```

It exits with 0 when the match ends and 2 on the time limit.

Without a graphics device, presentation stays off: every presentation and input MonoBehaviour derives from
`PresentationBehaviour`, which disables itself in `Awake`, and `HeadlessModeSystem` switches off the presentation
group. A server with nobody connected never starts its match, because Netcode enables prespawned ghosts only once the
first client is in game. A cost run needs at least one observer.

`ServerMonitor` watches a server started with `-exit-on-end` or `-time-limit`. Each second from match start it
samples CPU, memory, connections, entities and kbps sent. The `-report` JSON holds the mean, p95 and max of each,
plus the outcome, durations, winning team, each player's `PlayerStats` and the logged error count.

## Relay

Player-hosted matches behind NAT connect through a relay. The engine takes a Unity Transport `RelayServerData` and
depends on no service: the game gets the allocation from its provider and shares the join code through its lobby.

```csharp
NetworkSession.StartRelayHost(allocation.ToRelayServerData("dtls"));
NetworkSession.StartRelayClient(joinAllocation.ToRelayServerData("dtls"));
```

The host's own player is in the host world, so it never pays the relay round trip. Another relay, such as Steam
Networking Sockets, plugs in as its own `INetworkStreamDriverConstructor` kept in the game: the engine ships no
third-party SDK.

## Match start

The server holds a networked match until every human slot has a connection, so the AI and the fastest loaders get no
head start. While it waits, player ghosts spawn and joins complete, but movement, combat, production and the AI wait
and player commands are dropped. After the Match's **Join Timeout** (60 s by default) it starts anyway. An AI-only
match starts as soon as its player ghosts spawn. Starting sets `MatchState.Started`, which client loading screens
wait for. Single player never holds.

## Join and reconnect

1. Once the map is loaded, the client sends a join request with its preferred slot and its token.
2. The server picks a slot: the one this token owns, else the preferred slot if never claimed, else the first
   never-claimed human slot. With none left, or if the client asked to observe, it joins as an observer. Observers
   have owner 0 and see everything.
3. The server sets the slot's player ghost's `GhostOwner` to the connection, and Netcode tags the ghost
   `LocalPlayer` on that client, so input and HUD work unchanged. An observer owns no ghost: the server answers with
   `ObserverAccepted` and the client makes a `LocalObserver`.

A connection only carries RPCs until it joins. The global `NetcodeConfig` asset therefore needs **Automatic State
Replication** off and **Physics Run Mode** set to User Controlled, and `NetworkSession` throws otherwise. Gameplay
has no physics, so Netcode must not step it on every prediction tick; a server world switches physics off and a
presented world steps only its own (see [physics](presentation/README.md#physics)).

**Identity.** `NetworkSession.Token` is a random number made once per process, stored with the slot the client
claims. When the connection drops, the slot keeps the token and waits for its owner, with its units idle. If the
owner returns before the old connection times out, the new one takes over and the stale one is closed.

**Rejoin.** `NetworkSession.Rejoin(scene)` reconnects to the last `StartClient` endpoint (`CanRejoin` says whether
there is one), asking for the slot held before; an observer stays an observer. Pass the map with
`SessionScene.Load`, because a SubScene only streams into worlds that exist when it loads. A relay session rejoins
through a new allocation and `StartRelayClient`.

## Replication

- **Ghosts.** Units, buildings, resource nodes and projectiles need a `GhostAuthoringComponent` on the prefab root.
  The templates add it, and **HyperRTS ▸ Tools ▸ Validate** offers a *Make Ghost* fix.
- **Fields.** Components the client reads carry `[GhostField]`, and enableable ones `[GhostEnabledBit]`. Static data
  comes from the client's own copy of the prefab.
- **Match.** The Match object stays a plain scene object. Its replicated state lives on the `MatchState` ghost
  prefab, which the scene wizard places in every map. The players it bakes become ghosts at runtime, one per slot.
- **Map buildings.** Neutral buildings, capture points and resource stacks placed in the map are prespawned ghosts
  with Static optimization, so they send only when they change.
- **Scenery.** Trees, fences, bridges and wrecks are not ghosts. The server sends each change as a scenery id and a
  state byte (plus the broken mask of a sectioned piece), and every client spawns the remains itself. A fall the
  client saw tips over; one found later shows already lying down. A team hears of a change only once it sees the
  spot.
- **Late sight.** A ghost first seen more than a second after it spawned skips its spawn effect.
- **Prefab references.** Prefabs can't cross the wire, so buffers that name one also carry its type id, and the
  client looks it up in the `PrefabRegistry`. A game buffer does the same by implementing `IPrefabReference` and
  resolving with `PrefabReferences.Resolve<T>` in its own client system.

## Bandwidth

The server ticks at 60 Hz and sends snapshots at `NetworkSession.SnapshotRate`, 20 Hz. Netcode fills each snapshot to
about 1.4 KB and sends the longest-waiting ghosts first, so a full snapshot makes updates older, not bigger.

Measured with 500 tanks from the Play page's **Armies** cheat on a 400 m map:

| Case | Snapshot rate | Client receives | Unit update age |
| --- | --- | --- | --- |
| 500 tanks marching, fog on, ~340 visible | 60 Hz | 600 to 780 kbps | 6 ticks max |
| 650 units visible, fog off | 20 Hz | ~240 kbps | 13 ticks max |
| 1,060 units visible, fog off | 20 Hz | ~230 kbps, capped | 28 ticks max, ~470 ms |
| 500 tanks in combat | 20 Hz | peak 316 kbps | 18 ticks max |

A client needs about 0.3 Mbps in a large battle, so a player hosting a 4v4 uploads about 2.2 Mbps. Fog relevancy runs
as a parallel Burst job every tick, under the profiler marker `FogRelevancySystem.Hide`.

## Commands

Clients never run a `PlayerCommand`. They send it as a `CommandRpc` with its target and, for a single unit or
producer, that unit. A command for a group addresses the selection the client sent before it:

- `CommandSendSystem` sends the group as `SelectionRpc`s only when it differs from the last one sent, 127 units an
  RPC: the first replaces the server's copy, the rest append.
- The server keeps each remote player's selection in its `PlayerSelection` buffer, with only the units it owns. A
  seat taken by a new connection starts empty.
- Each RPC type serializes in its own system, so a command waits one tick behind a new selection. Each carries the
  selection's version so the server can match them.

A unit that despawned meanwhile arrives as `Entity.Null`, never as a different unit. The server writes commands to
the sender's player entity in arrival order, and cost, prerequisites and cooldowns are checked by the same systems as
in single player.

Receivers take RPCs through `RpcInbox<T>`, which destroys their request entities. Senders queue them with
`Rpc.Send`; `Entity.Null` means every remote connection. All RPCs share Netcode's one reliable ordered pipeline,
since Netcode 7.2 generates no serializer for out-of-band RPCs.

## Fog of war and cues

Each client receives only the owned ghosts its team can see, so a map hack has nothing to reveal. Undetected
stealthed enemies count as unseen, even with fog off. Ghosts without an `Owner`, such as players and the match,
always replicate. Observers see everything. A client remembers enemy buildings where it last saw them.

The server never plays sounds or effects. Every transient event (shot, impact, death, voice line) is a `CueEvent`,
sent to each client under the same fog and stealth rule at 18 bytes each. The client plays them as single player
does.

## Limits

- No client-side prediction: a command shows after a round trip.
- No host migration: if the host leaves, the match ends.
- No automatic reconnect: after `Disconnected` the game calls `Rejoin`, `StartRelayClient` or `Stop()`.
- The token lives as long as the process. A client that restarts gets a new one and rejoins as an observer.
