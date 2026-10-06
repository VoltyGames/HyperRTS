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
- `Stop()`: back to single player.

In Play mode use **HyperRTS ▸ Network**. Builds accept `-server`, `-host`, `-connect <address>` and
`-port <n>`; a dedicated server runs with `-batchmode -nographics -server`.

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
  stock, population, power, abilities, match state). Static data comes from the client's own copy of the prefab.
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
reveal. Ghosts without a `Faction` (players, the match) always replicate, and observers see everything. With fog
off, relevancy is disabled.

## Physics

Clicks raycast against Unity Physics, which Netcode only steps inside the prediction loop. The Match object needs
`NetCodePhysicsConfig` with **Always Run** (the validator offers the fix), and client worlds set
`PredictionLoopUpdateMode.AlwaysRun`. The server never raycasts, so `NetworkSession` disables its physics group.

## Limits

- No client-side prediction: commands take a round trip to show, like most server-based RTS.
- Enemy buildings disappear when they leave vision (no "last seen" ghosts yet).
- No host migration: if the host leaves, the match ends.
- Relay (Steam Datagram Relay, Unity Relay) plugs in through a custom Netcode driver constructor; not built in.
