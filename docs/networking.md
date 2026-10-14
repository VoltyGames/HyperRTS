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

To configure the match itself (open and closed slots, teams, colours, AI, starting resources, fog) every peer sets
the same `MatchSetupRequest` before the map loads; see [modules](modules/core.md#match).

The `Start*` calls return `false` when the session can't start (bad address, port in use) and stay in single
player. In Play mode use **HyperRTS ▸ Tools ▸ Play**. Builds accept `-server`, `-host`, `-connect <address>` and
`-port <n>`, plus `-scene <path>` to pick the map; a dedicated server runs with
`-batchmode -nographics -server -scene <map>`. Relay needs runtime data from the
game, so it has no menu item or flag.

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
`MatchRules.JoinTimeout` seconds (the Match's **Join Timeout**, 60 by default) it starts anyway. It then sets
`MatchState.Started`, which client loading screens wait for. Single-player matches never hold.

## Join and reconnect

1. Once the match is loaded, the client sends `JoinRequest` with its preferred slot.
2. The server binds the connection to that slot if it is free, else the first free human slot (no `AIPlayer`),
   else makes it an observer (faction 0, sees everything). A client that set `NetworkSession.JoinAsObserver`
   (`JoinPreference.Observe`) always becomes an observer. The server replies with `JoinAccepted`.
3. The client tags the player ghost of that faction as `LocalPlayer`, so input and HUD work unchanged.

The server links the connection to its slot (`ConnectionPlayer`). When a connection drops (read from Netcode's
connection events, since network ids are reused), its slot is freed and its units stay idle. The client world asks for the slot in its `JoinPreference` singleton, seeded
from `NetworkSession.PreferredFaction`, which takes the granted slot when the client world closes, so a rejoin gets it
back.

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
match) always replicate, and observers see everything.

## Cues and impacts

The server never plays sounds or effects. `CueSendSystem` sends each client the cues its team may see (`CueRules`: the
fog and stealth rule above, plus its own voices) as `CueRpc` batches of up to 48 `PackedCue`s (type id, slot, owner,
position, yaw byte, 7-bit pitch with a stealth flag bit; 20 bytes each), at most 2 batches per tick.
`CueReceiveSystem` unpacks them into the client's `CueQueue`, which plays them like single player. Impacts go as `ImpactRpc` batches of up to
64 `PackedImpact`s (the replay's 15-byte format: type id, position in 1/16 m steps within ±2048 m, radius, owner,
surface, material), at most 1 batch per tick.

## Physics

Clicks raycast against Unity Physics, which Netcode only steps inside the prediction loop. The Match object needs
`NetCodePhysicsConfig` with **Always Run** (the validator offers the fix), and client worlds set
`PredictionLoopUpdateMode.AlwaysRun`. The server never raycasts, so `NetworkSession` disables its physics group.

## Limits

- No client-side prediction: commands take a round trip to show, like most server-based RTS.
- Enemy buildings disappear when they leave vision (no "last seen" ghosts yet).
- No host migration: if the host leaves, the match ends.
- No automatic reconnect: after `Disconnected` the game calls `Stop()` and starts the session again.
