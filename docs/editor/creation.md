# Creation menus

How the editor makes scene objects, prefabs and data assets. Other editor tools are in [editor](README.md).

## Menus and the New Entity wizard

**GameObject ▸ HyperRTS** places the `EngineRig`, a Match, or a template for a unit, building, upgrade or map object.
New scene objects go into the SubScene open for editing. Authoring components sit under **Add Component ▸ HyperRTS**,
one submenu per module, each with a help button to its module section.

**Assets ▸ Create ▸ HyperRTS ▸ Entity...** opens the New Entity wizard: search, pick an entry, type the prefab name,
**Create**. It lists every group of every `EntityBases` asset (**Create ▸ HyperRTS ▸ Entity Bases**, where a project
names its base prefabs) and then the engine templates in `TemplateRegistry`. A base becomes a prefab variant of it. The
name sets the HUD display name, underscores read as spaces.

A game adds steps by subclassing `EntityWizardStep`, which the wizard finds on its own. `Applies(folder)` says whether
the step takes part for the folder the prefab saves in. `CreateForm` returns its fields, called with a callback to run
when a value changes. `Problem(name)` returns why **Create** is disabled, shown under the form, or null. After the
prefab asset is saved, `Run(prefab)` makes what belongs beside it, from the values in its fields; a step that throws
there leaves the prefab saved. A step's UXML sits beside its script and loads through `Layout()`.

**Assets ▸ Create ▸ HyperRTS** also creates data assets (Resource Type, Damage Type, Impact Mark, Sound Cue, AI Build
Order).

