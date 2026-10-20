# Combat modules

[Modules](../modules.md) ▸ Combat, Veterancy, Upgrades, Auras, Abilities, Vision

## Combat

A unit with a `Weapon` picks the nearest hostile its team can see, closes to range and fires. A target in fog or
hidden by stealth is never auto-acquired, so long-range weapons need a spotter; explicit attack orders still reach
past sight. Every hit goes through one damage
queue, so armor, splash and kill credit apply to all of them.

| Type | Role |
| --- | --- |
| `WeaponAuthoring` | Range, damage type, cooldown, splash, projectile, surface or air targets |
| `AmmoAuthoring` | Rounds per load. Reloads while docked |
| `ArmorAuthoring` | Multiplier per `DamageType`, plus front, side and rear |
| `DamageWriter` | Queues a hit. A negative amount heals |
| `CombatStance` | Aggressive, Defensive, HoldPosition or Passive |
| `LastAttacker` | Who gets the kill credit |

Idle units look for targets every 4th frame. `DamageSystem` applies all damage last in the combat phase. Turrets
that aim on their own are not built in.

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

Part of `Common`. Upgrades, veterancy, fields and difficulty all change stats through one formula, and each source
can be removed later:

```text
value = (base + Add) × (1 + ΣPercent)
```

A `StatModifier` names a `Stat`, such as MaxHealth, Damage, Range or Income, and its `StatSource`. `StatSystem`
recomputes stats at the end of the frame when modifiers change. A `MaxHealth` change keeps the health fraction.

## Veterancy

Units gain ranks from kills. `VeterancyAuthoring` sets the thresholds and bonuses. `ExperienceValue` is what killing
this unit earns, paid to the last hostile attacker.

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
