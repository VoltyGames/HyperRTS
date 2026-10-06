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

`HyperRTS.Network.Session.NetworkSession` swaps the local world for client and/or server worlds, then reloads the
active scene so its SubScene streams into them:

- `StartHost(port)`: server and client in one process (custom lobbies, LAN).
- `StartServer(port)`: dedicated server, no local player.
- `StartClient(address, port)`: join a server.
- `StartRelayHost(relayData)` / `StartRelayClient(relayData)`: the same over a relay (below).
- `Stop()`: back to single player.

The `Start*` calls return `false` when the session can't start (bad address, port in use) and stay in single
player. In Play mode use **HyperRTS ▸ Network**. Builds accept `-server`, `-host`, `-connect <address>` and
`-port <n>`; a dedicated server runs with `-batchmode -nographics -server`. Relay needs runtime data from the
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

`NetworkSession.Status` (`Idle`, `Connecting`, `Connected`, `Disconnected`) and the `StatusChanged` event drive
lobby and connection UI. `NetworkStatusSystem` reads it from Netcode's connection events in the client world; a
dedicated server is `Connected` once it listens. When a connection fails or closes, the status stays
`Disconnected` with Netcode's `DisconnectReason` (timeout, closed by remote, max attempts...) until the game
calls `Stop()` or starts a new session, so the UI can show why instead of an empty world.

## Join and reconnect

1. Once the match is loaded, the client sends `JoinRequest` with its preferred slot.
2. The server binds the connection to that slot if it is free, else the first free human slot (no `AIPlayer`),
   else makes it an observer (faction 0, sees everything). It replies with `JoinAccepted`.
3. The client tags the player ghost of that faction as `LocalPlayer`, so input and HUD work unchanged.

The server links the connection to its slot (`ConnectionPlayer`). When a connection drops (read from Netcode's
connection events, since network ids are reused), its slot is freed and its units stay idle. The client remembers its slot
(`NetworkSession.PreferredFaction`) and gets it back on rejoin.

## Replication

- **Ghosts**: units, buildings, resource nodes, projectiles and the Match object need a
  `GhostAuthoringComponent` on the prefab root. **HyperRTS ▸ Validate** flags missing ones with a *Make Ghost*
  fix, and the templates and scene wizard add it.
- **Players** are baked by `MatchAuthoring`, so `PlayerGhostSystem` turns each baked player into a ghost prefab
  at runtime (the same way on both sides) and the server spawns one ghost per slot.
- **Fields**: components the client reads carry `[GhostField]` (health, faction, construction, production queue,
  stock, population, power, abilities, ammo, match state; `Stealthed`, `Inside` and `Docked` as enabled bits). Static data comes from the client's own copy of the prefab.
- **References**: prefabs and assets can't cross the wire, so `ProductionQueueItem`, `ResearchedUpgrade` and
  `ResourceStock` also carry a type id, and `ReferenceResolveSystem` fills the reference back in on the client.

A new component the HUD or overlays read needs `[GhostField]` on the fields that change, and
`[GhostEnabledBit]` if it is enableable.

## Commands

The client never runs a `PlayerCommand`. `CommandSendSystem` turns each one into a `CommandRpc` carrying ghost
references: the target, and the commanded unit or the selected units the player owns (up to 127). Netcode sends
them with their spawn tick, so a despawned ghost arrives as `Entity.Null`, never as a newer ghost reusing its id.
`CommandReceiveSystem` on the server keeps only units the sender owns and writes the command to the sender's
player entity in arrival order. A group command lists its units in the player's `PlayerCommandSubject` buffer,
which `CommandSubjects.Collect` reads before `Unit` and the selection. Everything else (cost, prerequisites,
cooldowns) is checked by the same systems as in single player.

## Fog of war

`FogRelevancySystem` sends each client only the owned ghosts its team can see, so map hacks have nothing to
reveal. Undetected stealthed enemies count as unseen, with fog on or off. Ghosts without a `Faction` (players, the
match) always replicate, and observers see everything.

## Sound

The server never plays audio. `SoundSendSystem` sends each client the frame's sound events as `SoundRpc`s (type id,
slot, owner, position): only those its team can see (the fog and stealth rule above), plus its own voices, at most
16 per tick. `SoundReceiveSystem` queues them on the client, which plays them like single player.

## Physics

Clicks raycast against Unity Physics, which Netcode only steps inside the prediction loop. The Match object needs
`NetCodePhysicsConfig` with **Always Run** (the validator offers the fix), and client worlds set
`PredictionLoopUpdateMode.AlwaysRun`. The server never raycasts, so `NetworkSession` disables its physics group.

## Limits

- No client-side prediction: commands take a round trip to show, like most server-based RTS.
- Enemy buildings disappear when they leave vision (no "last seen" ghosts yet).
- No host migration: if the host leaves, the match ends.
- No automatic reconnect: after `Disconnected` the game calls `Stop()` and starts the session again.
