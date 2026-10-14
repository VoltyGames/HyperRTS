# Combat modules

[Modules](../modules.md) ▸ Combat, Stats, Veterancy, Upgrades, Fields, Abilities, Vision

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
| `ToppleAuthoring` / `Topple`, `FellFrom`, `Felling`, `ToppleMath` | Scenery that falls away from its lethal hit (`DamageSystem` records the fall heading away from the shooter, crusher or blast centre); `DeathSystem` lays the remains along the fall, and `ToppleSystem` (Presentation) shows a seen fall over 1.5 s. The inspector's **Measure from model** (`ToppleMeasure`) sets `trunk` from the LOD0 faces of a chosen trunk material and `lie` from the Destructible's remains |
| `DamageStagesAuthoring` / `DamageStage` | Child models shown by health fraction, each step with an effect |
| `ImpactWriter`, `ImpactQueue` / `ImpactEvent` | Where shots of shooters with impact marks or effects land; `ImpactSurfaceSystem` sets the `ImpactSurface` and `Material` (the target's `HitMaterial`, else `Water`, else `None` for presentation to read from `GroundMaterials`) |
| `SpawnEffectAuthoring` / `SpawnEffect` | Effect played once where an entity appears (a ruin's dust) |

Systems, in order: `AttackOrderSystem` (explicit attacks), `TargetAcquisitionSystem` (idle and moving units pick
the nearest hostile, every 4th frame each), `EngagementSystem` (chase, stop in range, release), `WeaponFireSystem`
(instant hit or projectile), `ProjectileSystem`, `CrushSystem` (lethal hit on scenery under a moving crusher) and
`DamageSystem` (armor × facing × `DamageTaken`, splash falloff over enemies, allies with friendly fire and scenery).
Turrets that turn on their own are not built in.

## Stats

| Type | Role |
| --- | --- |
| `StatModifier`, `Stat` | `(base + Add) × (1 + ΣPercent)` for MaxHealth, Damage, Range, FireRate, MoveSpeed, VisionRange, DamageTaken, BuildRate, ProductionSpeed, Income; games from `Stat.Custom` (128) |
| `StatSource` | Who added a modifier: Upgrade, Veterancy, Field; games from `Custom` |
| `BaseStat`, `StatMath` | Unmodified values; `Evaluate`, `Apply`, `RemoveSource` |

Each module applies its stats last in the lifecycle phase, only when the modifier buffer changes (`MaxHealth` keeps
the health fraction).

## Veterancy

`VeterancyAuthoring` sets rank thresholds and bonuses; `ExperienceValue` is what a killer earns. `KillCreditSystem`
pays the last hostile attacker; `VeterancySystem` promotes and swaps rank modifiers.

## Upgrades

`UpgradeAuthoring` / `UpgradeEffect`: a research prefab with stat bonuses per target type. `UpgradeSystem` applies
the owner's `CompletedResearch` to matching entities, catches up later spawns, and swaps sets on an owner change.

## Fields

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

`FogOfWarSystem` restamps vision 10 times a second (hills block sight over `TerrainHeight`; fog off still detects).
`StealthSystem` sets `Stealthed`. `LocalFogViewSystem` tags `FogHidden`, which selection, picking, HUD and rendering
skip; splash still hits. `Destructible` map structures are never hidden.
