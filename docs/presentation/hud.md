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

`PurchaseCommands` asks `PurchaseRules.FindRefusal` why a purchase is refused and sets a class on the button:
`hud-command--unaffordable`, `--locked`, `--maxed`, `--researched` or `--queued`. A refused button ignores presses but
keeps its tooltip.

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
- **Health bars** are screen-aligned quads of fixed pixel size. `OverlayRenderer.HealthBars` picks who shows one:
  everyone, damaged units, or only selected ones.
- **`CoverPreview`** rings the cover spots a move would use when squads are selected and the pointer is over open
  ground. It ignores craters, which are server only.
