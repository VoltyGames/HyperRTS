# HUD and overlays

[Docs](../README.md) ▸ [Presentation](README.md) ▸ HUD

The UI Toolkit HUD, the command card, the cursor and the world-space overlays on units. For game developers adding
panels, commands or overlay styles.

## HUD

`HUD.prefab` holds the HUD. Its `HUDPanelLayouts` asset gathers the tree (`HUD.uxml`) and the row templates.
`HUDController` clones the tree, hands each panel shell to its panel, styles it with `HUD.uss` (layout) and
`HUDTheme.uss` (looks) and keeps it in sync with the simulation. A game adds or replaces panels by overriding
`CreatePanels` (`Adopt` for a shell already in the tree, `Add` for a new one).

`HUDContext` reads the selection so panels rebuild only when it changes. It also holds the shown player: the local
player, or one an observer or replay picks with `ShowPlayer`. For anyone else `CanCommand` is false: the card shows with
disabled buttons and `Issue` drops commands.

| Type | Role |
| --- | --- |
| Panels | `ResourceBar`, `SelectionPanel`, `CommandCard`, `Minimap` and others, each built from its shell in `HUD.uxml` |
| `ICommandSource` | Decides which command specs apply to the selection and shows their live state |
| `HUDText` | Every HUD string, by key, with `{0}` placeholders filled from arguments. Set `HUDText.Current` to translate; `HUDText.Keys` lists the fixed keys |
| `PlacementGhosts` | The building's real model as tinted visual-only copies, one per spot or line segment |

`HUDText` keys are kebab-cased: `hud.*`, `command.*`, `entity.<name>`, `resource.<asset>`, `ability.<name>` and so on.
An optional `entity.<name>.description` adds a tooltip line. Panels write most text once, so `HUDController` rebuilds
the HUD when Unity Localization's selected locale changes.

For other UI Toolkit text, `LocalizedText` binds an element to a string table entry and follows language changes:
`element.Localize(table, entry, args)` from code, or `<hrts:LocalizedText property="text" table="..." entry="..." />`
in UXML with `xmlns:hrts="HyperRTS.Presentation.HUD"`. On an element with the `hrts-caps` class it writes capitals,
since USS has no text case.

### Command card

The card is data. A `CommandSpec` asset describes one button: its target (`CommandTarget`: a prefab to build, train or
research, an ability by name, or an order), the card it sits on (a builder or producer prefab, empty for every card),
a label key, an icon, a `Key` hotkey, a page and a slot. A `CommandCatalog` on `HUDController` lists them. A spec with a
card beats one without for the same target, which is how one side differs from another. A gap logs an error and skips
the button. `CommandSpecRule` rejects two specs sharing a key on one page, a key an input action already uses and a
spec with no target.

The card is 15 slots, five across. A taken slot moves the entry to the next free one, and past the last to the next
page. Escape, a right click on the card or a new selection turns back a page.

`PurchaseCommands` asks `Buyer.FindRefusal` why a purchase is refused and sets a class on the button:
`hud-command--unaffordable`, `--locked`, `--maxed`, `--researched` or `--queued`. A refused button ignores presses but
keeps its tooltip.

`OrderCommands` shows plain orders when a selected unit can take them. The fire mission button appears for a unit with
a `PointAim`, and a press or its hotkey sets a `PendingCommand` of `FireMission`, so the next ground click orders it at
that point (shift keeps targeting, a right click or Escape cancels, as for an ability).

### Squads in the HUD

One selected squad adds a `SquadView` to the selection panel: a card per soldier, the suppression bar and the retreat
or reinforce status. `SquadCommands` adds Retreat and Reinforce to the card.

### Minimap

The minimap shows the baked map image, local fog, markers and the camera view. Colours are `--minimap-*` custom
properties in `Map/Minimap.uss`, which a theme overrides.

## Cursors

