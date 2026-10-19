# Combat modules

[Modules](../modules.md) ▸ Combat, Veterancy, Upgrades, Auras, Abilities, Vision

## Combat

| Type | Role |
| --- | --- |
| `WeaponAuthoring` / `Weapon` | Range, damage and type, cooldown, splash, friendly fire, projectile, acquire range, `WeaponTargets` (Surface, Air) |
| `AmmoAuthoring` / `Ammo` | Rounds per load; empty weapons hold fire; reloads while `Docked` |
| `ArmorAuthoring` / `ArmorModifier`, `ArmorFacing` | Multiplier per `DamageType`; front / side / rear |
| `DamageWriter`, `DamageQueue` | Every hit and heal (negative) is queued |
| `CombatStance` | Aggressive, Defensive (returns to anchor), HoldPosition, Passive |
| `LastAttacker`, `Projectile`, `SpawnOnDeath` | Kill credit; a shot in flight; the wreck spawned on death |
| `TargetLookup` | `IsValidTarget` (hostile, alive, hittable layer, not stealthed); `IsAttackable` adds `Destructible` scenery for explicit orders |
| `DestructibleAuthoring` | Marks a building or unit `Destructible`, or makes standalone scenery with health, crush rating, remains and a blocked box |
| `Crusher` | A vehicle that flattens destructible scenery it drives over |
| `StretchPartsAuthoring` / `StretchParts`, `PartStretch`, `StretchLayout` | Start cap, end cap and span (sharing one parent under the root) of a prop the map builder stretches: caps move out, only the span scales. A stretched prop carries its `PartStretch`; `DeathSystem` copies it to the remains, whose own parts `StretchPartsSystem` stretches once to match |
| `ToppleAuthoring` / `Topple`, `FellFrom`, `Felling`, `ToppleMath` | Scenery that falls away from its lethal hit (`DamageSystem` records the fall heading away from the shooter, crusher or blast centre); `DeathSystem` lays the remains along the fall, and `ToppleSystem` (Presentation) shows a seen fall over 1.5 s. The inspector's **Measure from model** (`ToppleMeasure`) sets `trunk` from the LOD0 faces of a chosen trunk material and `lie` from the Destructible's remains |
| `DamageStagesAuthoring` / `DamageStage` | Child models shown by health fraction, each step with an effect |
| `ImpactSurfaceSystem` | Sets each `CueSlot.Impact` cue's `ImpactSurface` and `Material` (the target's `HitMaterial`, else `Water`, else the `GroundMaterials` cell under open ground, else `None`); impact marks and `ImpactSurface` live in `Cues` |
| `SpawnEffectAuthoring` / `SpawnEffect` | Effect played once where an entity appears (a ruin's dust) |
| `DecayAuthoring` / `Decay` | Remains (a wreck) that wait `lifetime` s, sink `despawnDepth` m over `despawnDuration` s and are destroyed (`DecaySystem`, authoritative worlds; put it on a ghost so clients follow). The wait is a fixed `DespawnStartTime` time, so waiting remains are only read and a static ghost sends nothing. `DespawnProgress` is replicated so clients fade the loop effects |

Systems, in order: `AttackOrderSystem` (explicit attacks), `TargetAcquisitionSystem` (idle and moving units pick
the nearest hostile, every 4th frame each), `EngagementSystem` (chase, stop in range, release), `WeaponFireSystem`
(instant hit or projectile), `ProjectileSystem`, `CrushSystem` (lethal hit on scenery under a moving crusher) and
`DamageSystem` (armor × facing × `DamageTaken`, splash falloff over enemies, allies with friendly fire and scenery).
Turrets that turn on their own are not built in.

## Stats

Part of `Common`.

| Type | Role |
| --- | --- |
| `StatModifier`, `Stat` | `(base + Add) × (1 + ΣPercent)` for MaxHealth, Damage, Range, FireRate, MoveSpeed, VisionRange, DamageTaken, BuildRate, ProductionSpeed, Income |
| `StatSource` | Who added a modifier: Upgrade, Veterancy, Field, Difficulty |
| `BaseStat`, `StatBasesPending`, `StatMath` | Unmodified values; `Evaluate`, `Apply`, `RemoveSource` |

`StatBaseSystem` records each entity's bases from its authored components at spawn (first in the order phase);
`StatSystem` rewrites every live stat from them last in the lifecycle phase, only when the modifier buffer changes
(`MaxHealth` keeps the health fraction).

## Veterancy

`VeterancyAuthoring` sets rank thresholds and bonuses; `ExperienceValue` is what a killer earns. `KillCreditSystem`
pays the last hostile attacker; `VeterancySystem` promotes and swaps rank modifiers.

## Upgrades

`UpgradeAuthoring` / `UpgradeEffect`: a research prefab with stat bonuses per target type. `UpgradeSystem` applies
the owner's `CompletedResearch` to matching entities, catches up later spawns, and swaps sets on an owner change.

## Auras

`AreaFieldAuthoring` / `AreaField`: an aura (radius, targets by relation and kind, stat bonuses, heal or damage per
second). Every 0.25 s `AreaFieldSystem` rewrites each entity's `FieldPresence` (one entry per field name, no
stacking); games read it for their own effects.

## Abilities

| Type | Role |
| --- | --- |
| `AbilityAuthoring` / `Ability` | Cooldown ability: target (none, point, entity) and filter, range, required building, built-in effects (spawn, area damage or heal) |
| `AbilityActivation`, `AbilityEvents` | This frame's uses, for game systems |

`AbilityCommandSystem` handles UseAbility (self-targeted: every ready caster; aimed: the nearest walks into range)
and UsePower (the player entity's `Ability` buffer). `AbilitySystem` ticks cooldowns and finishes walk-in casts.

## Vision

| Type | Role |
| --- | --- |
| `VisionRange` | Sight radius |
| `FogOfWar` | Singleton grid, one bit per team (1–15) for visible, explored, detected: `CanSee`, `IsHiddenFrom`, `IsCloakedFrom` |
| `StealthAuthoring` / `Stealthed` | Reveal time after firing, still-only option; enabled while hidden |
| `DetectorAuthoring` / `Detector` | Detection radius |
| `FogHidden`, `LocalFogView` | Hostile roots the local player can't see; who the local player views as (`RevealAll` for replays) |
| `FogMemory`, `FogSighting`, `LastSeen` | Remembered in the fog (baked on buildings, `rememberedInFog` opts out); latest sighting of a visible enemy root (cleanup, outlives a despawned ghost); a remembered building |

`FogOfWarSystem` restamps vision 10 times a second (hills block sight over `TerrainHeight`; fog off still detects).
`StealthSystem` sets `Stealthed`. `LocalFogViewSystem` tags `FogHidden`, which selection, picking, HUD and rendering
skip; splash still hits. `Destructible` map structures are never hidden. `FogMemorySystem` (presented worlds) records
each visible enemy `FogMemory` root and leaves a `LastSeen` memory when it goes out of sight, or its ghost is despawned
there; the memory goes when the building is seen alive (same entity, or same ghost id and spawn tick) or its place has
been in sight for `Grace` with nothing there. Reveal-all, fog off and a viewer change forget everything.
