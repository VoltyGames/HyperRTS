# Combat modules

[Modules](../modules.md) ▸ Combat, Destruction, Veterancy, Upgrades, Auras, Abilities, Status, Vision

## Combat

A unit with a `Weapon` picks the nearest hostile its team can see, closes to range and fires. A target in fog or
hidden by stealth is never auto-acquired, so long-range weapons need a spotter; explicit attack orders still reach
past sight. Every hit goes through one damage
queue, so armor, splash and kill credit apply to all of them.

Combat's code sits in sub-folders of `Simulation/Combat/`, each its own `HyperRTS.Simulation.Combat.<Folder>`
namespace: `Weapons` (weapons, aim, ammo, deploy), `Projectiles` (projectiles, interceptors, impact surfaces),
`Targeting` (acquisition, engagement, attack orders, stance), `Damage` (damage queue, armor, shelter, crush) and
`Death` (death, remains, decay, turret toss, damage stages).


| Type | Role |
| --- | --- |
| `WeaponAuthoring` | The primary's `stats`, turreted, acquire range, leash, stance and extra weapons |
| `WeaponStats` | Damage, damage type, cooldown, splash, projectile, trajectory and projectile kind, muzzle and extra muzzles, aim (turret and barrel pivots, traverse and elevation speeds, elevation limits, tolerance), range, minimum range, targets, needs deploy, salvo size and interval, spread. The primary and each extra weapon carry one |
| `WeaponMount` | An extra weapon. Server only, rewritten by stats like the primary |
| `AmmoAuthoring` | Rounds per load and how they refill: `Docked`, `Round` or `Magazine` |
| `DeployableAuthoring` | Deploy and pack-up times for a unit that deploys to fire |
| `ArmorAuthoring` | Multiplier per `DamageType`, plus front, side and rear |
| `DamageType` | In `Common`: a damage category that armor and section materials scale, and its `garrisonFactor`, the share that reaches a sheltered garrison ([Transport](movement.md#transport)) |
| `DamageWriter` | Queues a hit. A negative amount heals |
| `CombatStance` | Aggressive, Defensive, HoldPosition or Passive |
| `LastAttacker` | Who gets the kill credit |

Idle units look for targets every 4th frame. `DamageSystem` applies all damage last in the combat phase, scaled by
`DamageFactors` (armor by type and side, then damage taken), which `TargetLookup` also reads for `MinEffect`.

The primary `Weapon` sets how a unit engages, and orders and the AI read only it. Extra weapons in
`WeaponAuthoring.extraWeapons` become `WeaponMount`s that fire at the unit's target when they can hit it, and the
unit closes to the longest reach among them. An independent mount picks its own target, such as a missile that
engages aircraft while the gun shoots tanks. `MinEffect` skips targets whose armor blunts the damage, a mount may
need research and carry rounds that refill while docked (an aircraft goes home when all its limited weapons are
empty), and each fires its own Fire cue: the cue's variant is the weapon's pose index, 0 for the primary and 1 up
for the mounts, which `mountFire` on the sound and effect authoring fills in order. A mount without an entry fires
silently. `Shooter.TryFire` is the fire step both weapon kinds share.

### Salvos, muzzles and ammo

A weapon with `salvoSize` above 1 fires that many shots `salvoInterval` apart, then runs its cooldown. Each shot
spends a round. The salvo keeps to the weapon's current target and stops when it has none (dead, lost or out of
rounds) or when its unit packs up: the rest of the salvo is dropped and the cooldown counts from the last shot.
With `extraMuzzles`, shots leave from `muzzle` and then each extra muzzle in turn, wrapping round, and the next
salvo carries on where the last stopped. They bake into the shooter's `WeaponMuzzle` buffer. Aim always uses the
first muzzle. `spread` scatters each shot's aim point at random within that many metres, seeded by shooter, target
and shot count. A ballistic shell keeps its target while it stays put; a homing shot with a spread gives up its lock
and flies to its scattered point, so a rocket ripple covers an area. Missiles that must stay locked keep 0.

Limited rounds (`AmmoAuthoring` for the primary, `rounds` on an extra weapon) refill by their `AmmoRefill`:
`Docked` one round per `reloadTime` while docked at an airfield, `Round` one per `reloadTime` anywhere, and
`Magazine` every round together `reloadTime` after the magazine runs empty. A part-empty magazine never reloads.
`AmmoRefillSystem` reloads the last two. `WeaponMount` and `Interceptor` hold an `Ammo` of their own (max 0 never
runs dry), so `Ammo.Reload` is the one reload rule.

### Deploying

`DeployableAuthoring` makes a unit deploy before its `needsDeploy` weapons fire. The Deploy command clears its
orders and halts it, and `Deployable.Progress` rises from 0 to 1 over `deployTime`. Pack Up lowers it back over
`packUpTime`. Either command reverses one in progress. While deploying, deployed or packing up the unit is rooted:
`OrderResolver` refuses every order but Attack, so the player must pack it up first, and `EngagementSystem` fights
it like a building, never chasing or backing off. Until it has deployed, its `needsDeploy` weapons keep their turret
centred, and an interceptor with `needsDeploy` neither tracks nor engages. `WeaponRule` and `InterceptorRule` flag
either on a unit without a Deployable. `State` and `Progress` replicate, and a `Deployed` machine (see
[animation](support.md#animation)) follows the progress. The skirmish AI deploys a unit holding a target in range and
packs up a deployed one with nothing to shoot, so its next wave takes it.

A projectile flies by its weapon's `Trajectory`. Direct shots fly straight and home on the target. Ballistic shells
arc onto the point aimed at launch, `ArcHeight` times the ground distance high, and miss a target that moved. Rockets
climb, then dive onto the live target. A weapon with `MinRange` never picks targets inside it, drops ones that close
in, and backs off to mid range when ordered to attack one.

Each weapon has a `WeaponPose`: yaw relative to the hull, pitch and a wrapping shot count, three bytes per weapon that
clients read to aim turrets and play recoil. `WeaponPoseSystem` turns them toward the weapon's target at the
weapon's traverse and elevation speeds, within its elevation limits and lifted to the launch angle for arcs, and turns
idle ones back to centre. It runs before firing. The pivots, rates, limits and muzzle sit in the weapon's `WeaponAim`,
which an interceptor holds too. A `Turreted` weapon, and every extra weapon, fires without turning
the hull and only once its pose is within `aimTolerance` of the target. A unit's hull-fixed primary weapon sets a
`FacingGoal` instead, and movement turns the parked hull toward it at its `Locomotion` turn rate, never swinging a long
hull into a closed cell. The weapon fires once the hull is within `aimTolerance` of the target. Buildings never turn,
so a building's weapon always bakes as turreted and its `Turret` and `Barrel` follow the pose like a unit's.
Shots leave from the muzzle turned by the pose: raised about the weapon's `barrel` and traversed about its `turret`,
so directional armor sees the real origin, and the Fire cue (muzzle flash, tracer) plays from there too.

### Interception

`InterceptorAuthoring` lets a building or unit shoot down hostile projectiles in flight, such as a C-RAM gun or a SAM
battery. It engages the nearest hostile projectile inside its `radius` whose `ProjectileKind` it lists: Shell,
Rocket, Missile or Bomb, which the firing weapon's `projectileKind` sets (None is never engaged). Drones are units, so
interceptors ignore them.

| Field | Role |
| --- | --- |
| `radius`, `kinds` | What it engages, measured from the pivot in 3D |
| `engagementsPerSecond`, `killChance` | How often it engages, and the chance each engagement kills |
| `muzzle`, `turret`, `barrel`, `aimSpeed`, elevation limits, `aimTolerance` | Aim, like a weapon's |
| `rounds`, `reloadTime` | Optional stock, one round refilled per `reloadTime` while online (`AmmoRefill.Round`) |

`InterceptorSystem` runs after the weapons and before `ProjectileSystem`. Its `WeaponPose` sits after the weapons
(index 0 on an entity with no weapon), so a `MotionRigAuthoring` turret or launcher follows it, and it engages once
that pose is within `aimTolerance`. Each engagement plays its Fire cue toward the target, with the pose index as the
variant, for the tracer burst. A kill marks the projectile `Intercepted`, so it stops flying and never lands, and
plays the interceptor's Impact cue where it died as the airburst, classed as an `Air` surface. A miss lets it fly on.
A shot aimed inside the radius is tracked before it enters, so the turret is on it as it arrives, but only one
inside the radius is engaged. The roll is seeded by shooter, target and engagement count. An `Offline` or dead
interceptor does nothing.

### Remains

| Type | Role |
| --- | --- |
| `Crusher` | A vehicle that flattens destructible scenery it drives over |
| `DamageStagesAuthoring` | Models swapped by health fraction |
| `DecayAuthoring` | Remains that wait, are buried and go. Remains with a `NavObstacle` footprint block it until they go, and while they are buried leave a square scorched and churned scar on the interaction maps |
| `DropRemains` | Remains that fall from where the entity died onto the nav grid surface: ground, water or deck. Aircraft get it with `Flight` |
| `SpawnEffectAuthoring` | An effect played once where an entity appears |
| `TurretTossAuthoring` | A unit that throws its turret when it dies to a catastrophic hit (below) |
| `TossedTurretAuthoring` | On the thrown turret's remains: the throw clients fly |

#### Turret toss

A unit with `TurretTossAuthoring` records its lethal hit (`LethalHit`: damage type, overkill and the heading away from
the shooter). A hit is catastrophic when its type is in `damageTypes`, or when it overshoots the unit's health by
`overkill` times its maximum. `DeathSystem` then leaves `hull` (the wreck without a turret) in place of the usual death
spawn, and spawns `turret` as remains at a seeded point `distance` metres off, within `arc` degrees across the heading
away from the hit. The seed is the death position and heading, so server and tests agree. The turret remains block
their cell like any remains with a `Blocker`, and `FallingRemains` settles them on the ground. `TossedTurret` is a
ghost field set on spawn; `TurretTossSystem` flies the remains' models from the hull on an arc of `height` over
`duration` seconds with spin, and skips a toss the local player cannot see. Remains without `Decay` stay for the match.

## Destruction

Building sections and toppling scenery, beside `Presentation/Destruction`, which shows them
([client](client.md#destruction)). Combat uses it, never the other way round: `DamageSystem` routes hits into
sections through `SectionDamage` and fells `Topple` pieces away from their `LethalHit`. A building's
`SectionedBuilding` holds its section masks.

| Type | Role |
| --- | --- |
| `DestructibleAuthoring` | Scenery with health and remains. Only explicit attack orders target it |
| `SectionsAuthoring` | A building made of sections that break on their own (below) |
| `SectionMaterial` | What a section is made of, and a damage multiplier per `DamageType` |
| `Repairable` | Set on `SectionsAuthoring`: builders rebuild the sections for free, and the building never dies (below) |
| `ToppleAuthoring` | Scenery that falls away from the hit that killed it (read from its `LethalHit`) |
| `StretchPartsAuthoring` | A prop the map builder stretches. Only the middle span scales |

### Sections

`SectionsAuthoring` splits a building into up to 64 `Section`s: wall panels, roof panels and floors, each a box in the
building's space with a share of its health, a `SectionMaterial`, a floor, the sections it rests on, and whether it is
anchored (stands on the ground) and exterior (an outer wall). The building's health is the sum of its sections', so
the HUD, the AI, capture and repair read it as before. **Fill From Parts** in the context menu lists the sections from
`<Name>_Intact` children and their `<Name>_Broken` siblings, saved hidden at scale 0, and guesses the rest for you to
correct: anchored when it reaches the ground, exterior when it is a wall, a full share for a wall and half for the
rest, and supports from the sections it touches below. `SectionsRule` checks the pairs, the materials and that
everything reaches the ground.

A building from the structure tool (see [tools](../../../docs/tech/tools.md#structure)) lists its sections with
**Fill From Structure** instead: set `structure` to the `<Asset>.structure.json` and fill `materialNames`, the
structure file's material names mapped to `SectionMaterial`s (no name matching). It finds each section's intact,
holed (with the hole centres), pieces, stub and heap objects by the exact names the file lists, hides all but the
intact one at scale 0, and takes the box, facing, material, floor, anchored, exterior and supports from the file, and writes the file's three
break clips into `clips`. A refill keeps the share, deck and needs-all-supports of the entry for the same intact model;
a new section's share is guessed as above. A section either has a broken model or pieces with a stub and a heap, and
the baker reads the sections' stored boxes and `clips`, never the file, so the prefab is the only bake input. Pieces
without `clips` fail `SectionsRule`. See [client](client.md#destruction) for how they show.

- **Hits.** `DamageSystem` hands a hit on a sectioned building to Combat's `SectionDamage.Route`. A direct hit lands in full on the
  first section the line from its origin to its point enters (else the nearest), and its splash on the others in
  reach. Splash from a hit elsewhere reaches every section within its radius with the usual falloff. Each section's
  material multiplies the damage after the building's own armor. A section at 0 health breaks and takes the next
  break order.
- **Collapse.** `SectionCollapseSystem` floods the supports up from the anchored sections after each break. A section
  stands while any of its supports does, or only while all of them do when its `needsAllSupports` is ticked (a bridge
  span, so it falls with either pier). Whatever no longer reaches the ground falls one support level every 0.3 s (`SectionedBuilding.FallDelay`): first the sections whose
  supports are all down, or all of them at once when they only hold each other up.
- **Floors.** A section that falls marks its floor fallen in `IntactFloors`. A container gives each floor an even
  share of its slots, filled ground floor first; `FallenFloorSystem` hits the passengers of a fallen floor for 60 % of
  their maximum health, lets them out and closes its slots until it is rebuilt.
- **Garrisons.** A `Shelter` takes its protection from the standing share of its exterior sections' health.
- **Paths and rubble.** Every section has a `NavObstaclePart`, and an obstacle with parts stamps only the closed ones:
  standing sections on the ground. `SectionSettleSystem` follows the broken mask in every world, opening the parts of
  broken sections and leaving rubble (the authoring's `rubble` prefab, crushable scenery) at the foot of each
  broken outer wall, with a scenery id from its spot, so tanks flatten it and every world agrees on it.
- **Repair.** A heal rebuilds the latest broken section that can stand first (an anchored one, or one with a
  standing support). It comes back once full, then the next, and standing sections are mended last.
- **Repairable.** Ticking `repairable` on `SectionsAuthoring` (a bridge) lets any faction's builders repair a
  neutral building through `RepairSystem`, which needs neither a faction nor a finished site (see
  `BuildingRules.NeedsRepair`). A builder restores `rate × section health / repairSeconds` a second (10 s by default)
  to the section the heal reaches next. The building's health follows its sections and never drops below 1, so with
  every section broken it stays as a wreck instead of dying.
- **Decks.** A section with `deck` carries a `NavAreaPart` of the building's `NavArea`: it stamps its deck while the
  section stands and opens the cells to what lies below (water, for ships) while it is broken. A building with a
  deck closes no `NavObstaclePart`, since its piers stand under the roadway: the bake leaves `Section.Closes` off
  for all its sections, and on for every anchored section of any other building.
- **Clients.** Only `SectionedBuilding.Broken` reaches them, as a ghost field on a ghost building and in the scenery change of
  non-ghost scenery (see [networking](../networking.md)). `SectionViewSystem` shows each section's intact or broken
  model from it ([client](client.md#destruction)).

## Stats

Part of `Common`. Upgrades, veterancy, fields, status effects and difficulty all change stats through one formula,
and each source can be removed later:

```text
value = (base + Add) × (1 + ΣPercent)
```

A `StatModifier` names a `Stat`, such as MaxHealth, Damage, Range or Income, and its `StatSource`. `StatSystem`
recomputes stats at the end of the frame when modifiers change. A `MaxHealth` change keeps the health fraction.

## Veterancy

Units gain ranks from kills. `VeterancyAuthoring` sets the thresholds and bonuses. A kill pays the victim's resource
cost times `MatchRules.ExperiencePerCost` to the last hostile attacker.

## Upgrades

An upgrade is a research prefab with `UpgradeAuthoring`: stat bonuses per target type. Once researched, it applies
to the owner's matching entities, including ones built later.

## Fields

Part of `Auras`. `AreaFieldAuthoring` makes an aura with a radius, target filter, stat bonuses and heal or damage
per second. Every 0.25 s, each entity's `FieldPresence` lists the fields it is in. The same field never stacks.
Games read `FieldPresence` for their own effects.

## Abilities

`AbilityAuthoring` gives a unit a cooldown ability. It can target nothing, a point or an entity, and can require a
building. Built-in effects spawn a prefab or deal area damage or healing. For other effects, a game reads this
frame's `AbilityActivation`.

A self-targeted ability fires on every ready caster. An aimed one walks the nearest caster into range. Player
powers are `Ability` entries on the player entity.

## Status effects

Part of `Status`. A status effect is a prefab with `StatusEffectAuthoring`: a duration, a stack rule, stat bonuses,
extra damage taken from chosen damage types and damage over time, all per stack. Its type id is the hash of its
name, and its bonuses use that id as their source.

Game code applies one with `StatusEffectWriter.Apply`, and an entity-targeted ability applies its `statusEffect`
on use. `StatusEffectSystem` applies the queue on the server, refreshes or stacks effects already active, expires
them and credits damage over time to whoever applied the effect. It runs just before `DamageSystem`, so an
ability's effect applies in the frame it is cast and damage over time lands in the frame it ticks. Each effect is
an `ActiveStatus` entry, and clients get only its type id and end time for the HUD.

`DamageTakenModifier`, in `Combat`, scales hits of one damage type on top of the unfiltered `DamageTaken` stat.

## Vision

Each team sees what its units see. `FogOfWar` holds visible, explored and detected cells per team.

| Type | Role |
| --- | --- |
| `VisionRange` | Sight radius |
| `StealthAuthoring` | Hidden unless detected. Revealed for a while after firing |
| `DetectorAuthoring` | Detection radius. Detects nothing while `Offline`, so a radar goes blind on low power |
| `RememberedInFog` | Leaves a last-seen memory when it goes out of sight. On buildings by default |

- Vision updates 10 times a second, and hills block sight.
- Hidden enemies can't be selected, picked or seen, but splash still hits them.
- A memory clears when its spot is seen again.
