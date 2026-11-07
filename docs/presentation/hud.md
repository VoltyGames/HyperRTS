# HUD and overlays

[Docs](../README.md) ▸ [Presentation](README.md) ▸ HUD

The UI Toolkit HUD, the command card, the cursor and the world-space overlays on units. For game developers adding
panels, commands or overlay styles.

## HUD

`HUD.prefab` holds the HUD. Its `HUDPanelLayouts` asset gathers the tree (`HUD.uxml`), the row templates (UXML beside
each script), the command button parts (`CommandButton.uxml`) and the drag box. `HUD.uxml` holds each panel shell as a
template instance in stacking order. `HUDController` clones the tree, hands each shell to its panel, styles it with
`HUD.uss` (layout) and `HUDTheme.uss` (looks) and keeps it in sync with the simulation. Code fills in only per-frame
data. A game adds or replaces panels by overriding `CreatePanels` (`Adopt` for a shell already in the tree, `Add` for a
new one).

`HUDContext` reads the selection every frame and hashes it (`SelectionHash`) so panels rebuild only when it changes.
Purchase buttons re-read stock, queues and prerequisites at 10 Hz and on a new selection, and each button writes only
what changed. It also holds the shown player: the local player, or one an observer or replay picks with `ShowPlayer`.
When that is not the local player, `CanCommand` is false: the card shows with disabled buttons and `Issue` drops
commands. With no shown player, only panels that need none stay (`HUDPanel.NeedsPlayer`).

Code lives in `HUD/` (controller, `HUD*` building blocks, sheets) and in `Commands/`, `Map/` and `Panels/`, each its own
namespace with UXML beside its script.

| Type | Role |
| --- | --- |
| Panels | `ResourceBar`, `SelectionPanel`, `CommandCard`, `Minimap`, `GameOverBanner`, `StrategicCountdowns`, each built from its shell in `HUD.uxml` |
| Elements | `HUDIcon`, `HUDBar`, `CostRow`, `CommandButton`, `HUDTooltip`, `HUDSweep`, `HUDGlyph`, `HUDFade`: UXML elements filled through `Bind` or properties |
| `ICommandSource` | Decides which command specs apply to the selection and shows their live state: purchases, abilities, `OrderCommands` (stances, return, unload, deploy, pack up, sell), squad retreat and reinforce |
| `HUDText` | Every HUD string, by key. Set `HUDText.Current` to translate; `HUDText.Keys` lists the fixed keys so a game can check its tables |
| `PlacementGhosts` | The building's real model as visual-only copies in the `HyperRTS/Placement Ghost` material, tinted valid or invalid, one per spot or line segment |
| `LineCost`, `PlacementHint` | The total of a dragged line (turns `hud-costs--short` when unaffordable) and the rotate hint (`hud.rotate-hint`) beside the ghost |

`HUDText` key families: `hud.*` (resource bar, strategic ready, rotate hint), `command.*`, `stance.<stance>`,
`squad.*`, `tooltip.*`, `outcome.*`, and the names `entity.<name>`, `resource.<asset>`, `ability.<name>` and
`weapon.<designation>`, all kebab-cased ("War Factory" is `entity.war-factory`). `entity.<name>.description` is an
optional tooltip line; `HUDText.Description` returns null without it.

### Command card

The card is data. A `CommandSpec` asset describes one button: its target (`CommandTarget`: a prefab to build, train or
research, an ability by name, or an order with its argument), the card it sits on (a builder or producer prefab, empty
for every card), a label key, an icon (empty takes the prefab's or ability's own), a `Key` hotkey, a page and a slot. A
`CommandCatalog` on `HUDController` lists them. A spec with a card beats one without for the same target, which is how
one side differs from another. Sources look specs up by target and card, log an error for a gap and skip the button.

The card is a grid of 15 slots, five across. A slot already taken moves the entry to the next free one, and past the
last slot to the next page. With several pages the last slot of each is the page card (More, then Back from the last).
A hotkey fires the one entry at that key on the shown page. `CommandSpecRule` rejects two specs sharing a key on one
page of a card, a key an input action already uses (except the building rotation, which acts only while placing) and a
spec with no target.

