# HyperRTS — Future Extensions (Developer-Experience backlog)

Backlog of broader integration / DX simplifications, surfaced while building the
**Authoring DX Foundation** milestone (friendly Add Component menu, inspector polish,
per-module icons, `[HelpURL]`, one-click Selection UI setup, and the UIDocument →
PanelRenderer migration). Those are done; the items below are the natural next steps and
are intentionally separate follow-ups. Pick any of these up in a new session.

## Done

- **Prefab library.** Committed `Unit`/`Building`/`RTSWorld` (camera + selection-UI rig) and `SelectionUI`
  prefabs in `Assets/Modules/Prefabs/`; drag a unit/building into a SubScene to bake a rendered, selectable entity.
- **One-step scene setup.** `GameObject ▸ HyperRTS ▸ RTS World` drops the camera + selection-UI rig prefab into
  the scene (and `▸ Selection UI` for just the UI). Editor menus renamed `RTS ▸ … → HyperRTS ▸ …`.
- **Resource Editor polish.** Added missing-icon validation and fixed the `ScritableObjects → ScriptableObjects`
  folder/path typo (duplicate-ID validation already existed).
- **Custom inspector.** `SelectableAuthoringEditor` previews the selected/base highlight colours live.
- **Asset restructure.** `Demo/` moved out of the engine to `Assets/Demo/`; `Assets/Modules/` is now engine-only.

## Candidate follow-ups

- **Rendering for factory entities (roadmap Phase 6).** Factory-spawned entities are
  currently data-only (no mesh) until a SubScene prefab is baked — the biggest
  "it doesn't work out of the box" surprise for new users. Give factories a way to attach
  a render mesh so spawned entities are visible without manual baking. (The prefab library above
  solves this for the *authored* path, not the runtime-factory path.)

- **Property drawers.** A `SelectableKind` drawer, or other targeted `[CustomPropertyDrawer]`s now that
  the attribute foundation and the first custom inspector exist.

## Pointer

These build on the Authoring DX Foundation milestone. The engine conventions for authoring
components (menu paths, icons, tooltips, help links) live in `Assets/Modules/Core/HyperRTSMenu.cs`
and are documented in `CLAUDE.md` and `docs/editor-ux.md`.
