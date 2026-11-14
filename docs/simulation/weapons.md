# Weapon modules

[Docs](../README.md) ▸ [Simulation](README.md) ▸ Trajectories, Salvos, Ammo, Aim, Deploying, Fire missions, Interception, Remains

How a weapon fires once combat has picked a target: shot courses, salvos, ammo, aim, deploying, fire missions, interception and what
a dead unit leaves. Targeting and damage are in [combat](combat.md).

## Trajectories, salvos and ammo

The authored `Trajectory` picks a `ShotCourse` at bake, and runtime reads only the course. Direct shots fly straight
and home. Ballistic shells arc onto the point aimed at launch and miss a target that moved. Rockets climb, then dive
onto the live target.

A weapon with `salvoSize` above 1 fires that many shots `salvoInterval` apart, then runs its cooldown. A weapon that
launches rounds finishes its salvo when its target dies or is lost, firing the rest at the point it last aimed at
(`Weapon.SalvoAim`); hit-scan weapons and interceptors drop the rest, and so does any weapon whose unit packs up. With `extraMuzzles`, shots leave from each muzzle in turn; aim uses
the first. `spread` scatters the aim point, seeded by shooter, target and shot count. A homing shot with a spread gives
up its lock, so a rocket ripple covers an area; missiles that must stay locked keep 0.

Limited `rounds` sit in each mount's `Ammo` and refill by `AmmoRefill`: `Docked` (at an airfield), `Round` (one per
`reloadTime` anywhere) or `Magazine` (all together after the magazine runs empty). `AmmoRefillSystem` and the air
module's `AircraftReloadSystem` run `WeaponMount.Reload`.

## Aim

Each weapon has a `WeaponPose` (yaw, pitch, shot count) that clients read to aim turrets and play recoil.
`WeaponPoseSystem` turns it toward the target at the weapon's traverse and elevation speeds. A `Turreted` weapon, and
every extra weapon, fires without turning the hull once its pose is within `aimTolerance`. A hull-fixed primary sets a
`FacingGoal` instead, and movement turns the parked hull. Buildings never turn, so their weapons bake as turreted.
Shots leave from the muzzle turned by the pose, so directional armor sees the real origin and the Fire cue plays from
there. A squad's volley counts one shot on the pose and plays one cue.

## Deploying

`DeployableAuthoring` makes a unit deploy before its `needsDeploy` weapons fire. Deploy halts it and clears its
orders, `Deployable.Progress` runs 0 to 1 over `deployTime`, and Pack Up reverses it over `packUpTime`. While deploying,
deployed or packing up the unit stands: `EngagementSystem` fights it like a building and it holds its ground for
allied movers. An order to go elsewhere needs no Pack Up first: `DeployHoldSystem` packs the unit up when a move is
asked of it and holds the move until it is packed. Until deployed, its weapons keep the turret centred and an interceptor does not engage.
Once deployed, a weapon with a `deployedElevation` raises to it while it has nothing to aim at (a launcher held up to
fire) and lowers as the unit packs up; the Motion Rig poses its barrel, so no machine part may move that barrel too.
`WeaponRule` flags a `needsDeploy` weapon on a unit without a `Deployable`. A `Deployed` machine follows the progress
for jacks and outriggers ([animation](../presentation/animation.md#machines)). The skirmish AI deploys a unit holding a target and packs up one with nothing to
shoot.

A `firingArc` (half angle, 0 for all round) restricts fire to a cone about `Deployable.DeployedHeading`, or the unit's
facing when it never deploys. Auto-targeting skips targets outside it. For an ordered attack, `DeployFacingSystem` packs
up a deployed unit whose target is outside the cone, turns it to face the target and deploys again. It applies only
to weapons with a firing arc. A deployable squad is a weapon team ([squads](squads.md#squads-and-members)).

## Fire missions

A weapon with `firesOnOrder` never picks targets itself: `TargetAcquisitionSystem` and `WeaponTargetJob` skip it. It
fires only when ordered, and baking such a primary adds a `PointAim` to the unit. A `FireMission` command carries a
ground point in `Position`, anywhere in range, seen or not. `FireMissionSystem` and `MissionRunner` run the order: the
unit drives to the near edge of its primary weapon's range band (`minRange` to `range`, from its centre) if the point
lies outside it, then halts and enables `PointAim` there. `DeployFacingSystem` packs up and turns a deployed unit whose
point is outside its `firingArc`, as for an attack, and deploys it; a launcher with no arc only deploys. With the point
aimed, `WeaponPoseSystem` turns the pose onto it and `WeaponFireSystem` fires the weapon's whole salvo there through
`ShotAim`, each round scattered by `spread`; splash resolves through the hit's `DamageProfile` as for any shell. The
order ends once that salvo has fired (`Weapon.SalvosFired`), leaving the unit deployed, or at once when the weapon is out
of ammo or cannot fire at ground. A unit without `PointAim` ignores the command, and one with no move destination only
fires at points already in range. An Attack on an entity, or a Smart click on an enemy, becomes a fire mission at the
entity's position as ordered. The skirmish AI's `FireMissionPlanner` gives each idle such unit a mission on the known
enemy in range with the most enemies within a blast of it; `DeployPlanner` leaves a unit on a mission deployed.

## Interception

An interceptor is a weapon whose `targets` are `Projectile`, such as a C-RAM gun or a SAM battery. It engages the
nearest hostile projectile inside its `range` whose `ProjectileKind` is in its `interceptKinds` (Shell, Rocket,
Missile, Bomb; a game adds kinds from `ProjectileKind.GameFirst`). Drones are units, so interceptors ignore them.
`WeaponRule` wants it to hit only projectiles, with no projectile prefab and at least one kind. A primary interceptor
never sets an attack target: orders, engagement and the AI read the unit as unarmed.

`WeaponTargetSystem` files projectiles into a `ProjectileGrid`, `WeaponPoseSystem` aims the turret, and
`WeaponFireSystem` fires with the usual cooldown and rounds. `Shooter` rolls `killChance`. A kill marks the projectile
`Intercepted`, so it never lands, and plays the interceptor's Impact cue as an `Air` surface. A miss lets it fly on.
An `Offline` or dead interceptor does nothing.

## Remains

| Type | Role |
| --- | --- |
| `Crusher` | A vehicle that flattens destructible scenery it drives over |
| `DamageStagesAuthoring` | Models swapped by health fraction |
| `DecayAuthoring` | Remains that wait, are buried and go. Remains with a `NavObstacle` block their cells until then |
| `DropRemains` | Remains that fall onto the nav grid surface: ground, water or deck |
| `SpawnEffectAuthoring` | An effect played once where an entity appears |
| `TurretTossAuthoring` | A unit that throws its turret on a catastrophic death |
| `TossedTurretAuthoring` | On the thrown turret's remains: the throw clients fly |

### Turret toss

A unit with `TurretTossAuthoring` records its lethal hit (`LethalHit`). A hit is catastrophic when its type is in
`damageTypes` or when it overshoots the unit's health by `overkill` times its maximum. `DeathSystem` then leaves the
`hull` wreck instead of the usual spawn and spawns `turret` as remains at a seeded point off to the side away from the
hit. `TurretTossSystem` flies the model on an arc on clients, skipping a toss the local player cannot see.

