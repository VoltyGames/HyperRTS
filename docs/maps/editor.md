# Map Editor

**HyperRTS ▸ Map Editor** (Ctrl+Shift+M) opens on a gallery of the project's maps, then lays out one spec on a
top-down canvas. For the spec itself, see [maps](README.md).

Open a spec by double-clicking a `.map.json` or with **Open** on its card; **Maps** in the toolbar returns to the
gallery. A card shows the map's overview picture, mode, size and the last build's errors and warnings, and **Out of
date** marks a spec saved since its last build. A card's **Open scene** opens the built scene and **Build** builds the
map. The gallery's toolbar has **Build all** and **New blank scene...** (map size, player count and starting base).

The file stays the source. Every finished edit, undo and redo saves it at once, and a save changes only what was
edited, so the file diffs like a hand edit. A file written from outside reloads when Unity imports it, which clears
the undo history.

| Part | Shows |
| --- | --- |
| Toolbar | **Maps**, the map's name, a button per tool, **Ghosts** for the symmetry copies, **Auto preview**, **Preview** and **Build** |
| Options strip | What the active tool does, and for a drawing tool the feature kind it adds |
| Layers panel | Every feature by category, with a search field. A category has an eye (hidden: not drawn or picked) and a lock (drawn, not pickable) |
| Canvas | The latest preview under a metre grid, with bounds, sector edges, features, ghosts and the selection's handles |
| Feature tab | The selected feature's fields, with dropdowns for library keys |
| Issues tab | The newest preview's or build's errors and warnings. Clicking one that names a feature selects and frames it |
| Report tab | Land figures and build metrics, with **Open scene** and **Recapture views** |

| Tool | Key | Does |
| --- | --- | --- |
| Select | V | Click selects, drag moves, drag a handle to move a point or a circle's radius. Alt+click on a selected line or area adds a point, Ctrl+click a point removes it, Delete removes the feature |
| Point | P | Each click adds the kind picked in the options strip at the cursor |
| Line | L | Clicks add points; Enter or a double-click finishes (2 or more), Escape drops it |
| Area | A | The same for areas (3 or more points) |

A new feature copies the last one of its kind, or takes the defaults in an empty list. Each gesture is one undo step.
On the canvas, middle-drag or Space+drag pans, the wheel zooms and F fits the map. With **Auto preview** on, the map
previews when it opens and after each change, at most once a second. **Build** saves open scenes, builds, reopens
your scene and shows the report.

## Authoring rules

- **Author one sector** ([symmetry](README.md#symmetry)). A point outside it would overlap its own copies, so the
  editor rings it as a warning.
- **Shared features sit on the axis**, or at the centre for a rotation. The editor snaps them there.
- **Array order drives the randomness.** The editor warns when a deletion reshuffles later features.
