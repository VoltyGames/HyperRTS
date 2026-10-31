# Extending

How to build a game on the engine: new mechanics, HUD changes, AI and a front end. The engine stays generic, so
anything that names your units or rules lives in your own assemblies.

## Game assemblies

Mirror the engine's layers: a `MyGame.Simulation` on `HyperRTS.Simulation`, then Presentation, Network and Editor
assemblies on the matching engine ones. Keep Graphics, InputSystem and UIElements out of the simulation assembly.

- Systems are discovered automatically. Put each in a phase group such as `CombatSystemGroup`.
- Systems run on the server and in single player. A client-side one adds
  `[WorldSystemFilter(SimulationWorlds.Presented)]`, unless it sits under `PresentationSystemGroup`.
- A component the client reads needs `[GhostField]` on the fields that change.
- Authoring classes derive from `AuthoringBehaviour` to get the engine inspector and validation.

## Mechanics

| You want | Do |
| --- | --- |
| A new behaviour | A component, its authoring and a Burst `ISystem` |
| A new command | A `CommandType` from `GameFirst` (128) up, read with a `CommandReader` in `OrderSystemGroup` |
| Custom right-click | Add a `SmartOrderRule` to the unit in its baker |
| React to production or capture | Read `GameEvents` after `ProductionSystemGroup` |
| Change an owner | `Ownership.Transfer` |
| React to deaths | Query `Dead` entities after `DeathSystem`. `LastAttacker` names the killer |
| Damage or heal | `DamageWriter`. A negative amount heals |
| Custom ability effects | Read `AbilityActivation` after `AbilitySystem` |
| Field effects | Read the entity's `FieldPresence` |
| Disable an entity | Set a bit from `OfflineReason.Custom` up in `Offline.Reasons`. Units need `Offline` baked, disabled |
| Change a stat | Add a `StatModifier`. On the player entity it changes player-wide stats such as income |
| A sound or effect | `CueWriter.Play` before the lifecycle phase ends |
| Spawn from code | `ecb.Instantiate`. `PrefabRegistry` finds prefabs by `TypeId` |

### A new order

1. Pick an `OrderType` value from `GameFirst` (128) up in your own code. The engine never defines one there.
2. Register its traits once from a system update with `OrderTraitTable.Register`: `EngagesWhileMoving`,
   `AttackMove`, `Formation`, `MovesToPosition` (the move order runs it) or `Loops`. `OrderWriter.Issue` rejects
   a game type with none registered.
3. In a system, move units with that `ActiveOrder` using `TargetReach.Approach`, handing it a `MoveControl` built
   from the job's `MoveDestination` and `Arrival`, or set `MoveDestination` directly.
4. Disable `ActiveOrder` when the order is done.

## Mapping a Generals-style design

| Feature | In HyperRTS |
| --- | --- |
| Dozer | Unit with `Builder` |
| Supply truck and dock | `Harvester`, `Resource Node`, `Resource Drop-Off` |
| Power plants | Building **Power**: positive generates, negative draws |
| Weak rear armor | `Armor` facing multipliers |
| Upgrades, tech unlocks | `UpgradeAuthoring` research, usable as a prerequisite |
| Oil derricks | `Capturable` building with `ResourceIncome` |
| Garrisons, transports | `Container` and `Passenger` |
| General's powers | `Ability` on the player entity |
| Radar jamming, healing zones | `Area Field` |
| Aircraft | `Flight`, `Airfield` pads, `Ammo` |

## HUD

- **Panels:** derive `HUDPanel` with a UXML shell beside it, subclass `HUDController` and add panels in
  `CreatePanels`, into the slots the `PanelLayouts.Root` tree names such as `bottom`. Engine panels take
  `PanelLayouts`. Put the subclass on your HUD prefab.
- **Text:** subclass `HUDText` and set `HUDText.Current` before the HUD builds. Keys are listed in
  [client](modules/client.md#presentation).
- **Style:** `HUD.uss` holds layout and `HUDTheme.uss` the look. Assign your own sheet as
  `HUDController.themeSheet`.
- **Team colours:** write a `TeamColorOverride` singleton, for example for colour-blind palettes.

## AI

`SkirmishAISystem` only writes `PlayerCommand`s. Tune it with difficulty presets on `Match` and `AIBuildOrder`
assets. It needs the map's bot hints, which the map builder writes. To replace it, disable the system and write your
own commands for players with `AIPlayer`. Put every AI system in `AISystemGroup`, so the join hold and replays pause
it with the rest of gameplay through `GameplayPhaseGroup`.

## Front-end integration

A game shell drives a match through these calls:

| Need | API |
| --- | --- |
| Set players before the map loads | `MatchSetupRequest.Set(MatchSetup)` |
| Load a single-player map | `NetworkSession.StartLocal(SessionScene.Load(path))` |
| Host or join from a menu | `NetworkSession.StartHost` or `StartClient`, then `LoadScene` on every peer |
| Loading and connection state | `NetworkSession.SceneLoad`, `Status`, `DisconnectReason` |
| Read the match | `MatchView.TryGetDefault` |
| Command card buttons | An `ICommandSource` passed to `CommandCard` |
| Pause and speed | `LocalGameSpeed.SetScale` |
| Own the Escape key | `InputActionsProvider.FrontEndOwnsCancel` and `MatchView.CancelInteraction` |
| Block gameplay keys | `InputActionsProvider.Suspend` and `Resume` |
| Replays | `ReplayRecorder`, `ReplaySerializer`, `ReplayViewer` |

## Editor

- Validation rules show up in the inspector, **HyperRTS ▸ Tools ▸ Validate** and the CI test. See
  [validation](editor.md#validation) for the kinds.
- Debug layers subclass `DebugLayer` or `CellDebugLayer`.
- `RTSSceneBuilder.Build` creates a scene from code.

See [editor](editor.md).
