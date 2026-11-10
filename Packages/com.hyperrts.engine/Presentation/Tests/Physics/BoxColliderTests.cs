using System;
using System.Collections.Generic;
using HyperRTS.Presentation.Physics;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Movement;
using HyperRTS.Simulation.Selection;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests.Physics
{
    /// <summary>
    /// Buildings and vehicles in the presented physics world: a ragdoll dropped over one comes to rest on its roof or
    /// hull. Nothing else is in that world, so a missed box lets it fall forever.
    /// </summary>
    public class BoxColliderTests
    {
        private const float Step = 1f / 60f;
        private const float Radius = 0.1f;

        private World _world;
        private EntityManager _em;
        private BlobAssetReference<Unity.Physics.Collider> _capsule;
        private double _elapsed;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Box Collider Tests");
            _em = _world.EntityManager;
            var physics = typeof(PhysicsSystemGroup).Assembly;
            var systems = new List<Type>
            {
                typeof(FixedStepSimulationSystemGroup),
                typeof(TransformSystemGroup),
                typeof(PhysicsStartSystem),
                typeof(PhysicsGroup),
                typeof(BoxColliderSystem),
            };
            foreach (var type in DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default))
            {
                if (type.Assembly == physics)
                {
                    systems.Add(type);
                }
            }

            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(_world, systems);
            var bone = new CapsuleGeometry { Vertex0 = float3.zero, Vertex1 = new float3(0f, 0.4f, 0f), Radius = Radius };
            _capsule = Unity.Physics.CapsuleCollider.Create(bone, PhysicsGroup.BodyFilter);
            _elapsed = 0d;
        }

        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
            _capsule.Dispose();
        }

        [TestCase(false, 4f, TestName = "A ragdoll dropped over a building rests on its roof")]
        [TestCase(true, 2.4f, TestName = "A ragdoll dropped over a vehicle rests on its hull")]
        public void RagdollDroppedOverBox_RestsOnItsTop(bool vehicle, float top)
        {
            var at = new float3(30f, 0f, -12f);
            var box = new PickBox { Center = new float3(0f, top * 0.5f, 0f), HalfSize = new float3(3f, top * 0.5f, 5f) };
            CreateSource(vehicle, LocalTransform.FromPositionRotation(at, quaternion.RotateY(0.6f)), box);
            var hips = DropRagdoll(at + new float3(0.3f, top + 3f, 0.5f));

            Run(4f);

            var rest = _em.GetComponentData<LocalTransform>(hips).Position;
            var speed = math.length(_em.GetComponentData<PhysicsVelocity>(hips).Linear);
            Assert.That(rest.y, Is.InRange(at.y + top, at.y + top + 0.6f), $"hips at {rest}");
            Assert.Less(speed, 0.5f);
        }

        private void CreateSource(bool vehicle, in LocalTransform place, in PickBox box)
        {
            var source = _em.CreateEntity(typeof(LocalTransform), typeof(PickBox));
            _em.SetComponentData(source, place);
            _em.SetComponentData(source, box);
            if (vehicle)
            {
                _em.AddComponentData(source, new Locomotion());
            }
            else
            {
                _em.AddComponent<BuildingTag>(source);
            }
        }

        /// <summary>Two capsules standing a little tilted, the spine on the hips by a ball joint; returns the hips.</summary>
        private Entity DropRagdoll(float3 at)
        {
            var tilt = quaternion.RotateZ(0.3f);
            var hips = CreateBone(at, tilt);
            var spine = CreateBone(at + math.rotate(tilt, new float3(0f, 0.5f, 0f)), tilt);
            var joint = _em.CreateEntity(typeof(PhysicsConstrainedBodyPair), typeof(PhysicsJoint));
            _em.SetComponentData(joint, new PhysicsConstrainedBodyPair(spine, hips, false));
            _em.SetComponentData(joint, PhysicsJoint.CreateBallAndSocket(new float3(0f, -0.05f, 0f), new float3(0f, 0.45f, 0f)));
            _em.AddSharedComponent(joint, new PhysicsWorldIndex(PhysicsGroup.WorldIndex));
            return hips;
        }

        private Entity CreateBone(float3 at, quaternion tilt)
        {
            var bone = _em.CreateEntity(typeof(LocalTransform), typeof(PhysicsCollider), typeof(PhysicsVelocity),
                typeof(PhysicsMass), typeof(PhysicsDamping));
            _em.SetComponentData(bone, LocalTransform.FromPositionRotation(at, tilt));
            _em.SetComponentData(bone, new PhysicsCollider { Value = _capsule });
            _em.SetComponentData(bone, PhysicsMass.CreateDynamic(_capsule.Value.MassProperties, 10f));
            _em.SetComponentData(bone, new PhysicsDamping { Linear = 0f, Angular = 2f });
            _em.AddSharedComponent(bone, new PhysicsWorldIndex(PhysicsGroup.WorldIndex));
            return bone;
        }

        private void Run(float seconds)
        {
            for (var steps = (int)(seconds / Step); steps > 0; steps--)
            {
                _elapsed += Step;
                _world.SetTime(new TimeData(_elapsed, Step));
                _world.Update();
            }
        }
    }
}
