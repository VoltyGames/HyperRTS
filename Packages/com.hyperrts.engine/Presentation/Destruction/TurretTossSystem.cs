using HyperRTS.Presentation.Animation;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Rendering.Parts;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Death;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>
    /// Flies a <see cref="TossedTurret"/>'s models from the hull to where the simulation already put it, on an arc
    /// with spin, by moving them under the remains. A toss the local player does not see lands at once.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PoseSystemGroup))]
    public partial class TurretTossSystem : SystemBase
    {
        private static readonly float3 TumbleAxis = math.normalize(new float3(0.4f, 1f, 0.3f));

        private EntityQuery _thrown;
        private EntityQuery _flying;
        private CueVisibility _visibility;

        protected override void OnCreate()
        {
            _thrown = SystemAPI.QueryBuilder().WithAll<TossedTurret>().WithAbsent<TossClock>().Build();
            _flying = SystemAPI.QueryBuilder().WithAll<TossedTurret, TossClock, LocalTransform>().Build();
            _visibility = new CueVisibility(ref CheckedStateRef);
            RequireForUpdate<TossedTurret>();
        }

        protected override void OnUpdate()
        {
            CompleteDependency();
            Start();
            if (_flying.IsEmpty)
            {
                return;
            }

            var deltaTime = SystemAPI.Time.DeltaTime;
            using var entities = _flying.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                var clock = SystemAPI.GetComponentRW<TossClock>(entity);
                var toss = SystemAPI.GetComponent<TossedTurret>(entity);
                clock.ValueRW.Elapsed += deltaTime;
                var landed = clock.ValueRO.Elapsed >= toss.Duration;
                Fly(entity, toss, clock.ValueRO.Elapsed, landed);
                SystemAPI.SetComponentEnabled<TossClock>(entity, !landed);
            }
        }

        /// <summary>Clocks each new toss from the frame it appears; one nobody sees is left lying.</summary>
        private void Start()
        {
            if (_thrown.IsEmpty)
            {
                return;
            }

            using var entities = _thrown.ToEntityArray(Allocator.Temp);
            var types = new ComponentTypeSet(ComponentType.ReadWrite<TossClock>(), ComponentType.ReadWrite<FallPart>());
            EntityManager.AddComponent(_thrown, types);
            _visibility.Begin();
            var linked = GetBufferLookup<LinkedEntityGroup>(true);
            var children = GetBufferLookup<Child>(true);
            var parents = GetComponentLookup<Parent>(true);
            var transforms = GetComponentLookup<LocalTransform>(true);
            var members = new NativeList<Entity>(Allocator.Temp);
            foreach (var entity in entities)
            {
                var place = SystemAPI.GetComponent<LocalTransform>(entity);
                if (!_visibility.IsSeen(place.Position))
                {
                    SystemAPI.SetComponentEnabled<TossClock>(entity, false);
                    continue;
                }

                RenderHierarchy.Collect(entity, linked, children, members);
                FallPart.Record(entity, members, parents, transforms, SystemAPI.GetBuffer<FallPart>(entity));
            }
        }

        private void Fly(Entity remains, in TossedTurret toss, float elapsed, bool landed)
        {
            var share = landed ? 1f : elapsed / toss.Duration;
            var place = SystemAPI.GetComponent<LocalTransform>(remains);
            var lift = new float3(0f, 4f * toss.Height * share * (1f - share), 0f);
            var offset = math.lerp(toss.From - place.Position, float3.zero, share) + lift;
            var local = math.rotate(math.inverse(place.Rotation), offset);
            var spin = quaternion.AxisAngle(TumbleAxis, landed ? 0f : toss.Spin * elapsed);
            foreach (var part in SystemAPI.GetBuffer<FallPart>(remains))
            {
                var moved = part.Turned(spin);
                moved.Position += local;
                SystemAPI.SetComponent(part.Part, moved);
            }
        }
    }
}
