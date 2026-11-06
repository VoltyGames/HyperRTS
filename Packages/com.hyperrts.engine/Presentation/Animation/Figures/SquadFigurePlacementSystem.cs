using HyperRTS.Core;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Infantry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Animation.Figures
{
    /// <summary>
    /// Stands each living <see cref="SquadFigure"/> where its <see cref="SquadMember"/> is, eased, and turns it to its
    /// cover, in the same transform pass as its squad so the two never differ by a frame. The clips are
    /// <see cref="SquadFigureSystem"/>'s.
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
                Deployables = SystemAPI.GetComponentLookup<Deployable>(true),
                Parents = SystemAPI.GetComponentLookup<Parent>(true),
                Spots = SystemAPI.GetComponentLookup<LocalTransform>(),
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(SquadFigures))]
        private partial struct PlaceJob : IJobEntity
        {
            public float DeltaTime;
            [ReadOnly] public ComponentLookup<Deployable> Deployables;
            [ReadOnly] public ComponentLookup<Parent> Parents;
            public ComponentLookup<LocalTransform> Spots;

            private void Execute(Entity entity, DynamicBuffer<SquadFigure> figures, in DynamicBuffer<SquadMember> members)
            {
                var crewing = Deployables.TryGetComponent(entity, out var deployable) && deployable.Emplaced;
                var count = math.min(figures.Length, members.Length);
                for (var i = 0; i < count; i++)
                {
                    if (!members[i].Alive)
                    {
                        continue;
                    }

                    var figure = figures[i];
                    Stand(entity, ref figure, members[i], crewing && figure.Crew.Takes);
                    figures[i] = figure;
                }
            }

            /// <summary>
            /// Eases the figure toward its member's offset, or its crew place while <paramref name="crewed"/>, and moves
            /// it there: world axes turned into its parent's frame, at its baked height, turned to its cover.
            /// </summary>
            private void Stand(Entity squad, ref SquadFigure figure, in SquadMember member, bool crewed)
            {
                var parentToWorld = new float3x3(ParentToWorld(squad, figure.Figure));
                var worldToParent = math.inverse(parentToWorld);
                var goal = member.Offset;
                if (crewed)
                {
                    goal = math.mul(parentToWorld, new float3(figure.Crew.Offset.x, 0f, figure.Crew.Offset.y)).xz;
                }

                figure.Follow(goal, DeltaTime);
                var stance = member;
                if (crewed)
                {
                    stance.Cover = CoverKind.None;
                }

                var local = math.mul(worldToParent, new float3(figure.Offset.x, 0f, figure.Offset.y));
                var spot = Spots[figure.Figure];
                if (!figure.Primed)
                {
                    figure.Baked = spot.Rotation;
                }

                spot.Position = new float3(local.x, figure.Height, local.z);
                var parentRotation = quaternion.LookRotationSafe(parentToWorld.c2, parentToWorld.c1);
                figure.Turn(parentRotation, stance, DeltaTime);
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
