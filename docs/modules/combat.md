# Combat modules

[Modules](../modules.md) ▸ Combat, Veterancy, Upgrades, Auras, Abilities, Status, Vision

## Combat

A unit with a `Weapon` picks the nearest hostile its team can see, closes to range and fires. A target in fog or
hidden by stealth is never auto-acquired, so long-range weapons need a spotter; explicit attack orders still reach
past sight. Every hit goes through one damage
queue, so armor, splash and kill credit apply to all of them.

| Type | Role |
| --- | --- |
| `WeaponAuthoring` | Range, minimum range, damage type, cooldown, splash, projectile and trajectory, muzzle, turret, targets, extra weapons |
| `WeaponMount` | An extra weapon. Server only, rewritten by stats like the primary |
| `AmmoAuthoring` | Rounds per load. Reloads while docked |
| `ArmorAuthoring` | Multiplier per `DamageType`, plus front, side and rear |
| `DamageWriter` | Queues a hit. A negative amount heals |
| `CombatStance` | Aggressive, Defensive, HoldPosition or Passive |
| `LastAttacker` | Who gets the kill credit |

Idle units look for targets every 4th frame. `DamageSystem` applies all damage last in the combat phase.

The primary `Weapon` sets how a unit engages, and orders and the AI read only it. Extra weapons in
`WeaponAuthoring.extraWeapons` become `WeaponMount`s that fire at the unit's target when they can hit it, and the
unit closes to the longest reach among them. An independent mount picks its own target, such as a missile that
engages aircraft while the gun shoots tanks. `MinEffect` skips targets whose armor blunts the damage, a mount may
need research and carry rounds that refill while docked, and mount 1 to 3 play the `Fire2` to `Fire4` cues.
`ShotWriter` fires both kinds of weapon.

A projectile flies by its weapon's `Trajectory`. Direct shots fly straight and home on the target. Ballistic shells
arc onto the point aimed at launch, `ArcHeight` times the ground distance high, and miss a target that moved. Rockets
climb, then dive onto the live target. A weapon with `MinRange` never picks targets inside it, drops ones that close
in, and backs off to mid range when ordered to attack one.

Each weapon has a `WeaponPose`: yaw relative to the hull, pitch and a wrapping shot count, three bytes per weapon that
clients read to aim turrets and play recoil. `WeaponPoseSystem` aims them at the weapon's target, lifted to the launch
angle for arcs, and eases idle ones back to centre. A `Turreted` weapon fires without turning the hull.

### Destruction

| Type | Role |
| --- | --- |
| `DestructibleAuthoring` | Scenery with health and remains. Only explicit attack orders target it |
| `Crusher` | A vehicle that flattens destructible scenery it drives over |
| `DamageStagesAuthoring` | Models swapped by health fraction |
| `BreakablePartsAuthoring` | Wall and roof sections that break one at a time as health drops |
| `ToppleAuthoring` | Scenery that falls away from the hit that killed it |
| `StretchPartsAuthoring` | A prop the map builder stretches. Only the middle span scales |
| `DecayAuthoring` | Remains that wait, sink and disappear |
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
them and credits damage over time to whoever applied the effect. Each effect is an `ActiveStatus` entry, and
clients get only its type id and end time for the HUD.

`DamageTakenModifier`, in `Combat`, scales hits of one damage type on top of the unfiltered `DamageTaken` stat.

## Vision

Each team sees what its units see. `FogOfWar` holds visible, explored and detected cells per team.

| Type | Role |
| --- | --- |
| `VisionRange` | Sight radius |
| `StealthAuthoring` | Hidden unless detected. Revealed for a while after firing |
| `DetectorAuthoring` | Detection radius |
| `RememberedInFog` | Leaves a last-seen memory when it goes out of sight. On buildings by default |

- Vision updates 10 times a second, and hills block sight.
- Hidden enemies can't be selected, picked or seen, but splash still hits them.
- A memory clears when its spot is seen again.
