using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Deploy;
using HyperRTS.Simulation.Combat.Projectiles;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Events;
using HyperRTS.Simulation.Common.Prefabs;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Vision.Detection;
using HyperRTS.Simulation.Vision.Sight;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Weapons
{
    /// <summary>
    /// Fires a weapon from a job: the fire cue, an instant hit or a launched <see cref="Projectile"/>, and the stealth
    /// reveal. Use from one thread.
    /// </summary>
    public struct Shooter
    {
        [ReadOnly] private ComponentLookup<LocalTransform> _transforms;
        private PrefabSpawner _spawner;
        private ComponentLookup<Stealth> _stealth;
        [ReadOnly] private ComponentLookup<Deployable> _deployables;
        [ReadOnly] private BufferLookup<WeaponMuzzle> _muzzles;
        [ReadOnly] private ComponentLookup<Inside> _inside;
        [ReadOnly] private BufferLookup<FiringPort> _ports;
        private BufferLookup<WeaponPose> _poses;
        private ComponentLookup<Projectile> _projectiles;
        private EventWriter<DamageEvent> _damage;
        private CueWriter _cues;
        private EntityCommandBuffer _ecb;
        [ReadOnly] private SightBlockers _blockers;

        public Shooter(ref SystemState state) : this()
        {
            _transforms = state.GetComponentLookup<LocalTransform>(true);
            _spawner = new PrefabSpawner(ref state);
            _stealth = state.GetComponentLookup<Stealth>();
            _deployables = state.GetComponentLookup<Deployable>(true);
            _muzzles = state.GetBufferLookup<WeaponMuzzle>(true);
            _inside = state.GetComponentLookup<Inside>(true);
            _ports = state.GetBufferLookup<FiringPort>(true);
            _poses = state.GetBufferLookup<WeaponPose>();
            _projectiles = state.GetComponentLookup<Projectile>();
            _damage = new EventWriter<DamageEvent>(ref state);
            _cues = new CueWriter(ref state);
        }

        /// <summary>
        /// Call each update before scheduling, with this frame's buffer and the system's <see cref="SightBlockers"/>
        /// query, created in OnCreate.
        /// </summary>
        public void Update(ref SystemState state, EntityCommandBuffer ecb, EntityQuery blockers)
        {
            _transforms.Update(ref state);
            _spawner.Update(ref state);
            _stealth.Update(ref state);
            _deployables.Update(ref state);
            _muzzles.Update(ref state);
            _inside.Update(ref state);
            _ports.Update(ref state);
            _poses.Update(ref state);
            _projectiles.Update(ref state);
            _damage.Update(ref state);
            _cues.Update(ref state);
            _ecb = ecb;
            blockers.TryGetSingleton(out _blockers);
        }

        /// <summary>
        /// The fire step every weapon takes, ending in one volley of <paramref name="shots"/> rounds with one reveal,
        /// one shot counted and one fire cue.
        /// </summary>
        public bool TryFire(Entity shooter, ref Weapon weapon, byte pose, Entity target, byte faction, int shots)
        {
            if (Deployable.Holds(_deployables, shooter, weapon.NeedsDeploy) || OutsideArc(shooter, weapon, target))
            {
                weapon.EndSalvo();
                return false;
            }

            if (weapon.CoolingDown || !Aimed(shooter, weapon, pose, target))
            {
                return false;
            }

            Fire(shooter, ref weapon, pose, target, faction, shots);
            weapon.StartCooldown();
            return true;
        }

        /// <summary>Whether <paramref name="target"/> is a projectile still flying inside the weapon's range.</summary>
        public bool CanIntercept(Entity shooter, in Weapon weapon, Entity target)
        {
            if (!_projectiles.TryGetComponent(target, out var projectile) || projectile.Intercepted)
            {
                return false;
            }

            return math.distance(_transforms[shooter].Position, _transforms[target].Position) < weapon.Range;
        }

        private bool OutsideArc(Entity shooter, in Weapon weapon, Entity target) =>
            weapon.FiringArc > 0f
            && !weapon.InFiringArc(_deployables, shooter, _transforms[shooter], _transforms[target].Position);

        /// <summary>The weapon as it fires this shot, from its next muzzle in turn.</summary>
        private Weapon TakeMuzzle(Entity shooter, ref Weapon weapon)
        {
            var shot = weapon;
            if (weapon.MuzzleCount > 1)
            {
                shot.Aim.Muzzle = _muzzles[shooter][weapon.FirstMuzzle + weapon.NextMuzzle].Point;
                weapon.NextMuzzle = (byte)((weapon.NextMuzzle + 1) % weapon.MuzzleCount);
            }

            return shot;
        }

        /// <summary>A hull-fixed weapon fires as the hull faces; a turret once its pose points at the target.</summary>
        private bool Aimed(Entity shooter, in Weapon weapon, byte pose, Entity target)
        {
            if (!weapon.Aim.Turreted)
            {
                return true;
            }

            var aimed = _poses[shooter][pose];
            var aim = weapon.Aim.TargetPoint(_transforms[target].Position);
            return weapon.Aim.Aligned(aimed, weapon.Aim.Angles(_transforms[shooter], aimed, aim));
        }

        /// <summary>
        /// Fires a volley at <see cref="WeaponPose"/> index <paramref name="pose"/>: the reveal, the shot count and the
        /// fire cue happen once, the hit or launch once per round.
        /// </summary>
        private void Fire(Entity shooter, ref Weapon weapon, byte pose, Entity target, byte faction, int shots)
        {
            if (_stealth.TryGetRefRW(shooter, out var stealth))
            {
                stealth.ValueRW.RevealTimer = stealth.ValueRO.RevealDuration;
            }

            var poses = _poses[shooter];
            var fired = poses[pose];
            fired.CountShot();
            poses[pose] = fired;
            var place = _transforms[shooter];
            var targetPosition = _transforms[target].Position;
            var ported = TryFindPort(shooter, targetPosition, out var port);
            if (ported)
            {
                place = port;
                fired.YawAngle = 0f;
                fired.PitchAngle = 0f;
            }

            var firing = new Firing(shooter, faction, pose, place, fired);
            for (var round = 0; round < shots; round++)
            {
                var shot = TakeMuzzle(shooter, ref weapon);
                if (ported)
                {
                    shot.Aim.HasMuzzle = true;
                    shot.Aim.Muzzle = float3.zero;
                }

                if (round == 0)
                {
                    PlayFire(firing, shot, targetPosition);
                }

                Shoot(firing, shot, round, target, targetPosition);
            }
        }

        private void Shoot(in Firing firing, in Weapon shot, int round, Entity target, float3 targetPosition)
        {
            if (shot.FiresRounds)
            {
                var aimed = target;
                var aimedAt = targetPosition;
                Obstruct(firing.Shooter, shot, firing.Pose, firing.Place, ref aimed, ref aimedAt);
                Launch(firing, shot, round, aimed, aimedAt);
            }
            else if (shot.ShootsProjectiles)
            {
                Intercept(firing, shot, target, targetPosition);
            }
            else
            {
                Strike(firing, shot, round == 0, target, targetPosition);
            }
        }

        /// <summary>An instant hit; the first round of a volley also writes the impact cue.</summary>
        private void Strike(in Firing firing, in Weapon weapon, bool cue, Entity target, float3 targetPosition)
        {
            var origin = weapon.Aim.HitOrigin(firing.Place, firing.Pose);
            var hit = weapon.Damage.Hit(firing.Shooter, firing.Faction, origin, target, targetPosition, weapon.Targets);
            _damage.Add(hit);
            if (cue)
            {
                AddImpact(firing, hit, origin, targetPosition);
            }
        }

        private void AddImpact(in Firing firing, in DamageEvent hit, float3 origin, float3 targetPosition)
        {
            var along = math.normalizesafe(targetPosition - origin, firing.Place.Forward());
            hit.AddImpact(ref _cues, _cues.TypeIdFor(firing.Shooter, CueSlot.Impact), along);
        }

        /// <summary>A shot at a projectile: a seeded roll that downs it, then the impact cue where it dies.</summary>
        private void Intercept(in Firing firing, in Weapon weapon, Entity target, float3 targetPosition)
        {
            // Seeded by shooter, target and shot count, so the same fight rolls the same.
            var seed = math.hash(new uint4((uint)firing.Shooter.Index, (uint)target.Index, (uint)target.Version,
                firing.Pose.Shots));
            if (!weapon.Kills(seed))
            {
                return;
            }

            var projectile = _projectiles[target];
            projectile.Intercepted = true;
            _projectiles[target] = projectile;
            _ecb.DestroyEntity(target);
            var origin = weapon.Aim.HitOrigin(firing.Place, firing.Pose);
            var hit = weapon.Damage.Hit(firing.Shooter, firing.Faction, origin, target, targetPosition, weapon.Targets);
            AddImpact(firing, hit, origin, targetPosition);
        }

        /// <summary>Turns a direct-fire round onto whatever solid stands between the muzzle and the target.</summary>
        private void Obstruct(Entity shooter, in Weapon weapon, in WeaponPose pose, in LocalTransform place,
            ref Entity target, ref float3 targetPosition)
        {
            if (!weapon.NeedsLineOfFire || !_blockers.IsCreated)
            {
                return;
            }

            var holder = _inside.HasEnabled(shooter) ? _inside[shooter].Container : shooter;
            var origin = weapon.Aim.LaunchPoint(place, pose);
            var aim = weapon.Aim.TargetPoint(targetPosition);
            if (_blockers.TryFindObstruction(origin, aim, holder, target, out var contact, out var obstacle))
            {
                target = obstacle;
                targetPosition = contact;
            }
        }

        /// <summary>A garrisoned passenger shoots from the port of its container that faces the target best.</summary>
        private bool TryFindPort(Entity shooter, float3 targetPosition, out LocalTransform port)
        {
            port = default;
            if (!_inside.HasEnabled(shooter))
            {
                return false;
            }

            var container = _inside[shooter].Container;
            if (!_ports.TryGetBuffer(container, out var ports) || !_transforms.TryGetComponent(container, out var place))
            {
                return false;
            }

            return FiringPort.TryChoose(ports, place, targetPosition, out port);
        }

        /// <summary>The fire cue faces the target level, the way the shooter turned, from the muzzle as the pose turned it.</summary>
        private void PlayFire(in Firing firing, in Weapon weapon, float3 targetPosition)
        {
            var toward = targetPosition - firing.Place.Position;
            toward.y = 0f;
            weapon.Aim.PlayFire(ref _cues, firing, math.normalizesafe(toward, firing.Place.Forward()));
        }

        private void Launch(in Firing firing, in Weapon weapon, int round, Entity target, float3 targetPosition)
        {
            var origin = weapon.Aim.LaunchPoint(firing.Place, firing.Pose);
            var seed = math.hash(new uint4((uint)firing.Shooter.Index, (uint)target.Index, firing.Pose.Shots, (uint)round));
            var aim = weapon.Scatter(weapon.Aim.TargetPoint(targetPosition), seed);

            // Smoke thick enough to hide the target breaks a guided round's lock; it flies on to the aim point.
            var locked = weapon.KeepsLock && !(_blockers.IsCreated && _blockers.Haze(origin, aim) >= 1f);
            var hit = weapon.Damage.Hit(firing.Shooter, firing.Faction, origin, locked ? target : Entity.Null, aim, weapon.Targets);
            var flight = weapon.Launched(hit, _cues.TypeIdFor(firing.Shooter, CueSlot.Impact), origin, aim);
            var heading = flight.Arcs ? flight.Heading() : aim - origin;
            var projectile = _spawner.Spawn(_ecb, weapon.ProjectilePrefab, origin,
                quaternion.LookRotationSafe(heading, math.up()), firing.Faction);
            _ecb.AddComponent(projectile, flight);
        }
    }
}
