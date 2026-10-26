using HyperRTS.Client.Interaction;
using HyperRTS.Input.Commands;
using HyperRTS.Presentation.Overlays;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Tests;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Tests.Overlays
{
    /// <summary>Placing a building: the tinted visual-only ghost and its yaw from drag aim, turns, taps and the 15 degree snap.</summary>
    public class PlacementTests
    {
        private static readonly float3 Spot = new(5f, 0f, 3f);
        private const float Yaw = 0.5f;

        private World _world;
        private EntityManager _em;
        private Material _source;
        private Material _ghost;
        private Mesh _mesh;
        private OverlayStyle _style;
        private PlacementGhosts _ghosts;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Placement Ghost Tests");
            _em = _world.EntityManager;
            _source = new Material(Shader.Find("HyperRTS/Overlay"));
            _ghost = new Material(Shader.Find("HyperRTS/Placement Ghost"));
            _mesh = new Mesh();
            _style = ScriptableObject.CreateInstance<OverlayStyle>();
            _ghosts = new PlacementGhosts(_ghost);
        }

        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
            Object.DestroyImmediate(_source);
            Object.DestroyImmediate(_ghost);
            Object.DestroyImmediate(_mesh);
            Object.DestroyImmediate(_style);
        }

        // A building prefab with one drawn piece, as baking makes it.
        private Entity BuildingPrefab()
        {
            var prefab = _em.CreateEntity(typeof(Prefab), typeof(LocalTransform), typeof(LocalToWorld), typeof(Health),
                typeof(NavObstacle));
            _em.SetComponentData(prefab, LocalTransform.Identity);
            _em.SetComponentData(prefab, new NavObstacle { Size = new float2(4f, 4f) });
            var part = _em.CreateEntity(typeof(Prefab), typeof(LocalTransform), typeof(Parent), typeof(MaterialMeshInfo));
            _em.SetComponentData(part, new Parent { Value = prefab });
            _em.SetComponentData(part, MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
            _em.AddSharedComponent(part, new RenderMeshArray(new[] { _source }, new[] { _mesh }));
            _em.AddBuffer<LinkedEntityGroup>(prefab).AddRange(new NativeArray<LinkedEntityGroup>(
                new LinkedEntityGroup[] { prefab, part }, Allocator.Temp));
            return prefab;
        }

        private Entity StartPlacement(Entity prefab)
        {
            var singleton = _em.CreateEntity();
            _em.AddComponentData(singleton, new PlacementState
            {
                Active = true, Prefab = prefab, Position = Spot, Yaw = Yaw, Valid = false,
            });
            _em.AddBuffer<PlacementSegment>(singleton);
            return singleton;
        }

        private NativeArray<Entity> LiveGhosts()
        {
            using var query = _em.CreateEntityQuery(ComponentType.ReadOnly<LinkedEntityGroup>());
            return query.ToEntityArray(Allocator.Temp);
        }

        [Test]
        public void Ghost_IsAVisualOnlyCopy_InTheGhostMaterial_AtTheSpot()
        {
            StartPlacement(BuildingPrefab());

            _ghosts.Update(_em, _style);

            var ghosts = LiveGhosts();
            Assert.AreEqual(1, ghosts.Length);
            var root = ghosts[0];
            Assert.IsFalse(_em.HasComponent<Health>(root), "no gameplay");
            Assert.IsFalse(_em.HasComponent<NavObstacle>(root), "never stamps the nav grid");
            var transform = _em.GetComponentData<LocalTransform>(root);
            MathAssert.Near(Spot, transform.Position);
            MathAssert.Near(math.rotate(quaternion.RotateY(Yaw), math.forward()), math.rotate(transform.Rotation, math.forward()));

            var part = _em.GetBuffer<LinkedEntityGroup>(root)[1].Value;
            Assert.AreSame(_ghost, _em.GetSharedComponent<RenderMeshArray>(part).MaterialReferences[0].Value);
            var tint = _em.GetComponentData<URPMaterialPropertyBaseColor>(part).Value;
            float4 invalid = (Vector4)_style.placementInvalid.linear;
            Assert.AreEqual(invalid, tint, "tinted as a refused spot");
        }

        [Test]
        public void GhostMaterial_HasTheMetaPassSurfaceCacheGIDrawsMaterialsWith() =>
            Assert.GreaterOrEqual(_ghost.FindPass("Meta"), 0);

        [Test]
        public void Ghost_IsDestroyed_WhenPlacementEnds()
        {
            var singleton = StartPlacement(BuildingPrefab());
            _ghosts.Update(_em, _style);

            _em.SetComponentData(singleton, new PlacementState());
            _ghosts.Update(_em, _style);

            Assert.AreEqual(0, LiveGhosts().Length);
            using var tinted = _em.CreateEntityQuery(new EntityQueryBuilder(Allocator.Temp)
                .WithAll<URPMaterialPropertyBaseColor>().WithOptions(EntityQueryOptions.IncludePrefab));
            Assert.IsTrue(tinted.IsEmpty, "the ghost's visual prefab goes too");
        }

        [Test]
        public void Rotation_StartsFacingTheCamera_AndAimPointsTheFrontAlongThePull()
        {
            var rotation = PlacementRotation.Start();
            Assert.AreEqual(PlacementMath.CameraYaw, rotation.Yaw(false));

            rotation.Aim(new float2(1f, 1f));

            var yaw = rotation.Yaw(false);
            Assert.AreEqual(math.SQRT2 * 0.5f, math.sin(yaw), 1e-5f);
            Assert.AreEqual(math.SQRT2 * 0.5f, math.cos(yaw), 1e-5f);
        }

        [Test]
        public void Rotation_TapAddsFortyFiveDegrees()
        {
            var rotation = PlacementRotation.Start();
            rotation.Press(100f);
            rotation.Turn(102f);
            rotation.Release();

            Assert.AreEqual(PlacementMath.CameraYaw + math.radians(45f), rotation.Yaw(false), 1e-5f);
            Assert.IsFalse(rotation.IsTurning);
        }

        [Test]
        public void Rotation_HeldTurnFollowsThePointer_AndSnapsToFifteenDegrees()
        {
            var rotation = PlacementRotation.Start();
            rotation.Press(100f);
            rotation.Turn(120f);
            rotation.Turn(140f);

            Assert.IsTrue(rotation.IsTurning);
            Assert.AreEqual(PlacementMath.CameraYaw + math.radians(20f), rotation.Yaw(false), 1e-5f, "half a degree a pixel");
            Assert.AreEqual(PlacementMath.CameraYaw + math.radians(15f), rotation.Yaw(true), 1e-5f);
            rotation.Release();
            Assert.AreEqual(PlacementMath.CameraYaw + math.radians(20f), rotation.Yaw(false), 1e-5f, "a turn is no tap");
        }

        [Test]
        public void SnapEnd_TurnsTheLineToAFifteenDegreeStep_KeepingItsLength()
        {
            var start = new float3(1f, 0f, 1f);
            var end = PlacementRotation.SnapEnd(start, start + new float3(10f, 2f, 4f));

            var delta = end.xz - start.xz;
            Assert.AreEqual(math.length(new float2(10f, 4f)), math.length(delta), 1e-4f);
            Assert.AreEqual(15f, math.degrees(math.atan2(delta.y, delta.x)), 1e-3f);
            Assert.AreEqual(2f, end.y);
        }
    }
}
