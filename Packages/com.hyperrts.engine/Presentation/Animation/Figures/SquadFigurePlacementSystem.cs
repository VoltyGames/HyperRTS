using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Infantry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Animation.Figures
{
    /// <summary>
    /// Stands each living <see cref="SquadFigure"/> where its <see cref="SquadMember"/> is, in the same transform pass as its
    /// squad so the two never differ by a frame.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PoseSystemGroup))]
    public partial struct SquadFigurePlacementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<SquadFigures>();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new PlaceJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Parents = SystemAPI.GetComponentLookup<Parent>(true),
                Poses = SystemAPI.GetBufferLookup<WeaponPose>(true),
                Spots = SystemAPI.GetComponentLookup<LocalTransform>(),
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(SquadFigures))]
        private partial struct PlaceJob : IJobEntity
        {
            public float DeltaTime;
            [ReadOnly] public ComponentLookup<Parent> Parents;
            [ReadOnly] public BufferLookup<WeaponPose> Poses;
            public ComponentLookup<LocalTransform> Spots;

            private void Execute(Entity entity, DynamicBuffer<SquadFigure> figures, in DynamicBuffer<SquadMember> members)
            {
                var count = math.min(figures.Length, members.Length);
                var hasPoses = Poses.TryGetBuffer(entity, out var poses);
                for (var i = 0; i < count; i++)
                {
                    var member = members[i];
                    if (!member.Alive)
                    {
                        continue;
                    }

                    var figure = figures[i];
                    var aim = figure.Firing && hasPoses && member.Weapon < poses.Length ? poses[member.Weapon].YawRadians : 0f;
                    Stand(entity, ref figure, member, aim);
                    figures[i] = figure;
                }
            }

            /// <summary>
            /// Eases the figure toward its member's offset and moves it there: world axes turned into its parent's
            /// frame, at its baked height, turned to its cover or to its weapon's <paramref name="aim"/> while firing.
            /// </summary>
            private void Stand(Entity squad, ref SquadFigure figure, in SquadMember member, float aim)
            {
                var parentToWorld = new float3x3(ParentToWorld(squad, figure.Figure));
                var worldToParent = math.inverse(parentToWorld);
                figure.Follow(member.Offset, DeltaTime);
                var local = math.mul(worldToParent, new float3(figure.Offset.x, 0f, figure.Offset.y));
                var spot = Spots[figure.Figure];
                spot.Position = new float3(local.x, figure.Height, local.z);
                var parentRotation = quaternion.LookRotationSafe(parentToWorld.c2, parentToWorld.c1);
                figure.Turn(parentRotation, member, aim, DeltaTime);
                spot.Rotation = math.mul(math.inverse(parentRotation), figure.Facing);
                Spots[figure.Figure] = spot;
            }

            // The squad is top-level, so its LocalTransform is this frame's pose; its parts' LocalToWorld is last frame's.
            private float4x4 ParentToWorld(Entity squad, Entity figure)
            {
                var matrix = float4x4.identity;
                var link = Parents[figure].Value;
                while (link != squad)
                {
                    matrix = math.mul(Spots[link].ToMatrix(), matrix);
                    link = Parents[link].Value;
                }

                return math.mul(Spots[squad].ToMatrix(), matrix);
            }
        }
    }
}
