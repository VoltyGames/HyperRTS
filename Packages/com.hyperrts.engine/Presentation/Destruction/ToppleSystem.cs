using HyperRTS.Presentation.Animation;
using HyperRTS.Presentation.Rendering.Parts;
using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Destruction;
using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>
    /// Shows felled remains tipping down to the final pose the simulation already gave them, by turning their child
    /// models about the stump. A crushing vehicle's speed pushes the fall on. Unseen falls show lying down at once.
    /// </summary>
    // Children turn inside the root's scale and stretch, which stay as simulated.
    [BurstCompile]
    [UpdateInGroup(typeof(PoseSystemGroup))]
    public partial struct ToppleSystem : ISystem
    {
        private EntityQuery _fog;
        private EntityQuery _felled;
        private EntityQuery _falling;
        private EntityQuery _crushers;

        // How far from the stump a crusher counts as the one pushing the tree over.
        private const float CrushReach = 6f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _fog = SystemAPI.QueryBuilder().WithAll<FogOfWar>().Build();
            _felled = SystemAPI.QueryBuilder().WithAll<ToppleFall>().WithAbsent<FallClock>().Build();
            _falling = SystemAPI.QueryBuilder().WithAll<ToppleFall, FallClock, FallPart, LocalTransform>().Build();
            _crushers = SystemAPI.QueryBuilder().WithAll<Crusher, MotionRig, LocalToWorld>().Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!_felled.IsEmpty)
            {
                var parts = new ComponentTypeSet(ComponentType.ReadWrite<FallClock>(), ComponentType.ReadWrite<FallPart>());
                state.EntityManager.AddComponent(_felled, parts);
            }

            // The clock's enable bit ends the work, so resting remains don't keep the fog synced.
            if (_falling.IsEmpty)
            {
                return;
            }

            var view = SystemAPI.TryGetSingleton(out LocalFogView local) ? local : default;
            state.CompleteDependency();
            _fog.TryGetSingleton(out FogOfWar fog);
            var deltaTime = SystemAPI.Time.DeltaTime;
            var falls = new Falls
            {
                Transforms = SystemAPI.GetComponentLookup<LocalTransform>(),
                Parents = SystemAPI.GetComponentLookup<Parent>(true),
                Groups = SystemAPI.GetBufferLookup<LinkedEntityGroup>(true),
                Children = SystemAPI.GetBufferLookup<Child>(true),
                CrusherQuery = _crushers,
                View = view,
                Fog = fog,
            };
            using var entities = _falling.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                var toppleFall = SystemAPI.GetComponent<ToppleFall>(entity);
                var clock = SystemAPI.GetComponentRW<FallClock>(entity);
                var parts = SystemAPI.GetBuffer<FallPart>(entity);
                if (!clock.ValueRO.Started && !falls.Start(entity, toppleFall, ref clock.ValueRW, parts))
                {
                    SystemAPI.SetComponentEnabled<FallClock>(entity, false);
                    continue;
                }

                clock.ValueRW.Advance(toppleFall.Height, deltaTime);
                var resting = clock.ValueRO.Resting;
                falls.Turn(entity, toppleFall, clock.ValueRO.Progress, resting, parts);
                SystemAPI.SetComponentEnabled<FallClock>(entity, !resting);
            }
        }

        /// <summary>One frame's lookups, fog view and crushers for starting and turning falls.</summary>
        private struct Falls
        {
            public ComponentLookup<LocalTransform> Transforms;
            public ComponentLookup<Parent> Parents;
            public BufferLookup<LinkedEntityGroup> Groups;
            public BufferLookup<Child> Children;
            public EntityQuery CrusherQuery;
            public Crushers Crushers;
            public LocalFogView View;
            public FogOfWar Fog;

            /// <summary>Starts a seen fall, pushed by any crusher at the stump; false when nobody saw it.</summary>
            public bool Start(Entity remains, in ToppleFall toppleFall, ref FallClock clock, DynamicBuffer<FallPart> parts)
            {
                var stump = Transforms[remains].Position;
                var shown = toppleFall.Witnessed && Seen(View, Fog, stump);
                if (!shown)
                {
                    return false;
                }

                Crushers.Fill(CrusherQuery);
                clock = FallClock.Pushed(toppleFall.Height, Crushers.Speed(stump));
                RecordParts(remains, parts);
                return true;
            }

            /// <summary>The remains' direct children at rest, found through the group since a fresh instance has no Child buffer.</summary>
            private void RecordParts(Entity remains, DynamicBuffer<FallPart> parts)
            {
                var group = new NativeList<Entity>(Allocator.Temp);
                RenderHierarchy.Collect(remains, Groups, Children, group);
                FallPart.Record(remains, group, Parents, Transforms, parts);
            }

            /// <summary>Turns the children so the remains show <see cref="ToppleMath.Pose"/>; resting puts them back exactly.</summary>
            public void Turn(Entity remains, in ToppleFall toppleFall, float progress, bool resting, DynamicBuffer<FallPart> parts)
            {
                var final = Transforms[remains].Rotation;
                var turn = resting ? quaternion.identity : math.mul(math.inverse(final), ToppleMath.Pose(toppleFall, final, progress));
                foreach (var part in parts)
                {
                    Transforms[part.Part] = part.Turned(turn);
                }
            }
        }

        /// <summary>Crushers' rigs and places, read once the first fall of a frame starts.</summary>
        private struct Crushers
        {
            private NativeArray<MotionRig> _rigs;
            private NativeArray<LocalToWorld> _places;

            public void Fill(EntityQuery query)
            {
                if (_rigs.IsCreated)
                {
                    return;
                }

                // Temp memory goes with the frame.
                _rigs = query.ToComponentDataArray<MotionRig>(Allocator.Temp);
                _places = query.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            }

            /// <summary>Speed in m/s of the fastest crusher at the stump, or 0 if none is there.</summary>
            public readonly float Speed(float3 stump)
            {
                var speed = 0f;
                for (var i = 0; i < _rigs.Length; i++)
                {
                    var near = math.distancesq(_places[i].Position.xz, stump.xz) < CrushReach * CrushReach;
                    speed = near ? math.max(speed, math.length(_rigs[i].LastVelocity.xz)) : speed;
                }

                return speed;
            }
        }

        /// <summary>Fog never hides scenery, but it does hide the fall.</summary>
        private static bool Seen(in LocalFogView view, in FogOfWar fog, float3 position)
        {
            if (!view.Active || view.RevealAll || !fog.IsCreated)
            {
                return true;
            }

            return fog.IsVisible(position, view.Team, true);
        }
    }
}
