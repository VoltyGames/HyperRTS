using System.Linq;
using HyperRTS.Presentation.Environment;
using HyperRTS.Presentation.Impacts;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Replays;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Tests.Impacts
{
    /// <summary>Impact marks: the ring, fades, sizes and ground frame, and the system laying, capping and clearing them.</summary>
    public class ImpactMarkTests
    {
        private const int Mortar = 7;

        private PresentationWorldKit _kit;
        private World _world;
        private EntityManager _em;
        private ImpactMarkSystem _system;
        private InteractionStampSystem _stamps;
        private ImpactMark _mark;
        private Material _decal;

        [SetUp]
        public void SetUp()
        {
            _kit = new PresentationWorldKit("Impact Mark Tests");
            _world = _kit.World;
            _em = _kit.EntityManager;
            _stamps = _world.CreateSystemManaged<InteractionStampSystem>();
            _system = _world.CreateSystemManaged<ImpactMarkSystem>();
            _kit.AddCues();

            _decal = new Material(Shader.Find("Shader Graphs/Decal"));
            _mark = ScriptableObject.CreateInstance<ImpactMark>();
            _mark.decals = new[] { _decal };
            _mark.size = new Vector2(3f, 3f);
            _mark.depth = 1f;
            _mark.lifetime = 10f;
            _mark.fadeTime = 4f;
            _mark.maxCount = 2;

            var prefab = _em.CreateEntity(typeof(Prefab));
            _em.AddComponentData(prefab, new EntityInfo { TypeId = Mortar });
            _em.AddBuffer<EntityImpactMark>(prefab).Add(new EntityImpactMark { Mark = _mark });
            _kit.AddRegistry();
        }

        [TearDown]
        public void TearDown()
        {
            _kit.Dispose();
            Object.DestroyImmediate(_mark);
            Object.DestroyImmediate(_decal);
        }

        private void Land(float3 position, ImpactSurface surface = ImpactSurface.Ground, int typeId = Mortar) =>
            _kit.PushCue(new CueEvent
            {
                TypeId = typeId, Slot = CueSlot.Impact, Position = position, Faction = 1, Surface = surface,
            });

        private void Run(double time)
        {
            _world.SetTime(new TimeData(time, 1f / 30f));
            _stamps.Update();
            _system.Update();
            _kit.ClearCues();
        }

        private static DecalProjector[] Shown() => Resources.FindObjectsOfTypeAll<DecalProjector>()
            .Where(p => p.transform.parent != null && p.transform.parent.name == "HyperRTS Impact Marks" && p.enabled)
            .ToArray();

        [Test]
        public void GroundImpact_LaysTheMarkOnTheGround_OtherSurfacesAndTypesNone()
        {
            Land(new float3(5f, 1f, -2f));
            Land(new float3(9f, 0f, 0f), ImpactSurface.Water);
            Land(new float3(9f, 0f, 0f), ImpactSurface.Structure);
            Land(new float3(9f, 8f, 0f), ImpactSurface.Air);
            Land(new float3(9f, 0f, 0f), typeId: 99);
            Run(1.0);

            var shown = Shown();
            Assert.AreEqual(1, shown.Length);
            Assert.That(Vector3.Distance(new Vector3(5f, 0f, -2f), shown[0].transform.position), Is.LessThan(1e-4f),
                "at terrain height, flat ground here");
            Assert.AreEqual(new Vector3(3f, 3f, 1f), shown[0].size);
            Assert.AreSame(_decal, shown[0].material);
        }

        [Test]
        public void PastTheCap_TheOldestMarkMoves()
        {
            Land(new float3(1f, 0f, 0f));
            Run(1.0);
            Land(new float3(2f, 0f, 0f));
            Run(2.0);
            Land(new float3(3f, 0f, 0f));
            Run(3.0);

            var xs = Shown().Select(p => Mathf.Round(p.transform.position.x)).OrderBy(x => x).ToArray();
            CollectionAssert.AreEqual(new[] { 2f, 3f }, xs);
        }

        [Test]
        public void Marks_HoldTheirLifetime_ThenFadeOut()
        {
            Land(float3.zero);
            Run(1.0);
            Run(11.0);
            Assert.AreEqual(1f, Shown()[0].fadeFactor, 1e-3f);

            Run(13.0);
            Assert.AreEqual(0.5f, Shown()[0].fadeFactor, 0.02f);

            Run(15.5);
            Assert.IsEmpty(Shown());
        }

        [Test]
        public void CraterMark_ScarsTheGround_HalfItsSizeAcross()
        {
            _mark.craterDepth = 0.8f;
            Land(new float3(5f, 0f, -2f));
            Land(new float3(9f, 0f, 0f), ImpactSurface.Water);
            Run(1.0);

            var scars = _stamps.Stamps.Scars;
            Assert.AreEqual(1, scars.Count, "one scar, on the ground only");
            Assert.AreEqual(new float2(5f, -2f), scars[0].Center);
            Assert.AreEqual(new float2(1.5f), scars[0].HalfSize);
            Assert.AreEqual(1, _stamps.Stamps.Trample.Count, "grass flattened in the crater");
        }

        [Test]
        public void MarkWithoutCraterOrScorch_LeavesNoScar()
        {
            Land(float3.zero);
            Run(1.0);

            Assert.IsEmpty(_stamps.Stamps.Scars);
        }

        [Test]
        public void ReplaySeekBack_ResetsTheScars_AndClearsTheMarks()
        {
            var clock = _em.CreateEntity();
            _em.AddComponentData(clock, new ReplayPlayback { Time = 20f, Duration = 60f });
            Land(float3.zero);
            Run(1.0);
            Assert.IsFalse(_stamps.Stamps.Reset);
            Assert.AreEqual(1, Shown().Length, "replay time, not world time, ages the mark");

            _em.SetComponentData(clock, new ReplayPlayback { Time = 5f, Duration = 60f });
            Run(1.1);

            Assert.IsTrue(_stamps.Stamps.Reset);
            Assert.IsEmpty(Shown());
        }

        [Test]
        public void ScarsGround_OnlyWithACraterOrScorch()
        {
            Assert.IsFalse(_mark.ScarsGround);

            _mark.scorch = 0.9f;
            Assert.IsTrue(_mark.ScarsGround);
        }

        [Test]
        public void Ring_FillsInOrder_ThenReusesTheOldest()
        {
            var ring = new ImpactMarkRing(3);

            Assert.AreEqual(0, ring.Place(1f));
            Assert.AreEqual(1, ring.Place(2f));
            Assert.AreEqual(2, ring.Place(3f));
            Assert.AreEqual(3, ring.Count);
            Assert.AreEqual(0, ring.Place(4f), "past the cap the oldest slot goes");
            Assert.AreEqual(1f, ring.Age(0, 5f), 1e-5f);
            Assert.AreEqual(3, ring.Count);

            ring.Clear();
            Assert.AreEqual(0, ring.Count);
            Assert.AreEqual(0, ring.Place(6f));
        }

        [Test]
        public void Strength_HoldsForTheLifetime_FadesOut_AndLastsWithoutOne()
        {
            Assert.AreEqual(1f, ImpactMarkMath.Strength(10f, 10f, 4f));
            Assert.AreEqual(0.5f, ImpactMarkMath.Strength(12f, 10f, 4f), 1e-5f);
            Assert.AreEqual(0f, ImpactMarkMath.Strength(15f, 10f, 4f));
            Assert.AreEqual(0f, ImpactMarkMath.Strength(10.1f, 10f, 0f), "no fade time: gone at once");
            Assert.AreEqual(1f, ImpactMarkMath.Strength(1e6f, 0f, 4f), "no lifetime: kept until reused");
        }

        [Test]
        public void Diameter_SpansTheRange_AndGrowsWithTheSplash()
        {
            _mark.size = new Vector2(2f, 4f);
            _mark.sizePerSplashRadius = 0.5f;

            Assert.AreEqual(2f, ImpactMarkMath.Diameter(_mark, 0f, 0f), 1e-5f);
            Assert.AreEqual(4f, ImpactMarkMath.Diameter(_mark, 0f, 1f), 1e-5f);
            Assert.AreEqual(6f, ImpactMarkMath.Diameter(_mark, 6f, 0.5f), 1e-5f);
        }

        [Test]
        public void GroundFrame_FollowsTheSlope_AndProjectsIntoIt()
        {
            // A 45 degree ramp rising along x: height = x.
            using var heights = new NativeArray<float>(new[] { 0f, 10f, 0f, 10f }, Allocator.Temp);
            var terrain = TerrainHeight.Create(heights, new int2(2, 2), new float2(0f, 0f), 10f, Allocator.Persistent);
            try
            {
                var normal = ImpactMarkMath.GroundNormal(terrain, new float2(5f, 5f), 1f);
                Assert.That(math.distance(math.normalize(new float3(-1f, 1f, 0f)), normal), Is.LessThan(1e-4f));

                var facing = ImpactMarkMath.Facing(normal, 0.7f);
                var forward = math.mul(facing, new float3(0f, 0f, 1f));
                var up = math.mul(facing, new float3(0f, 1f, 0f));
                Assert.That(math.distance(-normal, forward), Is.LessThan(1e-4f), "projects into the ground");
                Assert.AreEqual(0f, math.dot(up, normal), 1e-4f, "the decal lies in the ground's plane");
            }
            finally
            {
                terrain.Blob.Dispose();
            }
        }

        [Test]
        public void Facing_OnFlatGround_ProjectsStraightDown_TurnedByTheYaw()
        {
            var facing = ImpactMarkMath.Facing(new float3(0f, 1f, 0f), math.PI / 2f);
            var forward = math.mul(facing, new float3(0f, 0f, 1f));
            var up = math.mul(facing, new float3(0f, 1f, 0f));

            Assert.That(math.distance(new float3(0f, -1f, 0f), forward), Is.LessThan(1e-4f));
            Assert.That(math.distance(new float3(1f, 0f, 0f), up), Is.LessThan(1e-4f), "a quarter turn from north");
        }
    }
}
