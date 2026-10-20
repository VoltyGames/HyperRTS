using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Points each <see cref="WeaponPose"/> at its weapon's target: the primary at the <see cref="AttackTarget"/>,
    /// mounts at their own. Arcing weapons pitch up to their launch angle; idle weapons ease back to centre.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(MountFireSystem))]
    public partial struct WeaponPoseSystem : ISystem
    {
        /// <summary>Yaw degrees per second an idle weapon turns back to centre.</summary>
        public const float SettleRate = 90f;

        private ComponentLookup<LocalTransform> _transforms;
        private BufferLookup<WeaponMount> _mounts;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _transforms = state.GetComponentLookup<LocalTransform>(true);
            _mounts = state.GetBufferLookup<WeaponMount>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _transforms.Update(ref state);
            _mounts.Update(ref state);
            var degrees = SettleRate * SystemAPI.Time.DeltaTime;
            new PoseJob
            {
                Transforms = _transforms,
                Mounts = _mounts,
                SettleSteps = math.max(1, (int)(degrees * HeadingByte.Steps / 360f)),
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithPresent(typeof(AttackTarget))]
        private partial struct PoseJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
            [ReadOnly] public BufferLookup<WeaponMount> Mounts;
            public int SettleSteps;

            private void Execute(Entity entity, DynamicBuffer<WeaponPose> poses, in Weapon weapon,
                in AttackTarget attack, EnabledRefRO<AttackTarget> attacking)
            {
                var hull = Transforms[entity];
                var primary = attacking.ValueRO ? attack.Value : Entity.Null;
                Pose(poses, 0, hull, weapon, primary);
                if (!Mounts.TryGetBuffer(entity, out var mounts))
                {
                    return;
                }

                foreach (var mount in mounts)
                {
                    Pose(poses, mount.Pose, hull, mount.Weapon, mount.Target);
                }
            }

            private void Pose(DynamicBuffer<WeaponPose> poses, int index, in LocalTransform hull, in Weapon weapon,
                Entity target)
            {
                var pose = poses[index];
                if (Transforms.TryGetComponent(target, out var aimed))
                {
                    var from = weapon.LaunchPoint(hull);
                    var aim = aimed.Position + new float3(0f, weapon.ProjectileHeight, 0f);
                    pose.Aim(math.rotate(math.inverse(hull.Rotation), aim - from), weapon.Elevation(from, aim));
                }
                else
                {
                    pose.Settle(SettleSteps);
                }

                poses[index] = pose;
            }
        }
    }
}
