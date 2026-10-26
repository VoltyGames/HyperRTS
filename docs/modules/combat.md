# Combat modules

[Modules](../modules.md) ▸ Combat, Veterancy, Upgrades, Auras, Abilities, Status, Vision

## Combat

A unit with a `Weapon` picks the nearest hostile its team can see, closes to range and fires. A target in fog or
hidden by stealth is never auto-acquired, so long-range weapons need a spotter; explicit attack orders still reach
past sight. Every hit goes through one damage
queue, so armor, splash and kill credit apply to all of them.

| Type | Role |
| --- | --- |
| `WeaponAuthoring` | The primary's `stats`, turreted, acquire range, leash, stance and extra weapons |
| `WeaponStats` | Damage, damage type, cooldown, splash, projectile, trajectory and projectile kind, muzzle and extra muzzles, aim (turret and barrel pivots, traverse and elevation speeds, elevation limits, tolerance), range, minimum range, targets, needs deploy, salvo size and interval, spread. The primary and each extra weapon carry one |
| `WeaponMount` | An extra weapon. Server only, rewritten by stats like the primary |
| `AmmoAuthoring` | Rounds per load and how they refill: `Docked`, `Round` or `Magazine` |
| `DeployableAuthoring` | Deploy and pack-up times for a unit that deploys to fire |
| `ArmorAuthoring` | Multiplier per `DamageType`, plus front, side and rear |
| `DamageWriter` | Queues a hit. A negative amount heals |
| `CombatStance` | Aggressive, Defensive, HoldPosition or Passive |
| `LastAttacker` | Who gets the kill credit |

Idle units look for targets every 4th frame. `DamageSystem` applies all damage last in the combat phase.

The primary `Weapon` sets how a unit engages, and orders and the AI read only it. Extra weapons in
`WeaponAuthoring.extraWeapons` become `WeaponMount`s that fire at the unit's target when they can hit it, and the
unit closes to the longest reach among them. An independent mount picks its own target, such as a missile that
engages aircraft while the gun shoots tanks. `MinEffect` skips targets whose armor blunts the damage, a mount may
need research and carry rounds that refill while docked (an aircraft goes home when all its limited weapons are
empty), and each fires its own Fire cue: the cue's variant is the weapon's pose index, 0 for the primary and 1 up
for the mounts, which `mountFire` on the sound and effect authoring fills in order. A mount without an entry fires
silently. `ShotWriter.TryFire` is the fire step both weapon kinds share.
`ShotWriter` fires both kinds of weapon.

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
`AmmoRefillSystem` reloads the last two, and `Ammo.Reload` is the one rule `Ammo`, `WeaponMount` and
`Interceptor` share.

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
idle ones back to centre. It runs before firing. A `Turreted` weapon, and every extra weapon, fires without turning
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

### Destruction

| Type | Role |
| --- | --- |
| `DestructibleAuthoring` | Scenery with health and remains. Only explicit attack orders target it |
| `Crusher` | A vehicle that flattens destructible scenery it drives over |
| `DamageStagesAuthoring` | Models swapped by health fraction |
| `BreakablePartsAuthoring` | Wall and roof sections that break one at a time as health drops |
| `ToppleAuthoring` | Scenery that falls away from the hit that killed it |
| `StretchPartsAuthoring` | A prop the map builder stretches. Only the middle span scales |
| `DecayAuthoring` | Remains that wait, are buried and go. Remains with a `NavObstacle` footprint block it until they go, and while they are buried leave a square scorched and churned scar on the interaction maps |
| `DropRemains` | Remains that fall from where the entity died onto the nav grid surface: ground, water or deck. Aircraft get it with `Flight` |
| `SpawnEffectAuthoring` | An effect played once where an entity appears |

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
