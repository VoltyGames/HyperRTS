# Editor authoring

How to write authoring components and their inspectors, and how editor layouts, icons and settings are built. For the tools
themselves see [editor](README.md).

## Authoring conventions

```csharp
[AddComponentMenu(HyperRTSMenu.Entity + "Combat/Weapon")]
[Icon(HyperRTSIcons.WeaponAuthoring)]
[HelpURL(HyperRTSDocs.Combat)]
[DisallowMultipleComponent]
public class WeaponAuthoring : AuthoringBehaviour
{
    [Tooltip("Distance at which idle units spot enemies; 0 uses the vision range.")]
    [Min(0f)]
    public float acquireRange;

    private class Baker : Baker<WeaponAuthoring> { ... }
}
```

- Derive from `AuthoringBehaviour`, in games too, so the inspector and validation find the component.
- Give every field a `[Tooltip]` (a sentence), guard values with `[Min]` or `[Range]`, and put the unit in
  `[Unit("m")]`, not the tooltip. `[Header]` starts a foldout group, so give each group one.
- Bakers write through `EntityWriter`, so tests build the same entities.
- Paths come from `HyperRTSMenu`, `HyperRTSIcons` and `HyperRTSDocs`. A component that only lives under a parent takes
  `[AddComponentMenu("")]`, and its parent an `[InspectorButton]` that adds it as a child (`PartButtons`).
- Each component has an icon: a white [game-icons.net](../../Packages/com.hyperrts.engine/Editor/Icons/Credits.md) glyph in
  `Editor/Icons/Components/Source/<ComponentClass>.svg` (black background path removed, row in `Credits.md`), a
  `HyperRTSIcons` constant, and its name in a module list in `ComponentIconGenerator`, which sets the tile colour. Run
  **Component icons** to draw the PNG. A game does the same with its own generator calling
  `ComponentIconGenerator.WriteIcon`.
- `[Owner]` on an int shows a dropdown of the Match's players. `[RequiresAuthoring(typeof(T), "why")]` declares a
  needed sibling, and validation offers to add it. `[ShowIf(nameof(field))]` hides a field while a sibling bool is
  false, a number 0 or less, or a reference empty; it works inside nested blocks.
- Field checks need no rule of their own: `[PrefabAsset]` wants a prefab asset, `[GhostPrefab]` a ghost prefab,
  `[NoEmptyEntries]` flags list entries the baker skips. `FieldAttributeRule` reads them, with fixes. A rule that finds
  a problem in one field passes its property path (`path:`) to `ValidationIssues.Warn` or `Error`, and the inspector
  marks that field.

## Inspectors

`AuthoringEditor` is the inspector for every `AuthoringBehaviour`. A strip on top counts the selection's validation
issues; click it for the list and quick fixes. Each `[Header]` starts a foldout (open state remembered in
`EditorState`), `[Unit]` fields show their unit inside the input, and an issue with a property path puts a red or yellow
edge on that field.

Units and buildings (`GameEntityAuthoringEditor`) start with an `EntityCard` (icon, name, role, owner colour, key stats
from `EntitySummary.Stats`). Under it the `ModuleStrip` has a chip per authoring component with a dot for its worst
issue: a click shows only that component, a second shows all, and **+** opens `AddComponentPopup`, a searchable list of
the components the object lacks.

`[InspectorButton("Fill from model", Glyph = "wand-sparkles")]` on a parameterless method of the component draws a
button under its fields, or on a static editor method taking the component, for work that needs editor code. A click
runs on every selected object as one undo step; an `InvalidOperationException` shows as an error under the buttons.

A component needs its own editor only for UI a button can't give, or handles. Subclass `AuthoringEditor` and override
`BuildHeader`, `BuildFooter` or `Refresh`; for handles use `GroundHandles.EditBox` or `EditRadius` in `OnSceneGUI`.
Scene handles already show vision, radius, footprints, weapon reach, spawn points and map bounds in the owner's colour.

Editor layouts are `.uxml` beside their scripts, found by `EditorAssets.Layout(type, part)` (`<Type><part>.uxml`, throws
when missing). Only root windows, inspectors and drawers link the shared `Editor/Common/HyperRTSEditor.uss`; pages and
parts take it from their window and link only their feature sheet. A `[UxmlElement]` with its own sheet adds it with
`EditorAssets.Sheet(name)`. The shared sheet holds the layout classes and colour tokens (`--hrts-border`,
`--hrts-dim-text`, `--hrts-error`, `--hrts-warning`), and the shared controls are `Section`, `ValueList` and
`PageShell`. Rows built from data clone a row template with `CloneRoot()`. Toggle visibility with `SetHidden` and
`SetInvisible`, not inline styles.

Glyphs are [Lucide](https://lucide.dev) SVGs in `Editor/Icons/Glyphs/`: `GlyphElements.CreateGlyph(name)` makes an
element, `SetGlyph(name)` swaps a recycled row's. To add one, copy the icon from the `lucide-static` package's `icons/`
folder to `Glyphs/<PascalName>.svg`, delete its licence comment and `class` attribute, set `stroke="#ffffff"` and add
its class to `Glyphs.uss`.

To edit a JSON file in an inspector or window, derive from `JsonDocument`, a hidden ScriptableObject holding the parsed
value for Undo. Override `Read`, `Write`, `WriteOver` (writes only what differs) and `Canonical`; edit through
`Serialized` and `Apply`, or `Record` from code. `MapSpecDocument` is the example.

## Editor state

`EditorState`, a `ScriptableSingleton` in `UserSettings/HyperRTS.asset`, keeps per-user, per-project editor settings:
last page and tab, filters, debug layers, cheat amounts, launcher choice and so on. A layout binds fields with
`binding-path` and `EditorState.Bind(root)`; defaults, `[Min]` and `[Range]` live on the fields, never in UXML. Code
that sets a value calls `Save()`. Values that must reset each session stay in `SessionState`.

## Hierarchy and Project rows

`Editor/Rows/` decorates two built-in windows. In the Hierarchy, a row of an object with a `ProducibleAuthoring` shows
its icon at the right edge, a 2 px edge in the owner's colour and a red or amber dot for a component rule error or
warning (UI Toolkit item events, `HierarchyWindow.BindViewItem`). The Project window offers only
`projectWindowItemOnGUI`, the second IMGUI exception after Scene view `Handles`: in the grid view a prefab whose root
`ProducibleAuthoring` has an `icon` shows it over the thumbnail. **Preferences ▸ HyperRTS** has the two switches.
