using HyperRTS.Simulation.Combat.Projectiles;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Cues;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Weapons
{
    /// <summary>How a weapon points its <see cref="WeaponPose"/> and where its shots leave from.</summary>
    public struct WeaponAim
    {
        /// <summary>Shot spawn point in the shooter's local space, used when <see cref="HasMuzzle"/>.</summary>
        public float3 Muzzle;

        public bool HasMuzzle;

        /// <summary>Points in the shooter's space the muzzle traverses and elevates about.</summary>
        public float3 TurretPivot;
        public float3 BarrelPivot;

        /// <summary>A turret aims instead of the hull turning to fire, and fires once it is on target.</summary>
        public bool Turreted;

        /// <summary>Radians per second the pose turns and raises.</summary>
        public float TraverseRate;
        public float ElevationRate;

        /// <summary>Elevation limits in radians, within the pose's -96 to 95 degrees.</summary>
        public float MinPitch;
        public float MaxPitch;

        /// <summary>Radians off target, per axis, a turret or a hull-fixed weapon's hull may fire at.</summary>
        public float AimTolerance;

        /// <summary>Height above the shooter's and target's pivots that projectiles fly at; 0 flies pivot to pivot.</summary>
        public float ProjectileHeight;

        public Trajectory Trajectory;

        /// <summary>Apex of an arcing shot as a fraction of the launch-to-aim ground distance.</summary>
        public float ArcHeight;

        /// <summary>Where a projectile starts: the posed muzzle, else <see cref="ProjectileHeight"/> above the pivot.</summary>
        public float3 LaunchPoint(in LocalTransform shooter, in WeaponPose pose) =>
            HasMuzzle ? shooter.TransformPoint(PosedMuzzle(pose)) : shooter.Position + new float3(0f, ProjectileHeight, 0f);

        /// <summary>Plays the Fire cue at the posed muzzle, else at the shooter's Fire socket or pivot.</summary>
        public void PlayFire(ref CueWriter cues, in Firing firing, float3 direction)
        {
            if (HasMuzzle)
            {
                cues.PlayAt(firing.Shooter, CueSlot.Fire, LaunchPoint(firing.Place, firing.Pose), direction,
                    firing.Faction, firing.PoseIndex);
                return;
            }

            cues.Play(firing.Shooter, CueSlot.Fire, firing.Place, direction, firing.Faction, firing.PoseIndex);
        }

        /// <summary>Where the weapon aims on a target standing at <paramref name="target"/>.</summary>
        public float3 TargetPoint(float3 target) => target + new float3(0f, ProjectileHeight, 0f);

        /// <summary>Where an instant hit comes from, for directional armor.</summary>
        public float3 HitOrigin(in LocalTransform shooter, in WeaponPose pose) =>
            HasMuzzle ? shooter.TransformPoint(PosedMuzzle(pose)) : shooter.Position;

        /// <summary>Bakes the muzzle and pivots from child transforms of <paramref name="root"/>; a missing barrel pivots at the muzzle.</summary>
        public void BakeSockets(IBaker baker, UnityEngine.Transform root, UnityEngine.Transform muzzle,
            UnityEngine.Transform turret, UnityEngine.Transform barrel)
        {
            if (muzzle != null)
            {
                Muzzle = AuthoringBehaviour.Socket(baker, root, muzzle);
                HasMuzzle = true;
            }

            TurretPivot = AuthoringBehaviour.Socket(baker, root, turret);
            BarrelPivot = barrel != null ? AuthoringBehaviour.Socket(baker, root, barrel) : Muzzle;
        }

        /// <summary>The muzzle raised about the barrel pivot, then turned about the turret pivot.</summary>
        public float3 PosedMuzzle(in WeaponPose pose)
        {
            var raised = BarrelPivot + math.rotate(quaternion.AxisAngle(math.right(), -pose.PitchAngle), Muzzle - BarrelPivot);
            return TurretPivot + math.rotate(quaternion.AxisAngle(math.up(), pose.YawAngle), raised - TurretPivot);
        }

        /// <summary>Yaw and pitch that point the posed weapon at <paramref name="aim"/>; a hull-fixed weapon keeps yaw 0.</summary>
        public float2 Angles(in LocalTransform shooter, in WeaponPose pose, float3 aim)
        {
            var from = LaunchPoint(shooter, pose);
            var local = math.rotate(math.inverse(shooter.Rotation), aim - from);
            var yaw = Turreted ? math.atan2(local.x, local.z) : 0f;
            return new float2(yaw, math.clamp(Elevation(from, aim), MinPitch, MaxPitch));
        }

        /// <summary>Turns <paramref name="pose"/> toward <paramref name="angles"/> at the traverse and elevation rates.</summary>
        public void Turn(ref WeaponPose pose, float2 angles, float deltaTime) =>
            pose.Turn(angles, new float2(TraverseRate, ElevationRate) * deltaTime);

        /// <summary>The pose points within <see cref="AimTolerance"/> of <paramref name="angles"/> on both axes.</summary>
        public bool Aligned(in WeaponPose pose, float2 angles) => math.all(pose.Gap(angles) <= AimTolerance);

        /// <summary>Radians above level to fire from <paramref name="from"/> at <paramref name="aim"/>, lobbed if arcing.</summary>
        public float Elevation(float3 from, float3 aim)
        {
            var ground = math.max(math.distance(from.xz, aim.xz), 0.01f);
            var slope = (aim.y - from.y) / ground;
            // An arc of apex ArcHeight times the ground distance leaves at a slope of 4 ArcHeight above the chord.
            return math.atan(Trajectory == Trajectory.Direct ? slope : slope + 4f * ArcHeight);
        }
    }
}
