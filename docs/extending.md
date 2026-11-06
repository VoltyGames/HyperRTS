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
- Bakers add components through `this.Writer()`, an `EntityWriter` that a static `Add...` on the authoring class takes, so tests build the same entity with `new EntityWriter(entityManager, entity)`.

## Mechanics

| You want | Do |
| --- | --- |
| A new behaviour | A component, its authoring and a Burst `ISystem` |
| A new command | A `CommandType` from `GameFirst` (128) up, read with a `CommandReader` in `OrderSystemGroup` |
| Custom right-click | Add a `SmartOrderRule` to the unit in its baker |
| React to production or capture | Read `GameEvents` after `ProductionSystemGroup` |
| Change an owner | `OwnerChanged.Transfer` |
| React to deaths | Query `Dead` entities after `DeathSystem`. `LastAttacker` names the killer |
| Damage or heal | `EventWriter<DamageEvent>`, built with a `DamageProfile`'s `Hit` or `DamageEvent.Direct`. A negative amount heals |
| A weapon | `WeaponAuthoring`: `stats` is the primary, `extraWeapons` more. Code reads them as the `WeaponMount` buffer, primary first |
| A new projectile kind | A `ProjectileKind` flag from `GameFirst` (16) up, set on the weapon's `projectileKind` and listed in an interceptor weapon's `interceptKinds` |
| Custom ability effects | Read `AbilityActivation` after `AbilitySystem` |
| Aura effects | Read the entity's `AuraPresence` |
| Disable an entity | Set a bit from `OfflineReason.Custom` up in `Offline.Reasons`. Units need `Offline` baked, disabled |
| Change a stat | Add a `StatModifier`. On the player entity it changes player-wide stats such as income |
| A new stat | See [a new stat](#a-new-stat) |
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

### A new stat

1. Pick a `Stat` value from `GameFirst` (128) up in your own code, such as `const Stat Jamming = (Stat)128`.
2. Mark the float field it drives with `[StatField(Jamming)]` on your component. The component must sit on a unit or
   building when it spawns: `StatBaseSystem` records the field as the base then, and `StatSystem` rewrites it whenever
   the entity's modifiers change. One field per stat; a second, or a field that is not a float, throws on startup.
3. Grant it like any stat: a `StatBonus` on an upgrade, veterancy rank, aura or status effect, or a `StatModifier`
   from code.

A stat read at the moment of use instead of stored, like `Income` or `DamageTaken`, needs no field: evaluate it where
it is used with `StatMath.Evaluate(modifiers, stat, baseValue)`.

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
  `CreatePanels`, into the slots the `PanelLayouts.Root` tree names such as `bottom`. Engine panels take
  `PanelLayouts`. Put the subclass on your HUD prefab.
- **Text:** subclass `HUDText` and set `HUDText.Current` before the HUD builds. Keys are listed in
  [client](modules/client.md#presentation).
- **Style:** `HUD.uss` holds layout and `HUDTheme.uss` the look, both read from custom properties declared on
  `.hud-root`. Both are indexes that import one sheet per panel, beside its UXML (`Panels/SquadView.uss` and
  `Panels/SquadViewTheme.uss`, for example). Assign your own sheet as `HUDController.themeSheet`: it imports the engine `HUDTheme.uss` and overrides
  the properties it wants on `.hud-root`, plus any rule of its own.
  - Layout, in `HUD.uss`: `--hud-inset` (edge gap of the top bar and bottom row), `--hud-panel-padding`,
    `--hud-minimap-size`, `--hud-bottom-height` (command card height), `--hud-icon-size` (selected unit icon),
    `--hud-slot-size` (queue slot), `--hud-group-size` (group tile), `--hud-resource-icon-size`, `--hud-resource-gap`.
  - Text and lines: `--hud-text`, `--hud-text-muted`, `--hud-resource-amount` (resource amount colour), `--hud-line`
    (panel border), `--hud-line-strong` (slot and tooltip border).
  - Fills: `--hud-panel-bg`, `--hud-tooltip-bg`, `--hud-overlay-bg` (banner, countdowns, line cost, hint),
    `--hud-badge-bg` (count and lock badges), `--hud-tile-bg` (icon tile shown while it has no picture).
  - Corners: `--hud-radius` (panels, tooltip), `--hud-radius-small` (tiles, cards, slots), `--hud-radius-tiny` (bars,
    badges).
  - Command cards: `--hud-card-bg`, `--hud-card-bg-hover`, `--hud-card-bg-active`, `--hud-card-line`,
    `--hud-card-line-hover`, `--hud-card-picture-bg` and `--hud-card-picture-bg-hover` (cards with a picture),
    `--hud-card-active-line`, `--hud-card-active-bg`, `--hud-card-queued-line`, `--hud-card-queued-bg`.
  - Signals: `--hud-warn` (low power, unpowered, ready countdown), `--hud-danger` (full storage, short cost, defeat).
  - Bars: `--hud-bar-health`, `--hud-bar-build`, `--hud-bar-progress`; each `HUDBar` fill reads `--bar-fill`, so a
    rule can set that on any bar.
  - Drag box, declared on `.hud-drag-box` because it sits beside `.hud-root`: `--hud-drag-fill`, `--hud-drag-line`,
    `--hud-drag-corner`.
  - The font is not a property, since the engine sets none: set `-unity-font-definition` on `.hud-root` and on the
    labels a game wants in another face.
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
| Command card buttons | A `CommandSpec` asset in the `CommandCatalogue`, shown by an `ICommandSource` passed to `CommandCard` |
| Pause and speed | `LocalGameSpeed.SetScale` |
| Own the Escape key | `InputActionsProvider.FrontEndOwnsCancel` and `MatchView.CancelInteraction` |
| Block gameplay keys | `InputActionsProvider.Suspend` and `Resume` |
| Replays | `ReplayRecorder`, `ReplaySerializer`, `ReplayViewer` |

## Editor

- Validation rules show up in the inspector, **HyperRTS ▸ Tools ▸ Validate** and the CI test. See
  [validation](editor.md#validation) for the kinds.
- Debug layers subclass `DebugLayer` or `CellTextureLayer`.
- `RTSSceneBuilder.Build` creates a scene from code.

See [editor](editor.md).
