using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Movement;
using HyperRTS.Simulation.Selection;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Profiling;
using Unity.Transforms;
using Collider = Unity.Physics.Collider;

namespace HyperRTS.Presentation.Physics
{
    /// <summary>
    /// Gives every building a static box and every vehicle a kinematic box in the presented physics world, sized by its
    /// <see cref="PickBox"/>, so ragdolls land on roofs and hulls. Vehicle boxes are driven to their unit each step.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(PhysicsGroup))]
    public partial class BoxColliderSystem : SystemBase
    {
        /// <summary>A vehicle further than this from its box in one step jumped there, so its box jumps too.</summary>
        public const float JumpDistance = 4f;

        private static readonly ProfilerMarker ScanMarker = new("BoxColliderSystem.Scan");

        private readonly Dictionary<Entity, Entity> _colliders = new();
        private readonly Dictionary<(float3, float3), BlobAssetReference<Collider>> _shapes = new();
        private readonly List<Entity> _gone = new();
        private EntityQuery _sources;
        private PhysicsGroup _physics;
        private uint _scanned;

        protected override void OnCreate()
        {
            _sources = new EntityQueryBuilder(Allocator.Temp).WithAll<PickBox, LocalTransform>()
                .WithAny<BuildingTag, Locomotion>().Build(this);
            _physics = World.GetOrCreateSystemManaged<PhysicsGroup>();
        }

        protected override void OnDestroy()
        {
            foreach (var shape in _shapes.Values)
            {
                shape.Dispose();
            }
        }

        protected override void OnUpdate()
        {
            if (!_physics.Enabled)
            {
                return;
            }

            // Units come and go far less often than physics steps.
            var order = (uint)EntityManager.GetComponentOrderVersion<PickBox>();
            if (order != _scanned)
            {
                using (ScanMarker.Auto())
                {
                    Scan();
                }

                _scanned = order;
            }

            new DriveJob
            {
                Sources = SystemAPI.GetComponentLookup<LocalTransform>(true),
                StepRate = 1f / SystemAPI.Time.DeltaTime,
            }.ScheduleParallel();
        }

        private void Scan()
        {
            using var sources = _sources.ToEntityArray(Allocator.Temp);
            var current = new HashSet<Entity>();
            foreach (var source in sources)
            {
                current.Add(source);
                var made = _colliders.TryGetValue(source, out var collider) && EntityManager.Exists(collider);
                if (!made)
                {
                    _colliders[source] = Create(source);
                }
            }

            _gone.Clear();
            foreach (var (source, collider) in _colliders)
            {
                if (!current.Contains(source))
                {
                    _gone.Add(source);
                    EntityManager.DestroyEntity(collider);
                }
            }

            foreach (var source in _gone)
            {
                _colliders.Remove(source);
            }
        }

        private Entity Create(Entity source)
        {
            var box = EntityManager.GetComponentData<PickBox>(source);
            var place = EntityManager.GetComponentData<LocalTransform>(source);
            var moves = EntityManager.HasComponent<Locomotion>(source);
            var collider = EntityManager.CreateEntity(typeof(PhysicsCollider), typeof(LocalTransform), typeof(ColliderSource));
            EntityManager.SetComponentData(collider, new PhysicsCollider { Value = Shape(box) });
            EntityManager.SetComponentData(collider, place);
            EntityManager.SetComponentData(collider, new ColliderSource { Value = source });
            // No PhysicsMass makes a moving body kinematic: contacts push ragdolls, never the box.
            if (moves)
            {
                EntityManager.AddComponentData(collider, new PhysicsVelocity());
            }
            else
            {
                EntityManager.AddComponentData(collider, new LocalToWorld { Value = place.ToMatrix() });
            }

            EntityManager.AddSharedComponent(collider, new PhysicsWorldIndex(PhysicsGroup.WorldIndex));
            return collider;
        }

        // A prefab's units share one box.
        private BlobAssetReference<Collider> Shape(in PickBox box)
        {
            var key = (box.Center, box.HalfSize);
            if (!_shapes.TryGetValue(key, out var shape))
            {
                var geometry = new BoxGeometry
                {
                    Center = box.Center,
                    Orientation = quaternion.identity,
                    Size = box.HalfSize * 2f,
                    BevelRadius = 0f,
                };
                shape = Unity.Physics.BoxCollider.Create(geometry, PhysicsGroup.GroundFilter);
                _shapes.Add(key, shape);
            }

            return shape;
        }

        /// <summary>Sets each vehicle box's velocity to reach its unit by the end of the step.</summary>
        [BurstCompile]
        private partial struct DriveJob : IJobEntity
        {
            // Sources are units and the job writes only boxes, so the two never alias.
            [ReadOnly, NativeDisableContainerSafetyRestriction]
            public ComponentLookup<LocalTransform> Sources;

            public float StepRate;

            private void Execute(ref LocalTransform transform, ref PhysicsVelocity velocity, in ColliderSource source)
            {
                if (!Sources.TryGetComponent(source.Value, out var target))
                {
                    return;
                }

                var jumped = math.distancesq(transform.Position, target.Position) > JumpDistance * JumpDistance;
                if (jumped)
                {
                    transform = target;
                    velocity = default;
                    return;
                }

                var mass = PhysicsMass.CreateKinematic(MassProperties.UnitSphere);
                velocity = PhysicsVelocity.CalculateVelocityToTarget(mass, transform.Position, transform.Rotation,
                    new RigidTransform(target.Rotation, target.Position), StepRate);
            }
        }
    }
}
