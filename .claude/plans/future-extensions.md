# HyperRTS — Future Extensions (Developer-Experience backlog)

Backlog of broader integration / DX simplifications, surfaced while building the
**Authoring DX Foundation** milestone (friendly Add Component menu, inspector polish,
per-module icons, `[HelpURL]`, one-click Selection UI setup, and the UIDocument →
PanelRenderer migration). Those are done; the items below are the natural next steps and
are intentionally separate follow-ups. Pick any of these up in a new session.

## Candidate follow-ups

- **Prefab / preset library.** Ship ready-made Unit/Building prefabs (or Unity
  `Preset` assets) so a game-builder drags one in instead of hand-assembling authoring
  components. Pairs well with the rendering item below.

- **One-call runtime setup.** A `RTSWorld.Bootstrap()` (or similar) helper that wires the
  selection input + UI in code, mirroring the `SelectionComponents.AddTo()` pattern the
  factories already share (`Units/UnitEntityFactory.cs`, `Buildings/BuildingEntityFactory.cs`).
  Goal: minimal boilerplate to stand up a playable scene.

- **Rendering for factory entities (roadmap Phase 6).** Factory-spawned entities are
  currently data-only (no mesh) until a SubScene prefab is baked — the biggest
  "it doesn't work out of the box" surprise for new users. Give factories a way to attach
  a render mesh so spawned entities are visible without manual baking.

- **Resource Editor polish.** Add duplicate-ID / missing-icon validation to the Resource
  Editor window (`Assets/Modules/Core/Editor/Resource/ResourceEditor.cs`) and fix the
  `Assets/ScritableObjects/Resources` path typo (`ScritableObjects` → `ScriptableObjects`).

- **Custom inspectors / property drawers.** Now that the attribute foundation exists, add
  targeted drawers — e.g. a `SelectableKind` drawer, or a `SelectableAuthoring` inspector
  that previews the selected/base highlight colors live.

## Pointer

These build on the Authoring DX Foundation milestone. The engine conventions for authoring
components (menu paths, icons, tooltips, help links) live in `Assets/Modules/Core/HyperRTSMenu.cs`
and are documented in `CLAUDE.md` and `docs/editor-ux.md`.
