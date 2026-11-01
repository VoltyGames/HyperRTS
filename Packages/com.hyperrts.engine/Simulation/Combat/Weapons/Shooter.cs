using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Projectiles;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Vision;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Weapons
{
    /// <summary>
    /// Fires a weapon from a job: the fire cue, an instant hit or a launched <see cref="Projectile"/>, and the stealth
    /// reveal. A direct-fire round with something solid in the way hits that instead, and one launched through smoke
    /// loses its target. Use from one thread.
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
            _damage = new EventWriter<DamageEvent>(ref state);
            _cues = new CueWriter(ref state);
        }

        /// <summary>Call each update before scheduling, with this frame's buffer.</summary>
        public void Update(ref SystemState state, EntityCommandBuffer ecb)
        {
            _transforms.Update(ref state);
            _spawner.Update(ref state);
            _stealth.Update(ref state);
            _deployables.Update(ref state);
            _muzzles.Update(ref state);
            _inside.Update(ref state);
            _ports.Update(ref state);
            _poses.Update(ref state);
            _damage.Update(ref state);
            _cues.Update(ref state);
            _ecb = ecb;
            new EntityQueryBuilder(Allocator.Temp).WithAll<SightBlockers>().Build(ref state)
                .TryGetSingleton(out _blockers);
        }

        /// <summary>
        /// The fire step both the primary and the mounts take: deployed if it must be, off cooldown, a turret on target,
        /// restart the cooldown or step the
        /// salvo, and fire from the next muzzle.
        /// </summary>
        public bool TryFire(Entity shooter, ref Weapon weapon, byte pose, Entity target, byte faction)
        {
            if (Deployable.Holds(_deployables, shooter, weapon.NeedsDeploy))
            {
                weapon.EndSalvo();
                return false;
            }

            if (weapon.CoolingDown || !Aimed(shooter, weapon, pose, target))
            {
                return false;
            }

            var shot = TakeMuzzle(shooter, ref weapon);
            weapon.StartCooldown();
            Fire(shooter, shot, pose, target, faction);
            return true;
        }

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

        /// <summary>Fires the weapon at <see cref="WeaponPose"/> index <paramref name="pose"/>, counting the shot there.</summary>
        private void Fire(Entity shooter, in Weapon weapon, byte pose, Entity target, byte faction)
        {
            Reveal(shooter);
            var poses = _poses[shooter];
            var fired = poses[pose];
            fired.CountShot();
            poses[pose] = fired;
            var place = _transforms[shooter];
            var targetPosition = _transforms[target].Position;
            var shot = weapon;
            if (TryFindPort(shooter, targetPosition, out var port))
            {
                place = port;
                shot.Aim.HasMuzzle = true;
                shot.Aim.Muzzle = float3.zero;
                fired.YawAngle = 0f;
                fired.PitchAngle = 0f;
            }

            PlayFire(shooter, shot, fired, pose, place, targetPosition, faction);
            if (weapon.ProjectilePrefab == Entity.Null)
            {
                var origin = shot.Aim.HitOrigin(place, fired);
                var hit = weapon.Hit(shooter, faction, origin, target, targetPosition);
                _damage.Add(hit);
                var along = math.normalizesafe(targetPosition - origin, place.Forward());
                hit.AddImpact(ref _cues, _cues.TypeIdFor(shooter, CueSlot.Impact), along);
            }
            else
            {
                Obstruct(shooter, shot, fired, place, ref target, ref targetPosition);
                Launch(shooter, shot, fired, place, target, targetPosition, faction);
            }
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
        private void PlayFire(Entity shooter, in Weapon weapon, in WeaponPose aimed, byte pose,
            in LocalTransform transform, float3 targetPosition, byte faction)
        {
            var toward = targetPosition - transform.Position;
            toward.y = 0f;
            weapon.Aim.PlayFire(ref _cues, shooter, transform, aimed, math.normalizesafe(toward, transform.Forward()), faction,
                pose);
        }

        private void Reveal(Entity shooter)
        {
            if (_stealth.TryGetRefRW(shooter, out var stealth))
            {
                stealth.ValueRW.RevealTimer = stealth.ValueRO.RevealDuration;
            }
        }

        private void Launch(Entity shooter, in Weapon weapon, in WeaponPose pose, in LocalTransform place,
            Entity target, float3 targetPosition, byte faction)
        {
            var origin = weapon.Aim.LaunchPoint(place, pose);
            var seed = math.hash(new uint3((uint)shooter.Index, (uint)target.Index, pose.Shots));
            var aim = weapon.Scatter(weapon.Aim.TargetPoint(targetPosition), seed);

            // Smoke thick enough to hide the target breaks a guided round's lock; it flies on to the aim point.
            var locked = weapon.KeepsLock && !(_blockers.IsCreated && _blockers.Haze(origin, aim) >= 1f);
            var flight = new Projectile
            {
                Speed = weapon.ProjectileSpeed,
                Height = weapon.Aim.ProjectileHeight,
                ImpactTypeId = _cues.TypeIdFor(shooter, CueSlot.Impact),
                Hit = weapon.Hit(shooter, faction, origin, locked ? target : Entity.Null, aim),
                Trajectory = weapon.Aim.Trajectory,
                Kind = weapon.ProjectileKind,
                Start = origin,
                Apex = weapon.Aim.ArcHeight * math.distance(origin.xz, aim.xz),
            };
            var heading = flight.Arcs ? flight.Heading() : aim - origin;

            var prefab = weapon.ProjectilePrefab;
            var projectile = _spawner.Spawn(_ecb, prefab, origin,
                quaternion.LookRotationSafe(heading, math.up()), faction);
            _ecb.AddComponent(projectile, flight);
        }
    }
}
