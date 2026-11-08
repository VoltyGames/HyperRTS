# Combat modules

[Docs](../README.md) ▸ [Simulation](README.md) ▸ Combat, Destruction, Stats, Veterancy, Upgrades, Auras, Abilities, Status, Vision

Targeting, damage, buildings that break, stat modifiers and sight. How weapons fire is in [weapons](weapons.md),
squads and cover in [squads](squads.md). Code sits in `Simulation/Combat/` sub-folders, namespace
`HyperRTS.Simulation.Combat.<Folder>`: `Weapons`, `Projectiles`, `Targeting`, `Damage` and `Death`.

## Combat

An armed unit picks the nearest hostile its team can see, closes to range and fires. A target in fog or hidden by
stealth is never auto-acquired, so long-range weapons need a spotter; explicit attack orders still reach past sight.
Every hit goes through one damage queue, so armor, splash and kill credit apply to all of them. Two kills skip it on
purpose: a destroyed container's passengers die with the container's killer credited, and a collapse only totals
sections that hits already broke.

| Type | Role |
| --- | --- |
| `WeaponAuthoring` | The primary's `stats`, turreted, acquire range, leash, stance and `extraWeapons` |
| `WeaponStats` | One weapon: its hit, cooldown, projectile and trajectory, muzzles, aim, range, minimum range, `WeaponTargets` (Ground, Naval, Air, Projectile), needs deploy, firing arc, ammo, salvo, spread. The primary and each extra weapon carry one |
| `WeaponMount` | One weapon at runtime: the primary at index 0, then the extras. Server only |
| `DamageProfile` | What a hit deals: amount, `DamageType`, garrison factor, cover piercing, suppression, splash. `DamageSettings` is its inspector block |
| `DeployableAuthoring` | Deploy and pack-up times for a unit that deploys to fire |
| `ArmorAuthoring` | Multiplier per `DamageType`, plus front, side and rear |
| `DamageEvent` | A queued hit, written through `EventWriter<DamageEvent>`. A negative amount heals. `Direct` and `Typed` build the hits that have no weapon: repair, crushing, falling floors, auras, status damage |
| `CombatStance` | Aggressive, Defensive, HoldPosition or Passive, with the acquire range and leash |
| `LastAttacker` | Who gets the kill credit |

`DamageSystem` applies all damage last in the combat phase, scaled by `DamageFactors` (armor by type and side, then
damage taken). Idle units look for targets every 4th frame.

**Extra weapons.** The primary sets how a unit engages; orders and the AI read only it. Extra weapons fire at the
unit's target when they can hit it, and the unit closes to the longest reach among them. An independent one picks its
own target, such as a missile that engages aircraft while the gun shoots tanks. An extra weapon may need research,
and may carry limited rounds. An aircraft goes home when all its limited weapons are empty. Each fires its own Fire
cue, filled in order from index 1 by `mountFire` on the sound and effect authoring; one without an entry is silent.
`WeaponTargetSystem` gives each weapon its target for the frame and `WeaponFireSystem` fires through
`Shooter.TryFire`. A weapon with `MinRange` never picks targets inside it and backs off when ordered to attack one.

**Line of fire.** Direct fire and rockets need a clear line from the shooter's eye to the target's top over the ground
and solid sight blocks (`SightBlockers.TryFindObstruction`). Scans skip blocked targets. A weapon that hits at once
waits for a clear line, so an ordered attack closes in until it has one. A weapon that fires rounds shoots anyway and
hits the obstacle. A round launched through dense porous cover such as smoke loses its lock and flies to the aim point.

**Reach.** `TargetLookup.Reach` is a weapon's range against one target. A passenger firing out of a container gains
`ContainerAuthoring.passengerRangeBonus`. Direct fire and rockets from above their target gain range per metre of
height (`SightRules`); ballistic fire does not.

A system using `TargetLookup` creates its `TargetQueries` in `OnCreate` and passes them to `Update`: the lookup travels
into jobs, which can't carry a query.

## Destruction

