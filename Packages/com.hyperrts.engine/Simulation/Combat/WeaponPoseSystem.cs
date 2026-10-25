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
    /// Turns each <see cref="WeaponPose"/> toward its weapon's target at the weapon's traverse and elevation rates:
    /// the primary at the <see cref="AttackTarget"/>, mounts at their own. Arcing weapons pitch up to their launch
    /// angle; idle weapons, and those waiting for their unit to deploy, turn back to centre. Runs before firing, so
    /// turrets fire once on target.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(EngagementSystem))]
    [UpdateBefore(typeof(WeaponFireSystem))]
    public partial struct WeaponPoseSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transforms;
        private BufferLookup<WeaponMount> _mounts;
        private ComponentLookup<Deployable> _deployables;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _transforms = state.GetComponentLookup<LocalTransform>(true);
            _mounts = state.GetBufferLookup<WeaponMount>(true);
            _deployables = state.GetComponentLookup<Deployable>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _transforms.Update(ref state);
            _mounts.Update(ref state);
            _deployables.Update(ref state);
            new PoseJob
            {
                Transforms = _transforms,
                Mounts = _mounts,
                Deployables = _deployables,
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithPresent(typeof(AttackTarget))]
        private partial struct PoseJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
            [ReadOnly] public BufferLookup<WeaponMount> Mounts;
            [ReadOnly] public ComponentLookup<Deployable> Deployables;
            public float DeltaTime;

            private void Execute(Entity entity, DynamicBuffer<WeaponPose> poses, in Weapon weapon,
                in AttackTarget attack, EnabledRefRO<AttackTarget> attacking)
            {
                var hull = Transforms[entity];
                var primary = attacking.ValueRO ? attack.Value : Entity.Null;
                Pose(poses, 0, hull, weapon, Aimed(entity, weapon, primary));
                if (!Mounts.TryGetBuffer(entity, out var mounts))
                {
                    return;
                }

                foreach (var mount in mounts)
                {
                    Pose(poses, mount.Pose, hull, mount.Weapon, Aimed(entity, mount.Weapon, mount.Target));
                }
            }

            // A packed-up launcher keeps its turret stowed until it has deployed.
            private Entity Aimed(Entity unit, in Weapon weapon, Entity target) =>
                Deployable.Holds(Deployables, unit, weapon) ? Entity.Null : target;

            private void Pose(DynamicBuffer<WeaponPose> poses, int index, in LocalTransform hull, in Weapon weapon,
                Entity target)
            {
                var pose = poses[index];
                // Authoring keeps level inside every pitch range, so rest is straight ahead and level.
                var angles = float2.zero;
                if (Transforms.TryGetComponent(target, out var aimed))
                {
                    angles = weapon.AimAngles(hull, pose, weapon.AimPoint(aimed.Position));
                }

                weapon.Turn(ref pose, angles, DeltaTime);
                poses[index] = pose;
            }
        }
    }
}
