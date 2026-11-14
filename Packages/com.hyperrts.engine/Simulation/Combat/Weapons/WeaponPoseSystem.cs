using HyperRTS.Simulation.Combat.Deploy;
using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Common.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Weapons
{
    /// <summary>
    /// Turns each <see cref="WeaponPose"/> toward its weapon's target at the weapon's traverse and elevation rates,
    /// before firing so turrets fire once on target.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(WeaponTargetSystem))]
    [UpdateBefore(typeof(WeaponFireSystem))]
    public partial struct WeaponPoseSystem : ISystem
    {

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new PoseJob
            {
                Transforms = SystemAPI.GetComponentLookup<LocalTransform>(true),
                Deployables = SystemAPI.GetComponentLookup<Deployable>(true),
                PointAims = SystemAPI.GetComponentLookup<PointAim>(true),
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithPresent(typeof(AttackTarget))]
        private partial struct PoseJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
            [ReadOnly] public ComponentLookup<Deployable> Deployables;
            [ReadOnly] public ComponentLookup<PointAim> PointAims;
            public float DeltaTime;

            private void Execute(Entity entity, DynamicBuffer<WeaponPose> poses, in DynamicBuffer<WeaponMount> mounts,
                in AttackTarget attack, EnabledRefRO<AttackTarget> attacking)
            {
                var hull = Transforms[entity];
                var primary = attacking.ValueRO ? attack.Value : Entity.Null;
                var ordered = PointAims.HasEnabled(entity);
                var point = ordered ? PointAims[entity].Value : default;
                for (var i = 0; i < mounts.Length; i++)
                {
                    var mount = mounts[i];
                    // The primary tracks the attack target while it closes in; extra weapons and interceptors only what they shoot.
                    var tracksAttack = i == 0 && !mount.Weapon.ShootsProjectiles;
                    var target = tracksAttack ? primary : mount.Target;
                    var aim = ShotAim.Choose(target, Transforms, mount.Weapon, ordered && i == 0, point);
                    var rest = Deployable.IsDeployed(Deployables, entity) ? mount.Weapon.Aim.DeployedPitch : 0f;
                    Pose(poses, i, hull, mount.Weapon, Holds(entity, mount.Weapon) ? default : aim, rest);
                }
            }

            // A packed-up launcher keeps its turret stowed until it has deployed.
            private bool Holds(Entity unit, in Weapon weapon) => Deployable.Holds(Deployables, unit, weapon.NeedsDeploy);

            // With nothing to aim at it rests straight ahead at restPitch: level, or raised on a deployed launcher.
            private void Pose(DynamicBuffer<WeaponPose> poses, int index, in LocalTransform hull, in Weapon weapon,
                in ShotAim aim, float restPitch)
            {
                var pose = poses[index];
                var angles = new float2(0f, restPitch);
                if (aim.Exists)
                {
                    angles = weapon.Aim.Angles(hull, pose, weapon.Aim.TargetPoint(aim.Point));
                }

                weapon.Aim.Turn(ref pose, angles, DeltaTime);
                poses[index] = pose;
            }
        }
    }
}
