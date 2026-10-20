# Networking

HyperRTS multiplayer is **server-authoritative Netcode for Entities**. The server runs the simulation; clients
send commands and receive the state their team can see. Single player keeps one local world and none of this runs.

## Worlds

| World | Runs | Created by |
| --- | --- | --- |
| Local (single player) | Everything | Default world (`OverrideAutomaticNetcodeBootstrap` on `RTSWorld`) |
| Server | Gameplay, AI, commands, relevancy | `NetworkSession.StartServer` / `StartHost` |
| Client | Selection, input, fog view, presentation, HUD | `NetworkSession.StartClient` / `StartHost` |

The phase groups in `SystemGroups.cs` exist in every world, but their systems default to the authoritative worlds
(`SimulationWorlds.Authoritative`). A system the client also needs opts in with
`[WorldSystemFilter(SimulationWorlds.Presented)]` (selection, input, local fog) or `SimulationWorlds.All` (fog
grid, nav grid for placement previews). New gameplay systems need nothing: they run on the server and in single
player.

## Starting a match

`HyperRTS.Network.Session.NetworkSession` swaps the local world for client and/or server worlds, then loads a scene
so its SubScene streams into them:

- `StartHost(port)`: server and client in one process (custom lobbies, LAN).
- `StartServer(port)`: dedicated server, no local player.
- Hosting opens the port before anything is torn down: if it can't, the start returns false and the running
  worlds are untouched.
