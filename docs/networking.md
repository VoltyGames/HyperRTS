# Networking

HyperRTS multiplayer is server-authoritative Netcode for Entities. The server runs the simulation. Clients send
commands and receive only the state their team can see. Single player keeps one local world and none of this runs.

## Worlds

| World | Runs | Created by |
| --- | --- | --- |
| Local | Everything, in single player | The default world |
| Server | Gameplay, AI, commands, relevancy | `NetworkSession.StartServer` or `StartHost` |
| Client | Selection, input, fog view, presentation, HUD | `NetworkSession.StartClient` or `StartHost` |

Systems run on the server and in single player by default, so a new gameplay system needs nothing. A system the
client also needs opts in with `[WorldSystemFilter(SimulationWorlds.Presented)]`, or `SimulationWorlds.All` if the
server needs it too.

## Starting a match

`NetworkSession` replaces the local world with client and server worlds, then loads a scene so its SubScene
streams into them.

| Call | Starts |
| --- | --- |
| `StartHost(port)` | Server and client in one process |
| `StartServer(port)` | A dedicated server with no local player |
| `StartClient(address, port)` | A client joining a server |
| `StartRelayHost`, `StartRelayClient` | The same over a [relay](#relay) |
| `StartLocal(scene)` | A fresh single-player world with the given scene |
| `Stop()` | Back to single player |

A start returns `false` when it can't begin, for example on a bad address or a port in use. The running worlds are
then left untouched.

Each start takes an optional `SessionScene`:

- `ReloadActive` is the default, for a map played from its own scene.
- `Load(path)` loads another scene.
- `Keep` loads nothing. A lobby connects this way while its menu stays up. Every peer then calls `LoadScene(path)`
  to stream the map in, and `SceneLoad` reports progress.

To configure slots, teams, colours, AI, resources, fog and weather, every peer sets the same `MatchSetupRequest`
before the map loads. See [Match](modules/core.md#match).

In Play mode, start sessions from **HyperRTS ▸ Tools ▸ Play**. Player builds use command-line flags. Relay needs
runtime data from the game, so it has neither.

## Command line and dedicated server

`NetworkLauncher` reads these flags in a player build after the first scene loads. The Editor ignores them.

| Flag | Meaning |
| --- | --- |
| `-server`, `-host`, `-connect <address>` | The session to start. One at most |
| `-port <n>` | Port to listen on or connect to. Default 7979 |
| `-scene <path>` | Map to load. Else `NetworkLauncher.DefaultScene`, else the first scene again |
| `-observe` | Client only: join as an observer |
| `-exit-on-end` | Server only: quit when the match ends |
| `-time-limit <minutes>` | Server only: quit after this much server time |
| `-report <path>` | Server only, with one of the two above: write a JSON report on quit |

A bad flag or a failed start logs the reason and quits with exit code 1. A typical dedicated server:

```text
-batchmode -nographics -server -scene <map> -exit-on-end -report <path>
```

It exits with 0 when the match ends and 2 on the time limit. In batch mode the launcher turns vSync off and caps the
frame rate at the 60 Hz simulation tick, so the process doesn't spin.

Without a graphics device, presentation stays off. Every presentation and input MonoBehaviour derives from
`PresentationBehaviour`, which disables itself in `Awake`. `HeadlessModeSystem` switches off the presentation group.
A server or headless client only simulates and networks.

A server with nobody connected never starts its match. The match state is a prespawned ghost, and Netcode enables
prespawns only once the first client is in game. A cost run therefore needs at least one observer.

## Server cost

`ServerMonitor` watches a server started with `-exit-on-end` or `-time-limit`. From match start it samples once a
second:

- CPU time per wall second, where 100 % is one busy core
- process memory and Unity's total used memory
- connections and entities in the server world
- kilobits per second sent

The `-report` JSON holds the mean, p95 and max of each sample, plus the outcome, server and match minutes, the
winning team, each player's `PlayerStats` and the number of errors logged.

## Relay

Player-hosted matches behind NAT connect through a relay. The engine takes a Unity Transport `RelayServerData` and
depends on no service. The game gets the allocation from Unity Relay, Steam or its own backend and shares the join
code through its lobby.

```csharp
// Host: create an allocation, publish its join code.
NetworkSession.StartRelayHost(allocation.ToRelayServerData("dtls"));
// Client: join an allocation with that code.
NetworkSession.StartRelayClient(joinAllocation.ToRelayServerData("dtls"));
```

The host's own client joins over IPC, so the host never pays the relay round trip. A relay other than Unity Relay,
such as Steam Networking Sockets, plugs in as its own `INetworkStreamDriverConstructor`. Keep it in the game: the
engine ships no third-party SDK.

## Connection status

`NetworkSession.Status` is `Idle`, `Connecting`, `Connected` or `Disconnected`. Poll it from a MonoBehaviour
`Update`, outside any world update, so the reaction can stop the session. A dedicated server is `Connected` once it
listens.

After a failure or close the status stays `Disconnected`, with Netcode's `DisconnectReason`, until the game calls
`Stop()` or starts a new session. The UI can then show why the connection ended.

## Match start

The server holds a networked match until every human slot has a connection, so the AI and the fastest loaders get
no head start. While it waits, the order phase runs so player ghosts spawn and joins complete. Movement, combat,
production and the AI wait, and every player command is dropped.

After the Match's **Join Timeout**, 60 s by default, it starts anyway. A match of only AI starts as soon as its
player ghosts spawn. Starting sets `MatchState.Started`, which client loading screens wait for. Single player never
holds.

## Join and reconnect

1. Once the map is loaded, the client sends a join request with its preferred slot and its token.
2. The server picks a slot: the one this token owns, else the preferred slot if never claimed, else the first
   never-claimed human slot. With none left, or if the client asked to observe, it joins as an observer. Observers
   have faction 0 and see everything.
3. The client marks that slot's player ghost as `LocalPlayer`, so input and HUD work unchanged.

**Identity.** `NetworkSession.Token` is a random number made once per process. The server stores it with the slot
the client claims. When the connection drops, the slot keeps the token and waits for its owner, with its units idle.
Strangers only take slots nobody has claimed. If the owner returns before the old connection times out, the new
connection takes over and the stale one is closed.

**Rejoin.** `NetworkSession.Rejoin(scene)` reconnects to the last `StartClient` endpoint. `CanRejoin` says whether
there is one. The client asks for the slot it held before, and an observer stays an observer. Pass the map with
`SessionScene.Load`, because a SubScene only streams into worlds that exist when it loads. A relay session rejoins
through a new allocation and `StartRelayClient`.

## Replication

- **Ghosts.** Units, buildings, resource nodes and projectiles need a `GhostAuthoringComponent` on the prefab root.
  The templates add it, and **HyperRTS ▸ Tools ▸ Validate** offers a *Make Ghost* fix.
- **Match.** The Match object stays a plain scene object. Its replicated state lives on the `MatchState` ghost
  prefab, which the scene wizard places in every map. The players it bakes become ghosts at runtime, one per slot.
- **Map buildings.** Neutral buildings, capture points and resource stacks placed in the map are prespawned ghosts.
  Give them Static optimization so they send only when they change. Their ruins are ghosts the server spawns.
- **Scenery.** Trees, fences, bridges and wrecks are not ghosts. The server sends each change as a scenery id and a
  state byte, and every client spawns the remains itself. A fall the client saw tips over. One found later
  shows already lying down.
- **Late sight.** A ghost first seen more than a second after it spawned skips its spawn effect, so only witnessed
  collapses throw dust.
- **Fields.** Components the client reads carry `[GhostField]`, and enableable ones `[GhostEnabledBit]`. Static data
  comes from the client's own copy of the prefab.
- **Prefab references.** Prefabs can't cross the wire, so buffers that name one also carry its type id, and the
  client looks the prefab up in the `PrefabRegistry`. A game buffer does the same by implementing
  `IPrefabReference` and resolving in its own client system with `PrefabReferences.Resolve<T>`.

A new component the HUD or overlays read needs `[GhostField]` on the fields that change.

## Bandwidth

The server ticks at 60 Hz and sends snapshots at `NetworkSession.SnapshotRate`, 20 Hz. Netcode fills each snapshot
up to about 1.4 KB and sends the longest-waiting ghosts first. A full snapshot makes updates older, not bigger.

Measured on a 400 m map, host and client in the Editor, with 500 tanks from the Play page's **Armies** cheat:

| Case | Snapshot rate | Client receives | Unit update age |
| --- | --- | --- | --- |
| 500 tanks marching, fog on, ~340 visible | 60 Hz | 600 to 780 kbps | 6 ticks max |
| 640 units visible, fog off | 60 Hz | 690 to 750 kbps, capped | 9 ticks max |
| 650 units visible, fog off | 20 Hz | ~240 kbps | 13 ticks max |
| 1,060 units visible, fog off | 20 Hz | ~230 kbps, capped | 28 ticks max, ~470 ms |
| 500 tanks in combat, fog on or off | 20 Hz | peak 316 kbps | 18 ticks max |

At 20 Hz a client needs about 0.3 Mbps in a large battle, so a player hosting a 4v4 uploads about 2.2 Mbps. Combat
adds about 80 kbps of cues on top of the capped snapshots.

## Commands

Clients never run a `PlayerCommand`. They send it to the server as an RPC with the target and the units it
addresses, up to 127. A unit that despawned meanwhile arrives as `Entity.Null`, never as a different unit. The
server keeps only units the sender owns and writes the command to the sender's player entity in arrival order.
Cost, prerequisites and cooldowns are checked by the same systems as in single player.

## Fog of war

Each client receives only the owned ghosts its team can see, so a map hack has nothing to reveal. Undetected
stealthed enemies count as unseen, even with fog off. Ghosts without a `Faction`, such as players and the match,
always replicate. Observers see everything. A client remembers enemy buildings where it last saw them, so a ghost
leaving relevancy doesn't vanish from its map.

## Cues

The server never plays sounds or effects. Every transient event, such as a shot, impact, death or voice line, is a
`CueEvent`. The server sends each client the cues its team may see, under the same fog and stealth rule, packed at
18 bytes each. The client plays them exactly as single player does.

## Physics

Clicks raycast against Unity Physics, which Netcode only steps inside the prediction loop. The Match object needs
`NetCodePhysicsConfig` set to **Always Run**, and the validator offers the fix. The server never raycasts, so its
physics stays off.

## Limits

- No client-side prediction: a command shows after a round trip, as in most server-based RTS games.
- No host migration: if the host leaves, the match ends.
- No automatic reconnect: after `Disconnected` the game calls `Rejoin`, `StartRelayClient` or `Stop()`.
- The token lives as long as the process. A client that restarts gets a new one and rejoins as an observer.