Build, train, research and ability buttons are pictures (icon, hotkey top left, cost strip); order buttons show their
label key unless the spec has an icon. Every button is built from `CommandButton.uxml`, whose parts stay hidden by
`hrts-hidden` until a source shows them. Escape, a right click on the card or a new selection turns back a page; the
page is the `CommandCardPage` client singleton.

`PurchaseCommands` asks `PurchaseRules.FindRefusal` why a purchase is refused and sets a class on the card:
`hud-command--unaffordable`, `--locked` (missing prerequisite), `--maxed` (at limit), `--researched`, and `--queued`
(a count badge and a sweep on the head). A refused card ignores presses and hotkeys but keeps its tooltip, which lists
cost, time, population, power and each missing prerequisite, on hover and on keyboard or gamepad focus.

### Squads in the HUD

One selected squad adds a `SquadView` to the selection panel: a `SquadMemberCard` per soldier (health, cover or posture,
weapon name), the suppression bar, and the retreat or reinforce status. `SquadCommands` adds Retreat and Reinforce to the
card; a refused one stays and its tooltip names the `SquadRefusal` from `SquadOrderRule`
(`CommandButton.ShowSquadRefusal`). Clients read the replicated `Retreating` and `Reinforcing` bits and progress. A
card names the member's own `Designation`, or the upgraded mount's (`WeaponPose.Designation`), through
`HUDText.WeaponName`; a squad at work shows "Building" with `SquadWork.Progress`.

### Minimap

The minimap shows the baked map image, local fog, unit and structure markers and the camera view. Colours, widths and
alphas are `--minimap-*` custom properties in `Map/Minimap.uss`, which a theme overrides. A Burst job
(`MinimapMarkerJob`) draws the markers into one texture, uploaded 10 times a second.

## Cursors

`CursorIntentSystem` picks the `CursorKind` each frame, first match wins: a scroll edge, then Default over UI, then
placement (Build, or Invalid on blocked ground), then a pending command's own cursor, then what `OrderResolver` says a
right click on the hovered entity gives each selected unit (so game `SmartOrderRule`s count; the highest wins in a mixed
selection; a plain move keeps Default), then Select for a selectable entity or Rally for ground with only producers
selected.

`CursorSet` is a ScriptableObject with one `CursorAnimation` per kind (fps, loop, frames and hotspot per pixel size).
Default is required and stands in for a missing kind. `CursorDisplay` animates the hardware cursor and picks the size
closest to `baseSize` times its `Scale`. `CursorDisplaySystem` shows the intent through a display the game hands it;
without one the OS pointer stays.

## Unit overlays

`OverlayRenderer` draws instanced ground markers, health bars, the placement footprint, rally markers, capture dials and
garrison pips. Colours and sizes come from an `OverlayStyle` asset, applied when the asset, its edit count or the pixel
scale changes, so Play mode edits show at once. Everything goes through `InstanceBuffer<T>`.

- **Ground markers** (`GroundMarker.shader`) drape on the terrain heightmap for selected and hovered entities: a
  rounded rectangle round a vehicle, a circle round infantry and squads, corner brackets round a building. Colours follow
  the relation (own, ally, enemy, neutral). They read `LocalToWorld`, so they stay on moving units, and are depth
  tested. Aircraft mark the ground.
- **Health bars** are screen-aligned quads of fixed pixel size (times `OverlayRenderer.UIScale`) above the model, with
  segments, squad member pips and veterancy pips. `OverlayRenderer.HealthBars` picks who shows one: everyone, damaged
  units, or only selected ones. A squad's bar adds a suppression strip and a cover shield (`SquadBarState`).
- **`CoverPreview`** rings the cover spots a move would use when squads are selected and the pointer is over open ground
  (`HoveredEntity.Ground`), using `CoverRules.Reach` and `CoverRules.Faces`, the same tests `SquadCoverSystem` runs.
  Craters are server only, so it ignores them.
