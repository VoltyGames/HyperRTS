# Extending

Writing game code on top of the engine: new mechanics, HUD changes, AI, and wiring a front end. The engine stays
generic; anything that names your units or rules lives in your game's assemblies.

## Game assemblies

Mirror the engine's layering:

| Assembly | References | Holds |
| --- | --- | --- |
| `MyGame.Simulation` | `HyperRTS.Core`, `HyperRTS.Simulation`, Entities, Collections, Mathematics, Burst, Transforms, `Unity.NetCode` (for `[GhostField]`) | Components, systems, authoring. No Graphics, InputSystem or UIElements |
| `MyGame.Presentation` | the above + `HyperRTS.Presentation` | HUD panels, visuals |
| `MyGame.Network` | the above + `HyperRTS.Network` | Game RPCs (lobby) |
| `MyGame.Editor` | the above + `HyperRTS.Editor` | Validation rules, builders |

- Systems are discovered automatically. Put them in a phase group
  (`[UpdateInGroup(typeof(CombatSystemGroup))]`), never `SimulationSystemGroup`.
- A system runs on the server and in single player by default. A client-side one adds
  `[WorldSystemFilter(SimulationWorlds.Presented)]`.
- A component the client reads needs `[GhostField]` on the fields that change (`[GhostEnabledBit]` if enableable).
- Authoring classes derive from `AuthoringBehaviour` so they get the engine inspector and validation.

## Mechanics

| You want | Do |
| --- | --- |
| A new behaviour | Component + authoring + Burst `ISystem` in a phase group |
| A new order (lay mines) | `OrderType` value from `OrderType.Custom` up. Issue with `OrderWriter.Issue`. A system runs units whose `ActiveOrder` has your type, moves them with `MoveDestination`, and disables `ActiveOrder` when done |
| A new command (call reinforcements) | `CommandType` value from `CommandType.Custom` up. Append to the player's `PlayerCommand` buffer; consume in `OrderSystemGroup` |
| React to deaths (bounty) | In `LifecycleSystemGroup` after `DeathSystem`, query entities with `Dead` enabled; `LastAttacker` says who did it |
| Deal damage or heal | `DamageWriter` → `DamageEvent` before `DamageSystem`. Negative heals |
| Ability effects | An ability without built-in effects; read `AbilityActivation` after `AbilitySystem` |
| Field effects (jamming) | Read the entity's `FieldPresence` |
| Change a stat | `StatModifier` with a `StatSource` kind from `StatSourceKind.Custom` (128) up, so removing yours never strips the engine's |
| A custom stat (fuel) | `Stat` value from `Stat.Custom` (128) up. A system last in `LifecycleSystemGroup`, change-filtered on `StatModifier`, sets your value with `StatMath.Apply(modifiers, bases, stat, value)` |
| A sound | `SoundWriter.Play(entity, slot, ...)` before the end of the lifecycle phase; custom slots from `SoundSlot.Custom` |
| Spawn from code | `ecb.Instantiate(prefab)`, set `LocalTransform` and `Faction`. Prefab entities come from authoring references (`PrefabRegistry` by `TypeId`) |

## Mapping a Generals-style design

| Feature | In HyperRTS |
| --- | --- |
| Dozer | Unit + `Builder` |
| Supply truck, dock | Unit + `Harvester`; `Resource Node`; building + `Resource Drop-Off` |
| Barracks, War Factory | Building + `Producer`, `Prerequisites` |
| Power plants | Building **Power** (positive generates, negative draws) |
| Damage vs armor, weak rear armor | `Damage Type` assets + `Armor`, directional fields |
| Artillery | `Weapon` splash radius and edge damage |
| Upgrades | `Upgrade` prefabs in a producer's **Research Options** |
| Veterancy | `Veterancy` ranks; kills pay `experienceValue` |
| Oil derricks, tech buildings | Building + `Capturable`; units with `Capturer` |
| Garrisons, transports | `Container` on the building or vehicle, `Passenger` on the units |
| General's powers | `Ability` buffer on the player entity (UsePower) |
| Radar jamming, healing zones | `Area Field` + `FieldPresence` |
| Aircraft | Unit + `Flight`; `Airfield` pads; `Ammo` |

## HUD

| Change | Do |
| --- | --- |
| Add or replace a panel | Implement `IHUDPanel` (or derive `HUDPanel` for a boxed one), subclass `HUDController`, override `CreatePanels` and `Add(panel, parent)`. Put the subclass on your HUD prefab |
| Translate text | Subclass `HUDText`, override `Get(key)` and `Name(key, authored)`, set `HUDText.Current` before the HUD builds (`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`). Keys: [modules](modules.md#presentation) |
| Restyle | Override classes from `Presentation/HUD/HUD.uss` in your theme |
| Team colours | Write a `TeamColorOverride` singleton (colour-blind palettes, own / ally / enemy) |

UI: use UI Toolkit for the HUD, menus and world-space UI. uGUI only where a UI needs Animation or Timeline. No
IMGUI.

## AI

`SkirmishAISystem` is self-contained and only writes `PlayerCommand`s.

- Tune it with difficulty presets on `Match` and `AIBuildOrder` assets per side.
- Replace it: disable the system (`state.Enabled = false` from a game system, or remove `AIPlayer`) and write your
  own system that appends `PlayerCommand`s for players with `AIPlayer`.

## Front-end integration

A game shell (menus, lobby, loading) drives a match through these:

| Need | API |
| --- | --- |
| Configure players before the map loads | `MatchSetupRequest.Set(MatchSetup)`: per slot open, control, team, colour, name, AI difficulty, side; resource scale; `FogOverride` |
| Load a single-player map | `NetworkSession.StartLocal(SessionScene.Load(path))` |
| Host or join with the menu still up | `NetworkSession.StartHost(port, SessionScene.Keep)` / `StartClient(address, port, SessionScene.Keep)`, then every peer `NetworkSession.LoadScene(path)` |
| Loading progress | `NetworkSession.SceneLoad`; networked matches also wait for `MatchState.Started` |
| Connection state | `NetworkSession.Status`, `StatusChanged`, `DisconnectReason` |
| Read the match from MonoBehaviours | `MatchView.TryGetDefault(out view)`: local player, relations, match state, colours |
| Pause and speed (single player) | `LocalGameSpeed.SetScale`, `Reset` |
| Escape for a pause menu | Set `InputActionsProvider.FrontEndOwnsCancel`; call `MatchView.CancelInteraction()` first |
| Block gameplay keys under menus | `InputActionsProvider.Suspend(owner)` / `Resume(owner)` |
| Camera speed setting | `CameraController.speedMultiplier` |
| Replays | `ReplayRecorder` in the authoritative world, `ReplaySerializer.Save/Load`, `ReplayViewer.Begin` after loading `Replay.ScenePath` |

## Editor

- Validation rules: `AuthoringRule<T>`, `ISceneRule` or `IPrefabRule` in an editor assembly. They are discovered
  and show up in the inspector, **HyperRTS ▸ Validate**, Play mode and the CI test.
- Debug layers: subclass `DebugLayer` or `CellDebugLayer`.
- Scenes from code: `RTSSceneBuilder.Build(RTSSceneSpec)`, the API behind the scene wizard.

See [editor](editor.md).
