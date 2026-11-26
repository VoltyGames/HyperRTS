using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Navigation.Grid;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Animation.Figures
{
    /// <summary>
    /// Stands each living <see cref="SquadFigure"/> where its <see cref="SquadMember"/> is, on the ground under its own
    /// feet, in the same transform pass as its squad so the two never differ by a frame.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PoseSystemGroup))]
    public partial struct SquadFigurePlacementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SquadFigures>();
            state.RequireForUpdate<NavGrid>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new PlaceJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Grid = SystemAPI.GetSingleton<NavGrid>(),
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
            [ReadOnly] public NavGrid Grid;
            [ReadOnly] public ComponentLookup<Parent> Parents;
            [ReadOnly] public BufferLookup<WeaponPose> Poses;
            public ComponentLookup<LocalTransform> Spots;

            private void Execute(Entity entity, DynamicBuffer<SquadFigure> figures, in DynamicBuffer<SquadMember> members)
            {
                var count = math.min(figures.Length, members.Length);
                var hasPoses = Poses.TryGetBuffer(entity, out var poses);
                var center = Spots[entity].Position.xz;
                var ground = NavGround.Footing(Grid, center, center);
                for (var i = 0; i < count; i++)
                {
                    var member = members[i];
                    if (!member.Alive)
                    {
                        continue;
                    }

                    var figure = figures[i];
                    var aim = figure.Firing && hasPoses && member.Weapon < poses.Length ? poses[member.Weapon].YawRadians : 0f;
                    Stand(entity, ref figure, member, aim, center, ground);
                    figures[i] = figure;
                }
            }

            /// <summary>
            /// Eases the figure toward its member's offset and moves it there: world axes turned into its parent's
            /// frame, at its baked height above its own ground, turned to its cover or to its weapon's
            /// <paramref name="aim"/> while firing. <paramref name="ground"/> is the footing under the squad's
            /// <paramref name="center"/>.
            /// </summary>
            private void Stand(Entity squad, ref SquadFigure figure, in SquadMember member, float aim, float2 center,
                float ground)
            {
                var parentToWorld = new float3x3(ParentToWorld(squad, figure.Figure));
                var worldToParent = math.inverse(parentToWorld);
                figure.Follow(member.Offset, DeltaTime);
                var local = math.mul(worldToParent, new float3(figure.Offset.x, Rise(center, figure.Offset, ground), figure.Offset.y));
                var spot = Spots[figure.Figure];
                spot.Position = new float3(local.x, figure.Height + local.y, local.z);
                var parentRotation = quaternion.LookRotationSafe(parentToWorld.c2, parentToWorld.c1);
                figure.Turn(parentRotation, member, aim, DeltaTime);
                spot.Rotation = math.mul(math.inverse(parentRotation), figure.Facing);
                Spots[figure.Figure] = spot;
            }

            // Metres the ground under the figure stands above the ground under its squad, which only the squad's own
            // height follows: on a slope the figures uphill would sink and the ones downhill float.
            private readonly float Rise(float2 center, float2 offset, float ground) =>
                NavGround.Footing(Grid, center + offset, center) - ground;

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