`CursorIntentSystem` picks the `CursorKind` each frame. A right click's result comes from `OrderResolver`, so game
`SmartOrderRule`s count. `CursorSet` is a ScriptableObject with one `CursorAnimation` per kind. Default is required and
stands in for a missing kind. `CursorDisplaySystem` shows the intent through a display the game hands it; without one
the OS pointer stays.

## Unit overlays

`OverlayRenderer` draws instanced ground markers, health bars and other markers. Colours and sizes come from an
`OverlayStyle` asset, and Play mode edits show at once.

- **Ground markers** (`GroundMarker.shader`) drape on the terrain for selected and hovered entities: a rounded
  rectangle round a vehicle, a circle round infantry, corner brackets round a building. Colours follow the relation.
- **Team rings** are thinner, fainter ground markers on the same footprint under every visible owned mobile unit,
  one per squad, in the owner's colour from `MatchView.GetColor`, the colour `HyperRTS/LitTeam` gets, so a
  `TeamColorOverride` recolours both. They draw one queue place under the selection markers, which read over them,
  and stay out of silhouettes since their shader has no unit mask pass. `OverlayRenderer.TeamRingsShown` hides them.
- **Small-unit pips** (`UnitPip.shader`) mark a mobile unit drawn shorter on screen than the style's
  `smallUnitHeight` with an owner-coloured dot in the gap above the model, under its health bar.
- **Health bars** are screen-aligned quads of fixed pixel size, which zoom doesn't change. `OverlayRenderer.HealthBars`
  picks who shows one: everyone, damaged units, or only selected ones. Sizes are pixels at 1080p, scaled by
  resolution and `OverlayRenderer.UIScale` but never below the style's `barMinHeight`. Bars and pips draw after
  upscaling, through the [screen overlay](rendering.md#screen-overlay), so they stay crisp at any resolution scale.
- **`FireMissionOverlay`** draws while a fire mission is pending, through a `GroundMarkers` on a finer grid so
  long rings follow the terrain: round each selected launcher an outline at its maximum range and a soft red disc
  inside its minimum range, and at the pointer the circle one salvo covers (`FireMissionReach`: spread plus splash
  radius). Range is measured flat from the unit's centre, as the mission order does. The circle is filled when some
  launcher reaches the point from where it stands and only an outline when each would first drive into range.
  Colours are the `fire*` fields of `OverlayStyle`.
- **`CoverPreview`** rings the cover spots a move would use when squads are selected and the pointer is over open
  ground. It ignores craters, which are server only.

## Team outlines

`TeamOutlineFeature` lines every owned unit and building on screen in its owner's colour, `width` pixels on a
1080-pixel-high screen (scaled with the screen's height) at `opacity`. It runs at the same point as the
[screen overlay](rendering.md#screen-overlay) and sits before it in the renderer's list, so health bars draw over the
lines. Two steps share one extra texture:

1. **Mask.** At the camera depth's size and against its depth and stencil, LitTeam's `HyperRTSUnitOutline` pass draws
   units on the units layer with no depth test, skipping [silhouette](rendering.md#team-colour) pixels (bit 32), so
   grass and row crops in front of a unit leave its outline whole. `HyperRTSBuildingOutline` draws buildings on the buildings layer by depth, with the
   silhouette's slack, since buildings have no silhouette. Both write the owner's colour at alpha 1, neutral entities
   none. A full-screen draw then marks the silhouette pixels at alpha 0.5.
2. **Outline.** A full-screen pass over the final target paints each empty pixel within the width of an alpha 1 pixel
   in that pixel's colour, so units of different owners keep their colours. Hidden pixels are not empty, so no line
   crosses a unit behind a wall: its silhouette shows there instead.

The mask draws with the camera's unjittered matrices, so the lines stay still under a temporal upscaler, at the cost of
following the render resolution's pixel grid. Units in the fog of war stop rendering, so they get no line; a fog memory
copy of a building keeps one. The mask costs one more draw per unit or building mesh on screen. `TeamOutlines.Hide`
turns the feature off for a player who wants no lines, and the validator offers to add it to a renderer that lacks it.