Building sections and toppling scenery. Presentation is in [client](../presentation/rendering.md#destruction). Combat uses it, never the
other way round: `DamageSystem` routes hits into sections through `SectionDamage` and fells `Topple` pieces away from
their `LethalHit`.

| Type | Role |
| --- | --- |
| `DestructibleAuthoring` | Scenery with health and remains. Only explicit attack orders target it |
| `SectionsAuthoring` | A building made of sections that break on their own |
| `SectionMaterial` | What a section is made of, with a damage multiplier per `DamageType` |
| `ToppleAuthoring` | Scenery that falls away from the hit that killed it |
| `StretchPartsAuthoring` | A prop the map builder stretches. Only the middle span scales |

### Sections

`SectionsAuthoring` splits a building into up to 64 `Section`s (walls, roofs, floors). Each is a box with a share of
the health, a `SectionMaterial`, a floor, the sections it rests on, and flags for anchored (stands on the ground) and
exterior. The building's health is the sum of its sections', so the HUD, AI, capture and repair read it as usual.

- **Filling the list.** **Fill from parts** lists sections from `<Name>_Intact` children and their `<Name>_Broken`
  siblings and guesses the rest for you to correct. For a building from the structure tool
  ([tools](../../../docs/art/generators.md#structure-builds)), **Fill from model** reads the sections its model's import
  keeps (`ModelSections`, from the FBX properties `section` and `hole` on the intact and holed meshes): materials,
  floors, supports, facing and holes. It measures each box from the intact mesh and takes the `In`, `Out` and
  `Collapse` break clips from the [rigid](../presentation/animation.md#rigid-vertex-animation) clip table the pieces
  play. Map its material names in `materialNames` (no name matching). A refill keeps shares and decks of existing entries. The baker reads the stored
  prefab, never the file. `SectionsRule` checks pairs, materials, clips and that everything reaches the ground.
- **Hits.** A direct hit lands in full on the first section its line enters, and its splash on the others in reach.
  The section's material multiplies the damage after the building's armor. A section at 0 breaks.
- **Collapse.** `SectionCollapseSystem` floods supports up from the anchored sections after each break. Whatever no
  longer reaches the ground falls level by level. A section needing all its supports (`needsAllSupports`, such as a
  bridge span) falls with either one.
- **Floors.** `FallenFloorSystem` hurts the passengers of a fallen floor, lets them out and closes its slots until it
  is rebuilt.
- **Garrisons.** A `Shelter` takes its protection from the standing share of its exterior sections' health.
- **Paths and rubble.** Standing sections on the ground stamp `NavObstaclePart`s. `SectionSettleSystem` opens broken
  ones and leaves crushable rubble where an outer wall fell.
- **Repair.** A heal rebuilds the latest broken section that can stand first.
- **Repairable.** Ticking `repairable` lets any owner's builders repair a neutral building such as a bridge, needing
  no owner or finished site. Its health never drops below 1, so it stays as a wreck.
- **Decks.** A section with `deck` carries a `NavAreaPart`: it stamps its deck while standing and opens the cells to
  what lies below while broken. A deck building closes no `NavObstaclePart`.
- **Clients** receive only `SectionedBuilding.Broken` (a ghost field, or in the scenery change of non-ghost scenery,
  see [networking](../networking.md)), and `SectionViewSystem` shows the models.

## Stats

Upgrades, veterancy, auras, status effects and difficulty all change stats through one `StatModifier` buffer and one
formula, and each source can be removed later:

```text
value = (base + Add) × (1 + ΣPercent)
```

A `StatModifier` names a `Stat` (MaxHealth, Damage, Range, Income and so on) and its `StatSource`. Authoring lists
`StatBonus`es, turned into modifiers by `StatBonus.ToModifiers`; auras and status effects keep `GrantedModifier`s and
copy them onto whoever they affect.

`StatBaseSystem` records each stat's base at spawn and `StatSystem` recomputes at the end of the frame when modifiers
change. Fields marked `[StatField(stat)]` go through one generic chunk job each; health (a `MaxHealth` change keeps
the health fraction) and weapon mounts have their own.

A `DamageTaken` modifier may name a `DamageType`. A hit is scaled by the untyped modifiers times those of its type,
each part floored at 0 so a resist never heals. Other stats read only untyped modifiers, and a typed bonus on them
fails the bake.

## Veterancy

Units gain ranks from kills. `VeterancyAuthoring` sets thresholds and bonuses. A kill pays the victim's resource cost
times `MatchRules.ExperiencePerCost` to the last hostile attacker.

## Upgrades

An upgrade is a research prefab with `UpgradeAuthoring`: stat bonuses per target type. Once researched it applies to
the owner's matching entities, including ones built later.

## Auras

`AuraAuthoring` makes an aura with a radius, target filter, stat bonuses and heal or damage per second. Every 0.25 s
each entity's `AuraPresence` lists the auras it is in. The same aura never stacks. Games read `AuraPresence` for their
own effects.

## Abilities

`AbilityAuthoring` gives a unit a cooldown ability. It can target nothing, a point or an entity, and can require a
building. Built-in effects spawn a prefab or deal area damage or healing; for others a game reads this frame's
`AbilityActivation`. A self-targeted ability fires on every ready caster, an aimed one walks the nearest caster into
range. Player powers are `Ability` entries on the player entity.

`Ability` is the fixed definition and never replicates; clients bake it from the same prefab. The running cooldown is
the replicated `AbilityCooldown { Id, Remaining }` buffer, matched by id.

A smoke screen is an ability whose spawned prefab carries a dense porous `SightBlock` and a `Decay`: it blocks sight
and breaks guided rounds' lock until its lifetime ends.

## Status effects

A status effect is a prefab with `StatusEffectAuthoring`: a duration, a stack rule, stat bonuses (damage taken ones
may name a damage type) and damage over time, all per stack. Its type id is the hash of its name.

Game code applies one by writing a `StatusRequest` through `EventWriter<StatusRequest>`; an entity-targeted ability
applies its `statusEffect` on use. `StatusEffectSystem` runs just before `DamageSystem` on the server, so an effect
applies in the frame it is cast and damage over time lands in the frame it ticks, credited to whoever applied it.
Clients get only the type id and end time for the HUD.

## Vision

Each team sees what its units see. `FogOfWar` holds visible, explored and detected cells per team.

| Type | Role |
| --- | --- |
| `VisionRange` | Sight radius |
| `StealthAuthoring` | Hidden unless detected, and revealed for a while after firing. `inConcealment` hides it only in concealing cover |
| `DetectorAuthoring` | Detection radius. Detects nothing while `Offline`, so a radar goes blind on low power |
| `RememberedInFog` | Leaves a last-seen memory when it leaves sight. On buildings by default |
| `SightBlockAuthoring` | A box that blocks sight: solid below `height` (houses, walls, wrecks), porous below `porousTop` with a `density` per metre (tree crowns, smoke), and concealing still infantry when `conceals` is set. **Fit to model** fills the solid box |
| `CropCoverAuthoring` | Bakes the map's `CropCoverMap` from each crop's `sightDensity` |
| `GameEntityAuthoring.tall` | A `TallTarget`, as tall as a vehicle or building: seen over cover that hides infantry |
| `MatchAuthoring.sight` | `SightRules`: what high ground is worth to sight and direct-fire range, and the reveal distance |
| `ContainerAuthoring.eyeAboveRoof` | Passengers of a container with a sight block see from this far above its top |

- **Layers.** Vision updates 10 times a second. `FogOfWar.Visible` holds cells seen at 1 m, for infantry;
  `VisibleTall` those seen at 3 m, for a `TallTarget`. `CanSee` and `IsHiddenFrom` take the target's layer.
- **Rebuild.** Each viewer keeps its sight in a `SightArea`, redrawn only when it moves cell, its eye, reach or team
  changes, or `SightBlockers.DirtyTiles` marks a change near it. `FogMergeJob` merges the areas.
- **High ground.** `HighGroundSystem` measures each cell's ground against the local mean when the map loads. A viewer
  on high ground sees further per metre of rise, up to a cap.
- **Blocks.** `SightBlockSystem` stamps enabled blocks onto the fog grid when they change. Remains turn theirs off as
  they are buried. Sight rays take density per metre from woods, crops and smoke the eye is below, so a viewer above
  the canopy sees over. Crushers flatten crop cover (`CropFlattenSystem`) in every world, so client fog matches.
- **Concealment.** `FogOfWar.Close` holds cells near a team's ground units. A `Concealment` (none, `Concealed`,
  `Stealthed`) goes with every check: stealth shows only to detectors, concealment also to close ground units. A cue
  carries its source's concealment and tall flag, so it reaches whoever could see the source.
- A viewer's own block, or its container's, hides nothing from it. Garrisons see from above the roof.
- Hidden enemies can't be selected or seen, but splash still hits them. A memory clears when its spot is seen again.