- `StartClient(address, port)`: join a server.
- `StartRelayHost(relayData)` / `StartRelayClient(relayData)`: the same over a relay (below).
- `Stop()`: back to single player.
- `StartLocal(scene)`: a fresh single-player world with the given scene (a game's menu or a skirmish map).

Each start takes an optional `SessionScene`: `ReloadActive` (the default, for a map played from its own scene),
`Load(path)`, or `Keep` to load nothing. A lobby connects with `Keep` while the menu stays up, then every peer calls
`LoadScene(path)` to stream the map into the running worlds. `SceneLoad` exposes the load for a progress bar.

To configure the match itself (open and closed slots, teams, colours, AI, starting resources, fog, weather) every peer sets
the same `MatchSetupRequest` before the map loads; see [modules](modules/core.md#match).

The `Start*` calls return `false` when the session can't start (bad address, port in use) and stay in single
player. In Play mode use **HyperRTS ▸ Tools ▸ Play**. Builds take command-line flags (next section). Relay needs
runtime data from the game, so it has no menu item or flag.

## Command line and dedicated server

`NetworkLauncher` reads the flags of a player build (`LaunchOptions`, never in the Editor) after the first scene loads:

| Flag | Meaning |
| --- | --- |
| `-server`, `-host`, `-connect <address>` | The session to start; one at most |
| `-port <n>` | Port to listen on or connect to (default 7979) |
| `-scene <path>` | Map to load; else `NetworkLauncher.DefaultScene` if the game set it, else the first scene again |
| `-observe` | Client only: join as an observer (`NetworkSession.JoinAsObserver`) |
| `-exit-on-end` | Server only: quit once `MatchState.Phase` is `Ended` |
| `-time-limit <minutes>` | Server only: quit after that much server time, whatever the match does |
| `-report <path>` | Server only, with one of the two above: write a `ServerReport` JSON when it quits |

A bad flag or a session that can't start logs the reason and quits with exit code 1. A dedicated server runs as
`-batchmode -nographics -server -scene <map> -exit-on-end -report <path>`; it exits 0 when the match ended and 2
on the time limit. In batch mode nothing renders, so the launcher turns vSync off and caps the frame rate at the
simulation tick rate (60 Hz) instead of spinning.

Without a graphics device (`-nographics`, `PresentationGate.HasGraphics` false) shaders, materials and compute kernels
never load, so presentation stays off: every presentation and input MonoBehaviour derives from `PresentationBehaviour`,
which disables itself in Awake before OnEnable or an update can run, and `PresentationGateSystem` switches off the
`PresentationSystemGroup` of a presented world. A server or headless client only simulates and networks.

The server's Match is a prespawned ghost, and Netcode enables prespawns only once the first client is in game, so
`MatchStartSystem` and the match start then: a server with nobody connected never starts, and a cost run needs at least
one observer.

`ServerMonitor` watches a server that ends itself. From `MatchState.Started` it samples once a second: process CPU
time per wall second (`cpuPercentOfCore`, 100 = one core busy), the process's system memory ("System Used Memory"), Unity's "Total Used Memory" profiler
counter, connections (`NetworkId` entities), kilobits per second sent by every driver (`GetStatistics().TxTotalBytes`)
and entities in the server world. The report gives mean, p95 (nearest rank) and max of each, plus the outcome, server
and match minutes, the winning team, each player's `PlayerStats` and the number of errors logged.

## Relay (player-hosted matches)

Player-hosted matches behind NAT connect through a relay. The engine takes a Unity Transport `RelayServerData`
and doesn't depend on any service: the game gets the allocation from Unity Relay, Steam or its own backend,
shares the join code through its lobby, then starts the session.

```csharp
// Host: create an allocation (UGS: RelayService.Instance.CreateAllocationAsync), publish its join code.
NetworkSession.StartRelayHost(allocation.ToRelayServerData("dtls"));
// Client: join with the code (UGS: RelayService.Instance.JoinAllocationAsync).
NetworkSession.StartRelayClient(joinAllocation.ToRelayServerData("dtls"));
```

`RelayDriverConstructor` replaces Netcode's default drivers before the world listens or connects: the host's
server listens on the relay and over IPC, and its own client joins over IPC, so the host never pays the relay
round trip. A Steam or custom relay that isn't Unity Relay plugs in as its own `INetworkStreamDriverConstructor`
(e.g. a Steam Networking Sockets `INetworkInterface`); keep it in the game, since the engine ships no
third-party SDK.

## Connection status

`NetworkSession.Status` (`Idle`, `Connecting`, `Connected`, `Disconnected`) drives lobby and connection UI; poll
it from a `MonoBehaviour` update, outside any world update, so a reaction can stop the session. `NetworkStatusSystem` reads it from Netcode's connection events in the client world; a
dedicated server is `Connected` once it listens. When a connection fails or closes, the status stays
`Disconnected` with Netcode's `DisconnectReason` (timeout, closed by remote, max attempts...) until the game
calls `Stop()` or starts a new session, so the UI can show why instead of an empty world.

## Match start

`MatchStartSystem` holds a networked match on the server until every human slot (no `AIPlayer`) has a connection,
so the AI and the first players to load don't get a head start. The order phase keeps running so player ghosts spawn
and joins bind; movement, combat, production, lifecycle and the skirmish AI wait, and `CommandReceiveSystem`
drops every player command, so nothing in the order phase (placing, selling, abilities) acts early. After
`MatchRules.JoinTimeout` seconds (the Match's **Join Timeout**, 60 by default) it starts anyway. A match of only AI
starts as soon as its player ghosts spawn. It then sets
`MatchState.Started`, which client loading screens wait for. Single-player matches never hold.

## Join and reconnect

1. Once the match is loaded, the client sends `JoinRequest` with its preferred slot and its token.
2. The server picks the slot with `SlotClaim`: the slot that token owns, else the preferred slot if it was never
   claimed, else the first never-claimed human slot (no `AIPlayer`), else none, which makes the client an observer
   (faction 0, sees everything). A client that set `NetworkSession.JoinAsObserver` (`JoinPreference.Observe`)
   always becomes an observer. The server replies with `JoinAccepted`.
3. The client tags the player ghost of that faction as `LocalPlayer`, so input and HUD work unchanged.

**Identity.** `NetworkSession.Token` is a random non-zero `ulong` made once per process; every client world gets it
as its `JoinToken` singleton. The server keeps the token of the client that claimed a slot on its `PlayerConnection`.
When a connection drops (read from Netcode's connection events, since network ids are reused), its slot's
`NetworkId` goes back to 0 but the token stays, so the slot waits for its owner and its units stay idle; a stranger
only takes slots never claimed. If the owner comes back before the old connection timed out, the new connection
gets the slot and the stale one is disconnected. A join with token 0 owns nothing, and its slot is free again once
it drops.

**Rejoin.** The client world asks for the slot in its `JoinPreference` singleton, seeded from
`NetworkSession.PreferredFaction` and `JoinAsObserver`. When a client world closes, both take what it was granted
(an observer stays an observer), or what it asked for if the server never answered. `NetworkSession.Rejoin(scene)`
starts a client again to the last `StartClient` endpoint (`CanRejoin` says whether there is one). The scene must load
the map again (`SessionScene.Load`), since a SubScene only streams into worlds that exist when it loads. A relay
session rejoins through a new allocation the game gets from its service, then `StartRelayClient` with the map scene.

## Replication

- **Ghosts**: units, buildings, resource nodes and projectiles need a `GhostAuthoringComponent` on the prefab root.
  **HyperRTS ▸ Tools ▸ Validate** flags missing ones with a *Make Ghost* fix, and the templates add it. The Match object stays
  a plain scene object, because the players it bakes become ghosts of their own; its replicated state (`MatchState`)
  is the engine's `MatchState` ghost prefab, which the scene wizard places in every map. In single player,
  `LocalGhostActivationSystem` enables such prespawned ghosts, which Netcode would otherwise leave disabled.
- **Map objects**: a map's own buildings (neutral houses, capture posts, derricks) and resource stacks are ghosts
  placed in the SubScene, so Netcode prespawns them on both sides and replicates owner, health, capture
  (`CaptureProgress`), garrison (`Cargo`) and death. Give them Static optimization: they send only when they change.
  Their ruins are ghosts the server spawns. Other scenery (trees, fences, bridges, wrecks) is no ghost: the server
  sends its changes by `SceneryId` (`SceneryChange`: id, state byte and, for toppling scenery, a `Fall` heading
  byte from `ToppleMath`) and every client spawns its remains itself, in the same pose the server derives from that
  byte. `SceneryRpc` carries up to 100 changes (10 bytes each); a change for scenery not yet streamed in waits whole.
  A fall the client saw happen is shown tipping over; one found later (fog, a late join) shows lying down.
- **Late sight**: `LateSpawnEffectSystem` disables the `SpawnEffect` of a ghost first shown more than a second after
  its spawn tick (a late join, a ruin found in fog), so only witnessed collapses throw dust.
- **Players** are baked by `MatchAuthoring`, so `PlayerGhostSystem` turns each baked player into a ghost prefab
  at runtime (the same way on both sides) and the server spawns one ghost per slot.
- **Fields**: components the client reads carry `[GhostField]` (health, faction, construction, production queue,
  stock, population, power, abilities, ammo, capture progress, cargo, match state; `Stealthed`, `Inside` and `Docked` as enabled bits). Static data comes from the client's own copy of the prefab.
- **References**: prefabs and assets can't cross the wire, so `ProductionQueueItem`, `CompletedResearch` and
  `ResourceStock` also carry a type id, and `ReferenceResolveSystem` (`Network/Replication/`) fills the reference
  back in on the client, finding prefabs through the `PrefabRegistry` singleton. A game buffer that names prefabs
  implements `IPrefabReference` (`[GhostField] TypeId`, `[GhostField(SendData = false)] Prefab`) and resolves them
  in its own client system with `PrefabReferences.Changed<T>` and `PrefabReferences.Resolve<T>`.

A new component the HUD or overlays read needs `[GhostField]` on the fields that change, and
`[GhostEnabledBit]` if it is enableable.

## Bandwidth

The server ticks the simulation at 60 Hz and sends snapshots at `NetworkSession.SnapshotRate` (20 Hz). Netcode fills
each snapshot up to about 1.4 KB and sends the ghosts that waited longest first, so a full snapshot makes updates
older instead of making packets bigger.

Measured on a 400 m map, host and client in the Editor, 500 tanks spawned through the Play page's **Armies** cheat:

| Case | Snapshot rate | Client receives | Unit update age |
| --- | --- | --- | --- |
| 500 tanks marching, fog on (~340 visible) | 60 Hz | 600-780 kbps | 6 ticks max |
| 640 units visible, fog off | 60 Hz | 690-750 kbps (cap) | 9 ticks max |
| 650 units visible, fog off | 20 Hz | ~240 kbps | 13 ticks max |
| 1,060 units visible, fog off | 20 Hz | ~230 kbps (cap) | 28 ticks max (~470 ms) |
| 500 tanks in combat, fog on or off | 20 Hz | peak 316 kbps | 18 ticks max |

At 20 Hz a client needs about 0.3 Mbps in a large battle, so a player hosting a 4v4 uploads about 2.2 Mbps.
Combat adds about 80 kbps of cue RPCs on top of the capped snapshots.

## Commands

The client never runs a `PlayerCommand`. `CommandSendSystem` turns each one into a `CommandRpc` carrying ghost
references: the target, and the commanded unit or the selected units the player owns (up to 127); a prefab travels
as its `EntityInfo.TypeId` and the server looks it up in the `PrefabRegistry`. Netcode sends ghost references with
their spawn tick, so a despawned ghost arrives as `Entity.Null`, never as a newer ghost reusing its id.
`CommandReceiveSystem` on the server keeps only units the sender owns and writes the command to the sender's
player entity in arrival order. A group command lists its units in the player's `PlayerCommandSubject` buffer,
which `PlayerCommands.Collect` reads before `Unit` and the selection. Everything else (cost, prerequisites,
cooldowns) is checked by the same systems as in single player.

## Fog of war

`FogRelevancySystem` sends each client only the owned ghosts its team can see, so map hacks have nothing to
reveal. Undetected stealthed enemies count as unseen, with fog on or off. Ghosts without a `Faction` (players, the
match) always replicate, and observers see everything. A client remembers enemy buildings it saw where it last saw
them (`FogMemorySystem`), so a ghost leaving relevancy doesn't vanish from its map.

## Cues

The server never plays sounds or effects. Every transient event (fire, impact, death, ability, ready, voices) is one
`CueEvent` stream; a landed shot is a `CueSlot.Impact` cue carrying its radius, surface and material.
`CueSendSystem` sends each client the cues its team may see (`CueRules`: the fog and stealth rule above, plus its own
voices) as `CueRpc` batches of up to 48 `PackedCue`s, at most 3 batches per tick. A `PackedCue` is the replay's 18-byte
format: type id, position in 1/16 m steps within ±2048 m, radius, slot, owner, yaw byte, 7-bit pitch with a stealth
flag bit, surface, material. `CueReceiveSystem` unpacks them into the client's `CueQueue`, which plays them like single
player.

The sender uses `FactionConnections`: `FactionConnectionsSystem` groups the in-game connections by viewer
faction once per tick (observers under neutral), and each sender filters and packs once per faction, then queues the
same batches to every connection of that faction.

## Physics

Clicks raycast against Unity Physics, which Netcode only steps inside the prediction loop. The Match object needs
`NetCodePhysicsConfig` with **Always Run** (the validator offers the fix), and client worlds set
`PredictionLoopUpdateMode.AlwaysRun`. The server never raycasts, so `NetworkSession` disables its physics group.

## Limits

- No client-side prediction: commands take a round trip to show, like most server-based RTS.
- No host migration: if the host leaves, the match ends.
- No automatic reconnect: after `Disconnected` the game calls `Rejoin` (or `StartRelayClient`) or `Stop()`.
- The token lives as long as the process: a client that restarts gets a new one, so its slot stays reserved and it
  rejoins as an observer.
