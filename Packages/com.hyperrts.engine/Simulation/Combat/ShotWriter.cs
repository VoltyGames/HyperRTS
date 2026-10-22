using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Vision;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Fires a weapon from a job: the fire cue, an instant hit or a launched <see cref="Projectile"/>, and the stealth
    /// reveal. Owns the one writable transform lookup, so shooters turn through it too. Use from one thread.
    /// </summary>
    public struct ShotWriter
    {
        private ComponentLookup<LocalTransform> _transforms;
        [ReadOnly] private ComponentLookup<PostTransformMatrix> _stretches;
        private ComponentLookup<Stealth> _stealth;
        private ComponentLookup<Ammo> _ammo;
        private BufferLookup<WeaponPose> _poses;
        private DamageWriter _damage;
        private CueWriter _cues;
        private EntityCommandBuffer _ecb;

        public ShotWriter(ref SystemState state) : this()
        {
            _transforms = state.GetComponentLookup<LocalTransform>();
            _stretches = state.GetComponentLookup<PostTransformMatrix>(true);
            _stealth = state.GetComponentLookup<Stealth>();
            _ammo = state.GetComponentLookup<Ammo>();
            _poses = state.GetBufferLookup<WeaponPose>();
            _damage = new DamageWriter(ref state);
            _cues = new CueWriter(ref state);
        }

        /// <summary>Call each update before scheduling, with the damage and cue queues and this frame's buffer.</summary>
        public void Update(ref SystemState state, Entity damageQueue, Entity cueQueue, EntityCommandBuffer ecb)
        {
            _transforms.Update(ref state);
            _stretches.Update(ref state);
            _stealth.Update(ref state);
            _ammo.Update(ref state);
            _poses.Update(ref state);
            _damage.Update(ref state, damageQueue);
            _cues.Update(ref state, cueQueue);
            _ecb = ecb;
        }

        public float3 Position(Entity entity) => _transforms[entity].Position;

        /// <summary>Turns the shooter level toward the target.</summary>
        public void Face(Entity shooter, float3 targetPosition)
        {
            var transform = _transforms[shooter];
            var direction = targetPosition - transform.Position;
            direction.y = 0f;
            if (math.lengthsq(direction) > 1e-6f)
            {
                transform.Rotation = quaternion.LookRotationSafe(direction, math.up());
                _transforms[shooter] = transform;
            }
        }

        /// <summary>Spends one of the shooter's <see cref="Ammo"/> rounds; shooters without ammo never run dry.</summary>
        public bool TryUseRound(Entity shooter)
        {
            if (!_ammo.TryGetRefRW(shooter, out var ammo))
            {
                return true;
            }

            if (ammo.ValueRO.Current <= 0)
            {
                return false;
            }

            ammo.ValueRW.Current--;
            return true;
        }

        /// <summary>
        /// The fire step both the primary and the mounts take: off cooldown, a turret on target, spend a round (the
        /// shooter's <see cref="Ammo"/> when <paramref name="usesShooterAmmo"/>), restart the cooldown and fire.
        /// </summary>
        public bool TryFire(Entity shooter, ref Weapon weapon, byte pose, Entity target, byte faction,
            bool usesShooterAmmo)
        {
            if (weapon.CoolingDown || !Aimed(shooter, weapon, pose, target))
            {
                return false;
            }

            if (usesShooterAmmo && !TryUseRound(shooter))
            {
                return false;
            }

            weapon.StartCooldown();
            Fire(shooter, weapon, pose, target, faction);
            return true;
        }

        /// <summary>A hull-fixed weapon fires as the hull faces; a turret once its pose points at the target.</summary>
        private bool Aimed(Entity shooter, in Weapon weapon, byte pose, Entity target)
        {
            if (!weapon.Turreted)
            {
                return true;
            }

            var aimed = _poses[shooter][pose];
            var aim = _transforms[target].Position + new float3(0f, weapon.ProjectileHeight, 0f);
            return weapon.Aligned(aimed, weapon.AimAngles(_transforms[shooter], aimed, aim));
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
            PlayFire(shooter, pose, place, targetPosition, faction);
            if (weapon.ProjectilePrefab == Entity.Null)
            {
                var origin = weapon.HitOrigin(place, fired);
                var hit = CombatMath.Hit(weapon, shooter, faction, origin, target, targetPosition);
                _damage.Add(hit);
                var along = math.normalizesafe(targetPosition - origin, place.Forward());
                _cues.AddImpact(_cues.TypeIdFor(shooter, CueSlot.Impact), hit, along);
            }
            else
            {
                Launch(shooter, weapon, fired, place, target, targetPosition, faction);
            }
        }

        /// <summary>The fire cue faces the target level, the way the shooter turned.</summary>
        private void PlayFire(Entity shooter, byte pose, in LocalTransform transform, float3 targetPosition,
            byte faction)
        {
            var toward = targetPosition - transform.Position;
            toward.y = 0f;
            var direction = math.normalizesafe(toward, transform.Forward());
            _cues.Play(shooter, CueSlot.Fire, transform, direction, faction, pose);
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
            var origin = weapon.LaunchPoint(place, pose);
            var aim = targetPosition + new float3(0f, weapon.ProjectileHeight, 0f);

            var flight = new Projectile
            {
                Speed = weapon.ProjectileSpeed,
                Height = weapon.ProjectileHeight,
                ImpactTypeId = _cues.TypeIdFor(shooter, CueSlot.Impact),
                Hit = CombatMath.Hit(weapon, shooter, faction, origin, target, aim),
                Trajectory = weapon.Trajectory,
                Start = origin,
                Apex = weapon.ArcHeight * math.distance(origin.xz, aim.xz),
            };
            var heading = flight.Arcs ? flight.Heading() : aim - origin;

            var prefab = weapon.ProjectilePrefab;
            var projectile = _ecb.Spawn(prefab, _transforms[prefab], _stretches, origin,
                quaternion.LookRotationSafe(heading, math.up()), faction);
            _ecb.AddComponent(projectile, flight);
        }
    }
}
