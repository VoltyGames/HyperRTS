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
- Bakers add components through `this.Writer()`, an `EntityWriter` that a static `Add...` on the authoring class
  takes, so tests build the same entity with `new EntityWriter(entityManager, entity)`.

## Mechanics

| You want | Do |
| --- | --- |
| A new behaviour | A component, its authoring and a Burst `ISystem` |
| A new command | A `CommandType` from `GameFirst` (128) up, read with a `CommandReader` in `OrderSystemGroup` |
| A new order | See [a new order](#a-new-order) |
| A new stat | See [a new stat](#a-new-stat) |
| Custom right-click | Add a `SmartOrderRule` to the unit in its baker |
| React to production or capture | Read `GameEvents` after `ProductionSystemGroup` |
| React to deaths | Query `Dead` entities after `DeathSystem`. `LastAttacker` names the killer |
| Change an owner | `OwnerChanged.Transfer` |
| Damage or heal | `EventWriter<DamageEvent>`, built with a `DamageProfile`'s `Hit` or `DamageEvent.Direct`. A negative amount heals |
| A weapon | `WeaponAuthoring`: `stats` is the primary, `extraWeapons` more. Code reads the `WeaponMount` buffer, primary first |
| A new projectile kind | A `ProjectileKind` flag from `GameFirst` (16) up, set on the weapon and listed in an interceptor's `interceptKinds` |
| Ability or aura effects | Read `AbilityActivation` after `AbilitySystem`, or the entity's `AuraPresence` |
| Disable an entity | Set a bit from `OfflineReason.Custom` up in `Offline.Reasons`. Units need `Offline` baked, disabled |
| Change a stat | Add a `StatModifier`. On the player entity it changes player-wide stats such as income |
| A sound or effect | `CueWriter.Play` before the lifecycle phase ends |
| Spawn from code | `ecb.Instantiate`. `PrefabRegistry` finds prefabs by `TypeId` |

### A new order

1. Pick an `OrderType` from `GameFirst` (128) up in your own code.
2. Register its traits once from a system update with `OrderTraitTable.Register`: `EngagesWhileMoving`,
   `AttackMove`, `Formation`, `MovesToPosition` or `Loops`. `OrderWriter.Issue` rejects a game type with none.
3. In a system, move units with that `ActiveOrder` using `TargetReach.Approach`, or set `MoveDestination` directly.
4. Disable `ActiveOrder` when the order is done.

### A new stat

1. Pick a `Stat` from `GameFirst` (128) up, such as `const Stat Jamming = (Stat)128`.
2. Mark the float field it drives with `[StatField(Jamming)]`. The component must be on a unit or building at spawn:
   `StatBaseSystem` records the field as the base then, and `StatSystem` rewrites it when the entity's modifiers
   change. One field per stat; a second, or a non-float field, throws on startup.
3. Grant it with a `StatBonus` on an upgrade, veterancy rank, aura or status effect, or a `StatModifier` from code.

A stat read at the moment of use, like `Income` or `DamageTaken`, needs no field: call
`StatMath.Evaluate(modifiers, stat, baseValue)` where it is used.

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
| Radar jamming, healing zones | `Aura` |
| Aircraft | `Flight`, `Airfield` pads, weapon ammo |

## HUD

- **Panels:** derive `HUDPanel` with a UXML shell beside it, subclass `HUDController` and add panels in
  `CreatePanels`, into the slots the `PanelLayouts.Root` tree names such as `bottom`. Put the subclass on your HUD
  prefab.
- **Text:** subclass `HUDText` and set `HUDText.Current` before the HUD builds. Keys are listed in
  [client](presentation/README.md#presentation).
- **Style:** `HUD.uss` holds layout and `HUDTheme.uss` the look, both read from custom properties declared on
  `.hud-root` (`--hud-panel-bg`, `--hud-line`, `--hud-warn`, `--hud-card-bg` and so on). Each imports one sheet per
  panel, beside its UXML. Assign your own sheet as `HUDController.themeSheet`: it imports the engine
  `HUDTheme.uss`, overrides the properties it wants on `.hud-root` and adds rules of its own. The engine sets no
  font, so set `-unity-font-definition` on `.hud-root` and on labels that want another face.
- **Team colours:** write a `TeamColorOverride` singleton, for example for colour-blind palettes.

## AI

`SkirmishAISystem` only writes `PlayerCommand`s. Tune it with difficulty presets on `Match` and `AIBuildOrder`
assets. It needs the map's bot hints, which the map builder writes. To replace it, disable the system and write your
own commands for players with `AIPlayer`. Put every AI system in `AISystemGroup`, so the join hold and replays pause
it with the rest of gameplay.

## Front-end integration

A game shell drives a match through these calls:

| Need | API |
| --- | --- |
| Set players before the map loads | `MatchSetupRequest.Set(MatchSetup)` |
| Load a single-player map | `NetworkSession.StartLocal(SessionScene.Load(path))` |
| Host or join from a menu | `NetworkSession.StartHost` or `StartClient`, then `LoadScene` on every peer |
| Loading and connection state | `NetworkSession.SceneLoad`, `Status`, `DisconnectReason` |
| Read the match | `MatchView.TryGetDefault` |
| Hand a scene object to systems | `SceneSingleton<T>.Write` each frame, `Remove` when disabled |
| Command card buttons | A `CommandSpec` asset in the `CommandCatalog`, shown by an `ICommandSource` passed to `CommandCard` |
| Pause and speed | `LocalGameSpeed.SetScale` |
| Own the Escape key | `InputActionsProvider.FrontEndOwnsCancel` and `MatchView.CancelInteraction` |
| Block gameplay keys | `InputActionsProvider.Suspend` and `Resume` |
| Replays | `ReplayRecorder`, `ReplaySerializer`, `ReplayViewer` |

## Editor

- Validation rules show up in the inspector, **HyperRTS ▸ Tools ▸ Validate** and the CI test; see
  [validation](editor/README.md#validation).
- Debug layers subclass `DebugLayer` or `CellTextureLayer`.
- `SceneBuilder.Build` creates a scene from code.
