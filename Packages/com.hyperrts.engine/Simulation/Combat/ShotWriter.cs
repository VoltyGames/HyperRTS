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
    /// reveal. Use from one thread.
    /// </summary>
    public struct ShotWriter
    {
        [ReadOnly] private ComponentLookup<LocalTransform> _transforms;
        [ReadOnly] private ComponentLookup<PostTransformMatrix> _stretches;
        private ComponentLookup<Stealth> _stealth;
        private ComponentLookup<Ammo> _ammo;
        [ReadOnly] private ComponentLookup<Deployable> _deployables;
        [ReadOnly] private BufferLookup<WeaponMuzzle> _muzzles;
        private BufferLookup<WeaponPose> _poses;
        private DamageWriter _damage;
        private CueWriter _cues;
        private EntityCommandBuffer _ecb;

        public ShotWriter(ref SystemState state) : this()
        {
            _transforms = state.GetComponentLookup<LocalTransform>(true);
            _stretches = state.GetComponentLookup<PostTransformMatrix>(true);
            _stealth = state.GetComponentLookup<Stealth>();
            _ammo = state.GetComponentLookup<Ammo>();
            _deployables = state.GetComponentLookup<Deployable>(true);
            _muzzles = state.GetBufferLookup<WeaponMuzzle>(true);
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
            _deployables.Update(ref state);
            _muzzles.Update(ref state);
            _poses.Update(ref state);
            _damage.Update(ref state, damageQueue);
            _cues.Update(ref state, cueQueue);
            _ecb = ecb;
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
        /// The fire step both the primary and the mounts take: deployed if it must be, off cooldown, a turret on target,
        /// spend a round (the shooter's <see cref="Ammo"/> when <paramref name="usesShooterAmmo"/>), restart the
        /// cooldown or step the salvo, and fire from the next muzzle.
        /// </summary>
        public bool TryFire(Entity shooter, ref Weapon weapon, byte pose, Entity target, byte faction,
            bool usesShooterAmmo)
        {
            if (Deployable.Holds(_deployables, shooter, weapon))
            {
                weapon.EndSalvo();
                return false;
            }

            if (weapon.CoolingDown || !Aimed(shooter, weapon, pose, target))
            {
                return false;
            }

            if (usesShooterAmmo && !TryUseRound(shooter))
            {
                return false;
            }

            var shot = NextMuzzle(shooter, ref weapon);
            weapon.StartCooldown();
            Fire(shooter, shot, pose, target, faction);
            return true;
        }

        /// <summary>The weapon as it fires this shot, from its next muzzle in turn.</summary>
        private Weapon NextMuzzle(Entity shooter, ref Weapon weapon)
        {
            var shot = weapon;
            if (weapon.MuzzleCount > 1)
            {
                shot.Muzzle = _muzzles[shooter][weapon.FirstMuzzle + weapon.NextMuzzle].Point;
                weapon.NextMuzzle = (byte)((weapon.NextMuzzle + 1) % weapon.MuzzleCount);
            }

            return shot;
        }

        /// <summary>A hull-fixed weapon fires as the hull faces; a turret once its pose points at the target.</summary>
        private bool Aimed(Entity shooter, in Weapon weapon, byte pose, Entity target)
        {
            if (!weapon.Turreted)
            {
                return true;
            }

            var aimed = _poses[shooter][pose];
            var aim = weapon.AimPoint(_transforms[target].Position);
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
            PlayFire(shooter, weapon, fired, pose, place, targetPosition, faction);
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

        /// <summary>The fire cue faces the target level, the way the shooter turned, from the muzzle as the pose turned it.</summary>
        private void PlayFire(Entity shooter, in Weapon weapon, in WeaponPose aimed, byte pose,
            in LocalTransform transform, float3 targetPosition, byte faction)
        {
            var toward = targetPosition - transform.Position;
            toward.y = 0f;
            weapon.PlayFire(ref _cues, shooter, transform, aimed, math.normalizesafe(toward, transform.Forward()), faction,
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
            var origin = weapon.LaunchPoint(place, pose);
            var seed = math.hash(new uint3((uint)shooter.Index, (uint)target.Index, pose.Shots));
            var aim = weapon.Scatter(weapon.AimPoint(targetPosition), seed);

            var flight = new Projectile
            {
                Speed = weapon.ProjectileSpeed,
                Height = weapon.ProjectileHeight,
                ImpactTypeId = _cues.TypeIdFor(shooter, CueSlot.Impact),
                Hit = CombatMath.Hit(weapon, shooter, faction, origin, target, aim),
                Trajectory = weapon.Trajectory,
                Kind = weapon.ProjectileKind,
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
